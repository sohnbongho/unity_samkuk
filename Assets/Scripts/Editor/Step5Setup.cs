using System.IO;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 5: 투사체 프리팹, 무기 스프라이트/데이터 3종, 플레이어 WeaponController를 구성한다.</summary>
    public static class Step5Setup
    {
        const string SpriteDir = "Assets/Sprites";
        const string DataDir = "Assets/ScriptableObjects/Weapons";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const int PPU = 64;

        [MenuItem("Samkuk/Step 5 - Setup Weapons")]
        public static void Run()
        {
            Directory.CreateDirectory(DataDir);
            CreateSprites();
            AssetDatabase.Refresh();

            CreateWeaponData();
            CreateProjectilePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetupPlayerPrefab();
            Debug.Log("[Samkuk] Step 5 setup 완료 (시작 무기: 활. Play 후 F2: 무기 레벨업)");
        }

        // ───────────────────────── 스프라이트 ─────────────────────────

        static void CreateSprites()
        {
            // 화살: 가로로 긴 몸통 + 촉
            WriteSprite("Arrow", 40, 14, (x, y, w, h) =>
            {
                float cy = (h - 1) * 0.5f;
                float dy = Mathf.Abs(y - cy);
                bool shaft = x < w - 12 && dy <= 1.2f;
                float headLen = 12f;
                float hx = x - (w - headLen);
                bool head = hx >= 0f && dy <= (1f - hx / headLen) * (h * 0.5f - 0.5f);
                bool fletch = x < 8 && dy <= 1f + (8 - x) * 0.45f;
                if (head) return new Color(0.85f, 0.85f, 0.9f);
                if (shaft || fletch) return new Color(1f, 0.85f, 0.4f);
                return Color.clear;
            });

            // 회전 도끼(칼날): 4갈래 별
            WriteSprite("Blade", 48, 48, (x, y, w, h) =>
            {
                float dx = Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f);
                float dy = Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f);
                // 별 모양: |dx|^0.5 + |dy|^0.5 <= 1
                bool inside = Mathf.Sqrt(dx) + Mathf.Sqrt(dy) <= 1f;
                if (!inside) return Color.clear;
                float core = dx + dy;
                return core < 0.35f ? new Color(0.95f, 0.95f, 1f) : new Color(0.65f, 0.7f, 0.8f);
            });

            // 베기 이펙트: 반투명 원판 + 테두리 링
            WriteSprite("Slash", 128, 128, (x, y, w, h) =>
            {
                float r = w * 0.5f;
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                if (d > r - 1f) return Color.clear;
                if (d > r - 8f) return new Color(1f, 1f, 1f, 0.9f);
                return new Color(1f, 1f, 1f, 0.28f);
            });
        }

        static void WriteSprite(string name, int w, int h, System.Func<int, int, int, int, Color> pixel)
        {
            string path = $"{SpriteDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel(x, y, w, h));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        // ───────────────────────── 데이터/프리팹 ─────────────────────────

        static void CreateWeaponData()
        {
            CreateData("Weapon_Bow", d =>
            {
                d.displayName = "활";
                d.type = WeaponType.Arrow;
                d.sprite = LoadSprite("Arrow");
                d.tint = Color.white;
                d.damage = 10f;
                d.cooldown = 0.9f;
                d.range = 9f;
                d.count = 1;
                d.projectileSpeed = 12f;
                d.pierce = 1;
                d.duration = 1.5f;
                d.size = 0.3f;
            });
            CreateData("Weapon_Sword", d =>
            {
                d.displayName = "검 베기";
                d.type = WeaponType.Slash;
                d.sprite = LoadSprite("Slash");
                d.tint = new Color(1f, 0.95f, 0.8f);
                d.damage = 14f;
                d.cooldown = 1.2f;
                d.range = 2.4f;
                d.count = 1;
                d.duration = 0.18f;
            });
            CreateData("Weapon_Axe", d =>
            {
                d.displayName = "회전 도끼";
                d.type = WeaponType.Orbit;
                d.sprite = LoadSprite("Blade");
                d.tint = Color.white;
                d.damage = 6f;
                d.range = 1.8f;
                d.count = 2;
                d.size = 0.45f;
                d.rotateSpeed = 200f;
                d.tickInterval = 0.3f;
                d.levelsPerExtraCount = 2;
            });
        }

        static void CreateData(string name, System.Action<WeaponData> init)
        {
            string path = $"{DataDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null) return; // 사용자가 조정한 값 유지

            var data = ScriptableObject.CreateInstance<WeaponData>();
            init(data);
            AssetDatabase.CreateAsset(data, path);
        }

        static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        static void CreateProjectilePrefab()
        {
            var root = new GameObject("Projectile") { layer = LayerMask.NameToLayer(GameLayers.PlayerProjectile) };
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Arrow");
            sr.sortingLayerName = GameLayers.Sorting.Projectile;
            root.AddComponent<Projectile>();

            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);
        }

        static void SetupPlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            var wc = root.GetComponent<WeaponController>();
            if (wc == null) wc = root.AddComponent<WeaponController>();

            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<Projectile>();
            var bow = AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataDir}/Weapon_Bow.asset");
            var sword = AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataDir}/Weapon_Sword.asset");
            var axe = AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataDir}/Weapon_Axe.asset");
            if (projectile == null || bow == null || sword == null || axe == null)
            {
                Debug.LogError($"[Samkuk] 에셋 로드 실패 projectile={projectile} bow={bow} sword={sword} axe={axe}");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var so = new SerializedObject(wc);
            so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
            var list = so.FindProperty("startingWeapons");
            // 시작 무기는 활 하나. 나머지는 레벨업(Step 6)으로 얻는다.
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = bow;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
