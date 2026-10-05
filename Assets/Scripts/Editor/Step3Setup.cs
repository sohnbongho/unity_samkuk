using System.IO;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 3: 적 데이터/프리팹 생성, 씬에 EnemyManager·EnemySpawner·DebugOverlay 구성.</summary>
    public static class Step3Setup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const string DataDir = "Assets/ScriptableObjects/Enemies";
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string EnemySpritePath = "Assets/Sprites/Enemy.png";

        [MenuItem("Samkuk/Step 3 - Setup Enemies")]
        public static void Run()
        {
            Directory.CreateDirectory(DataDir);
            AssetDatabase.Refresh();

            var soldier = CreateEnemyData("Enemy_Soldier", d =>
            {
                d.displayName = "황건적 병사";
                d.tint = Color.white;
                d.scale = 1f;
                d.moveSpeed = 1.6f;
                d.maxHp = 10;
                d.contactDamage = 5;
                d.expReward = 1;
                d.colliderRadius = 0.4f;
            });
            var scout = CreateEnemyData("Enemy_Scout", d =>
            {
                d.displayName = "황건적 척후";
                d.tint = new Color(1f, 0.65f, 0.25f);
                d.scale = 0.8f;
                d.moveSpeed = 2.6f;
                d.maxHp = 6;
                d.contactDamage = 3;
                d.expReward = 1;
                d.colliderRadius = 0.4f;
            });

            CreateEnemyPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene();
            Debug.Log("[Samkuk] Step 3 setup 완료 (Play 후 F1: 적 +100)");
        }

        static EnemyData CreateEnemyData(string assetName, System.Action<EnemyData> init)
        {
            string path = $"{DataDir}/{assetName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                init(data);
                AssetDatabase.CreateAsset(data, path);
            }
            // 이미 존재하면 사용자가 조정한 값을 유지한다.
            return data;
        }

        static Enemy CreateEnemyPrefab()
        {
            var root = new GameObject("Enemy") { layer = LayerMask.NameToLayer(GameLayers.Enemy) };

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.None;

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
            col.isTrigger = true; // 접촉 피해/투사체 판정은 트리거로 처리 (물리적으로 밀지 않음)

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemySpritePath);
            sr.sortingLayerName = GameLayers.Sorting.Enemy;

            root.AddComponent<Enemy>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<Enemy>();
        }

        static void BuildScene()
        {
            // 주의: OpenScene(Single)은 사용 중이지 않은 에셋을 언로드하므로
            // 에셋 로드는 반드시 씬을 연 뒤에 해야 한다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath)?.GetComponent<Enemy>();
            var soldier = AssetDatabase.LoadAssetAtPath<EnemyData>($"{DataDir}/Enemy_Soldier.asset");
            var scout = AssetDatabase.LoadAssetAtPath<EnemyData>($"{DataDir}/Enemy_Scout.asset");
            if (enemyPrefab == null || soldier == null || scout == null)
            {
                Debug.LogError($"[Samkuk] 에셋 로드 실패 prefab={enemyPrefab} soldier={soldier} scout={scout}");
                return;
            }

            var old = GameObject.Find("GameSystems");
            if (old != null) Object.DestroyImmediate(old);

            var player = GameObject.Find("Player");
            var cam = Camera.main;

            var sys = new GameObject("GameSystems");
            var manager = sys.AddComponent<EnemyManager>();
            var spawner = sys.AddComponent<EnemySpawner>();
            var overlay = sys.AddComponent<DebugOverlay>();

            manager.Target = player != null ? player.transform : null;

            var sp = new SerializedObject(spawner);
            sp.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            sp.FindProperty("manager").objectReferenceValue = manager;
            sp.FindProperty("cam").objectReferenceValue = cam;
            var table = sp.FindProperty("spawnTable");
            table.arraySize = 2;
            SetEntry(table.GetArrayElementAtIndex(0), soldier, 3f);
            SetEntry(table.GetArrayElementAtIndex(1), scout, 1f);
            sp.ApplyModifiedPropertiesWithoutUndo();

            if (spawner.EnemyPrefab == null || spawner.SpawnTable.Exists(s => s.data == null))
                Debug.LogError("[Samkuk] EnemySpawner 참조 연결 실패 (enemyPrefab/spawnTable)");

            var ov = new SerializedObject(overlay);
            ov.FindProperty("spawner").objectReferenceValue = spawner;
            ov.FindProperty("manager").objectReferenceValue = manager;
            ov.ApplyModifiedPropertiesWithoutUndo();

            if (player == null) Debug.LogWarning("[Samkuk] Player가 씬에 없습니다. 먼저 Step 2 를 실행하세요.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetEntry(SerializedProperty entry, EnemyData data, float weight)
        {
            entry.FindPropertyRelative("data").objectReferenceValue = data;
            entry.FindPropertyRelative("weight").floatValue = weight;
        }
    }
}
