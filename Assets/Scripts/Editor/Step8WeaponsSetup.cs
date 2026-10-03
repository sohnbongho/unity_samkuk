using System.IO;
using Samkuk.Data;
using UnityEditor;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 8-2: 신규 무기 스프라이트(번개, 불길)와 무기 7종을 만들고 레벨업 카탈로그에 추가한다.</summary>
    public static class Step8WeaponsSetup
    {
        const string SpriteDir = "Assets/Sprites";
        const string WeaponDir = "Assets/ScriptableObjects/Weapons";
        const string CatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";
        const int PPU = 64;

        /// <summary>카탈로그에 포함될 신규 무기 에셋 이름 (순서 유지).</summary>
        static readonly string[] NewWeaponNames =
        {
            "Weapon_Thrust", "Weapon_FireZone", "Weapon_Lightning", "Weapon_Rain",
            "Weapon_Nova", "Weapon_Crossbow", "Weapon_Knives"
        };

        [MenuItem("Samkuk/Step 8-2 - Setup More Weapons")]
        public static void Run()
        {
            Directory.CreateDirectory(WeaponDir);
            CreateSprites();
            AssetDatabase.Refresh();

            CreateWeapons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AddToCatalog();
            AssetDatabase.SaveAssets();
            Debug.Log("[Samkuk] Step 8-2 setup 완료 (신규 무기 7종이 레벨업 선택지에 추가됨)");
        }

        // ───────────────────────── 스프라이트 ─────────────────────────

        static void CreateSprites()
        {
            // 번개: 위아래로 긴 지그재그 줄기 (흰색 심 + 노란 테두리)
            WriteSprite("Bolt", 32, 128, (x, y, w, h) =>
            {
                float cx = w * 0.5f + Mathf.Sin(y * 0.18f) * 6f + Mathf.Sin(y * 0.47f) * 2.5f;
                float d = Mathf.Abs(x + 0.5f - cx);
                float taper = Mathf.Lerp(0.6f, 1f, y / (float)h); // 위쪽이 약간 굵음
                if (d < 1.6f * taper) return new Color(1f, 1f, 0.95f);
                if (d < 4.2f * taper) return new Color(1f, 0.9f, 0.3f, 0.85f);
                return Color.clear;
            });

            // 불길: 중심이 밝고 가장자리로 갈수록 옅어지는 주황 원판
            WriteSprite("Fire", 128, 128, (x, y, w, h) =>
            {
                float r = w * 0.5f;
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                if (d > 1f) return Color.clear;
                float angle = Mathf.Atan2(y - r, x - r);
                float wobble = 1f + 0.12f * Mathf.Sin(angle * 7f);
                if (d > wobble * 0.95f) return Color.clear;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, Mathf.Lerp(0.35f, 0.9f, a), Mathf.Lerp(0.1f, 0.4f, a * a), 0.35f + 0.65f * a);
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

        static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

        // ───────────────────────── 무기 데이터 ─────────────────────────

        static void CreateWeapons()
        {
            Create("Weapon_Thrust", w =>
            {
                w.displayName = "창 찌르기"; w.type = WeaponType.Thrust; w.sprite = LoadSprite("Arrow");
                w.description = "가까운 적에게 창을 내질러 일직선상의 적을 꿰뚫는다.";
                w.tint = new Color(0.85f, 0.85f, 1f);
                w.damage = 16f; w.cooldown = 1.3f; w.range = 3.2f; w.size = 0.35f; w.count = 1;
                w.duration = 0.15f; w.knockback = 5f; w.levelsPerExtraCount = 3;
            });
            Create("Weapon_FireZone", w =>
            {
                w.displayName = "화계"; w.type = WeaponType.FireZone; w.sprite = LoadSprite("Fire");
                w.description = "적이 모인 곳에 불길을 일으켜 지속 피해를 준다.";
                w.tint = Color.white;
                w.damage = 5f; w.cooldown = 3.5f; w.range = 8f; w.size = 1.3f; w.count = 1;
                w.duration = 3f; w.tickInterval = 0.4f; w.levelsPerExtraCount = 2;
            });
            Create("Weapon_Lightning", w =>
            {
                w.displayName = "뇌격"; w.type = WeaponType.Lightning; w.sprite = LoadSprite("Bolt");
                w.description = "적 위로 벼락을 떨어뜨려 주변까지 피해를 준다.";
                w.tint = Color.white;
                w.damage = 22f; w.cooldown = 1.8f; w.range = 9f; w.size = 0.9f; w.count = 1;
                w.duration = 0.2f; w.levelsPerExtraCount = 2;
            });
            Create("Weapon_Rain", w =>
            {
                w.displayName = "화살비"; w.type = WeaponType.Rain; w.sprite = LoadSprite("Slash");
                w.description = "적 머리 위로 화살비를 쏟는다. 낙하 지점이 먼저 표시된다.";
                w.tint = new Color(1f, 0.85f, 0.4f);
                w.damage = 9f; w.cooldown = 3f; w.range = 7f; w.size = 0.8f; w.count = 6;
                w.duration = 0.5f; w.levelsPerExtraCount = 1;
            });
            Create("Weapon_Nova", w =>
            {
                w.displayName = "전고"; w.type = WeaponType.Nova; w.sprite = LoadSprite("Slash");
                w.description = "북을 울려 충격파를 퍼뜨리고 적을 밀어낸다.";
                w.tint = new Color(1f, 0.8f, 0.5f);
                w.damage = 14f; w.cooldown = 4f; w.range = 4.5f; w.duration = 0.6f; w.knockback = 9f;
                w.levelsPerExtraCount = 0;
            });
            Create("Weapon_Crossbow", w =>
            {
                w.displayName = "쇠뇌"; w.type = WeaponType.Arrow; w.sprite = LoadSprite("Arrow");
                w.description = "느리지만 강력한 화살로 적을 여럿 관통한다.";
                w.tint = new Color(0.95f, 0.75f, 0.4f);
                w.damage = 28f; w.cooldown = 1.8f; w.range = 11f; w.count = 1;
                w.projectileSpeed = 18f; w.pierce = 3; w.duration = 1.2f; w.size = 0.3f; w.levelsPerExtraCount = 3;
            });
            Create("Weapon_Knives", w =>
            {
                w.displayName = "비도"; w.type = WeaponType.Arrow; w.sprite = LoadSprite("Arrow");
                w.description = "가볍고 빠른 단검을 연달아 던진다.";
                w.tint = new Color(0.8f, 0.9f, 1f);
                w.damage = 5f; w.cooldown = 0.45f; w.range = 8f; w.count = 1;
                w.projectileSpeed = 16f; w.pierce = 1; w.duration = 1.2f; w.size = 0.2f; w.levelsPerExtraCount = 3;
            });
        }

        static void Create(string name, System.Action<WeaponData> init)
        {
            string path = $"{WeaponDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null) return; // 사용자가 조정한 값 유지

            var w = ScriptableObject.CreateInstance<WeaponData>();
            init(w);
            AssetDatabase.CreateAsset(w, path);
        }

        // ───────────────────────── 카탈로그 ─────────────────────────

        static void AddToCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Samkuk] UpgradeCatalog 가 없습니다. Step 6 을 먼저 실행하세요.");
                return;
            }

            catalog.weapons.RemoveAll(w => w == null);
            foreach (var n in NewWeaponNames)
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{n}.asset");
                if (w != null && !catalog.weapons.Contains(w)) catalog.weapons.Add(w);
            }
            EditorUtility.SetDirty(catalog);
        }
    }
}
