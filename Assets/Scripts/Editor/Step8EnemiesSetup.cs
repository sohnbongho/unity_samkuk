using System.IO;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 8-3: 적 병과(보병/궁병/기병). 병과별 스프라이트, 궁병 3종, 적 투사체 프리팹/시스템,
    /// 스테이지 웨이브에 궁병 편성.
    /// </summary>
    public static class Step8EnemiesSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string SpriteDir = "Assets/Sprites";
        const string EnemyDir = "Assets/ScriptableObjects/Enemies";
        const string StagePath = "Assets/ScriptableObjects/Stage/Stage_YellowTurban.asset";
        const string ProjectilePrefabPath = "Assets/Prefabs/EnemyProjectile.prefab";
        const int PPU = 64;

        [MenuItem("Samkuk/Step 8-3 - Setup Enemy Roles (Archers)")]
        public static void Run()
        {
            Directory.CreateDirectory(EnemyDir);
            CreateSprites();
            AssetDatabase.Refresh();

            CreateArchers();
            AssignRolesToExistingEnemies();
            CreateProjectilePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AddArchersToStage();
            AssetDatabase.SaveAssets();

            BuildScene();
            Debug.Log("[Samkuk] Step 8-3 setup 완료 (궁병이 웨이브 2부터 등장)");
        }

        // ───────────────────────── 스프라이트 ─────────────────────────

        static void CreateSprites()
        {
            // 궁병: 위쪽을 향한 삼각형 (뾰족한 화살촉 느낌)
            WriteSprite("Archer", 48, 48, (x, y, w, h) =>
            {
                float t = y / (float)(h - 1);                   // 0(아래) → 1(위)
                float halfWidth = Mathf.Lerp(w * 0.46f, 1.5f, t);
                float dx = Mathf.Abs(x + 0.5f - w * 0.5f);
                if (y < 3 || y > h - 3 || dx > halfWidth) return Color.clear;
                bool edge = dx > halfWidth - 3f || y < 6;
                return edge ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
            });

            // 기병: 가로로 긴 타원 (말의 몸통 느낌) + 앞쪽 머리 돌기
            WriteSprite("Cavalry", 64, 48, (x, y, w, h) =>
            {
                float cx = w * 0.5f - 4f, cy = h * 0.5f;
                float nx = (x + 0.5f - cx) / (w * 0.42f);
                float ny = (y + 0.5f - cy) / (h * 0.34f);
                bool body = nx * nx + ny * ny <= 1f;

                float hx = (x + 0.5f - (w - 11f)) / 8f;
                float hy = (y + 0.5f - (cy + 7f)) / 8f;
                bool head = hx * hx + hy * hy <= 1f;

                if (!body && !head) return Color.clear;
                bool edge = (nx * nx + ny * ny > 0.82f) && body;
                return edge ? new Color(0.72f, 0.72f, 0.72f) : Color.white;
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

        // ───────────────────────── 적 데이터 ─────────────────────────

        static void CreateArchers()
        {
            CreateEnemy("Enemy_YellowArcher", d =>
            {
                d.displayName = "황건 궁병"; d.role = EnemyRole.Archer; d.sprite = LoadSprite("Archer");
                d.tint = new Color(1f, 0.92f, 0.45f); d.scale = 0.95f;
                d.moveSpeed = 1.5f; d.maxHp = 9; d.contactDamage = 3; d.expReward = 2;
                d.attackRange = 5.5f; d.fireInterval = 2.4f;
                d.projectileSpeed = 6f; d.projectileDamage = 6; d.projectileLifetime = 3f;
            });
            CreateEnemy("Enemy_DongzhuoCrossbow", d =>
            {
                d.displayName = "동탁군 노수"; d.role = EnemyRole.Archer; d.sprite = LoadSprite("Archer");
                d.tint = new Color(0.75f, 0.55f, 0.95f); d.scale = 1.05f;
                d.moveSpeed = 1.3f; d.maxHp = 22; d.contactDamage = 6; d.expReward = 3;
                d.attackRange = 6.5f; d.fireInterval = 2.0f;
                d.projectileSpeed = 7f; d.projectileDamage = 8; d.projectileLifetime = 3f;
            });
            CreateEnemy("Enemy_LvbuArcher", d =>
            {
                d.displayName = "여포군 신궁"; d.role = EnemyRole.Archer; d.sprite = LoadSprite("Archer");
                d.tint = new Color(1f, 0.7f, 0.35f); d.scale = 1.1f;
                d.moveSpeed = 1.6f; d.maxHp = 38; d.contactDamage = 8; d.expReward = 4;
                d.attackRange = 7f; d.fireInterval = 1.6f;
                d.projectileSpeed = 8f; d.projectileDamage = 10; d.projectileLifetime = 3f; d.projectileSize = 0.22f;
            });
        }

        static void CreateEnemy(string name, System.Action<EnemyData> init)
        {
            string path = $"{EnemyDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<EnemyData>(path) != null) return; // 사용자가 조정한 값 유지

            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.colliderRadius = 0.4f;
            init(d);
            AssetDatabase.CreateAsset(d, path);
        }

        /// <summary>기존 기병 적에 기병 병과와 스프라이트를 지정한다 (스프라이트는 비어 있을 때만).</summary>
        static void AssignRolesToExistingEnemies()
        {
            var cavalry = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/Enemy_XiliangCavalry.asset");
            if (cavalry != null)
            {
                cavalry.role = EnemyRole.Cavalry;
                if (cavalry.sprite == null) cavalry.sprite = LoadSprite("Cavalry");
                EditorUtility.SetDirty(cavalry);
            }

            // 보스 여포도 기마 무장이므로 기병 병과
            var boss = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/Boss_Lvbu.asset");
            if (boss != null)
            {
                boss.role = EnemyRole.Cavalry;
                if (boss.sprite == null) boss.sprite = LoadSprite("Cavalry");
                EditorUtility.SetDirty(boss);
            }
        }

        // ───────────────────────── 투사체 프리팹 ─────────────────────────

        static void CreateProjectilePrefab()
        {
            var root = new GameObject("EnemyProjectile");
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Projectile");
            sr.sortingLayerName = Samkuk.Core.GameLayers.Sorting.Projectile;
            root.AddComponent<EnemyProjectile>();

            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);
        }

        // ───────────────────────── 스테이지 편성 ─────────────────────────

        /// <summary>웨이브 2~4(15초, 30초, 45초)에 궁병을 한 종류씩 추가한다 (이미 있으면 건너뜀).</summary>
        static void AddArchersToStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
            if (stage == null)
            {
                Debug.LogWarning("[Samkuk] 스테이지 에셋이 없습니다. Step 7 을 먼저 실행하세요.");
                return;
            }

            var yellow = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/Enemy_YellowArcher.asset");
            var crossbow = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/Enemy_DongzhuoCrossbow.asset");
            var lvbu = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/Enemy_LvbuArcher.asset");

            AddToWave(stage, 1, yellow, 1f);
            AddToWave(stage, 2, crossbow, 1f);
            AddToWave(stage, 3, lvbu, 1f);
            EditorUtility.SetDirty(stage);
        }

        static void AddToWave(StageData stage, int waveIndex, EnemyData enemy, float weight)
        {
            if (enemy == null || waveIndex >= stage.waves.Count) return;
            var wave = stage.waves[waveIndex];
            foreach (var e in wave.enemies)
                if (e.data == enemy) return;
            wave.enemies.Add(new WaveEnemy { data = enemy, weight = weight });
        }

        // ───────────────────────── 씬 ─────────────────────────

        static void BuildScene()
        {
            // 주의: OpenScene(Single) 이후에 에셋을 로드한다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var systems = GameObject.Find("GameSystems");
            if (player == null || systems == null)
            {
                Debug.LogError("[Samkuk] Player / GameSystems 가 씬에 없습니다. Step 2~3 을 먼저 실행하세요.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<EnemyProjectile>();
            if (prefab == null)
            {
                Debug.LogError("[Samkuk] EnemyProjectile 프리팹 로드 실패");
                return;
            }

            var old = systems.GetComponent<EnemyProjectileSystem>();
            if (old != null) Object.DestroyImmediate(old);
            var sys = systems.AddComponent<EnemyProjectileSystem>();
            var so = new SerializedObject(sys);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (sys.Prefab == null) Debug.LogError("[Samkuk] EnemyProjectileSystem 참조 연결 실패");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
