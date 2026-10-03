using System.IO;
using Samkuk.Core;
using Samkuk.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 2: Player 프리팹, 무한 배경, 카메라 추적을 SampleScene에 구성한다.</summary>
    public static class Step2Setup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string PrefabDir = "Assets/Prefabs";
        public const string PlayerPrefabPath = PrefabDir + "/Player.prefab";
        const string PlayerSpritePath = "Assets/Sprites/Player.png";
        const string BackgroundSpritePath = "Assets/Sprites/Background.png";

        [MenuItem("Samkuk/Step 2 - Setup Player & Camera")]
        public static void Run()
        {
            Directory.CreateDirectory(PrefabDir);
            PrepareBackgroundSprite();
            var prefab = CreatePlayerPrefab();
            BuildScene(prefab);
            Debug.Log("[Samkuk] Step 2 setup 완료");
        }

        /// <summary>Tiled 드로우 모드를 쓰려면 스프라이트 메시 타입이 FullRect, 랩 모드가 Repeat여야 한다.</summary>
        static void PrepareBackgroundSprite()
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(BackgroundSpritePath);
            if (imp == null)
            {
                Debug.LogError("[Samkuk] Background.png 가 없습니다. 먼저 Step 1 을 실행하세요.");
                return;
            }
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(settings);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
        }

        static GameObject CreatePlayerPrefab()
        {
            var root = new GameObject("Player") { layer = LayerMask.NameToLayer(GameLayers.Player) };

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            var body = new GameObject("Body") { layer = root.layer };
            body.transform.SetParent(root.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            sr.sortingLayerName = GameLayers.Sorting.Player;

            var controller = root.AddComponent<PlayerController>();
            var so = new SerializedObject(controller);
            so.FindProperty("body").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// URP 2D 조명은 Target Sorting Layers에 포함된 정렬 레이어만 비춘다.
        /// 템플릿 기본값은 Default뿐이라, 새로 만든 정렬 레이어의 스프라이트가 검게 나온다.
        /// </summary>
        static void ApplyLightsToAllSortingLayers()
        {
            var ids = new int[SortingLayer.layers.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = SortingLayer.layers[i].id;

            foreach (var light in Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsInactive.Include))
            {
                light.targetSortingLayers = ids;
                EditorUtility.SetDirty(light);
            }
        }

        static void BuildScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 다시 실행해도 중복되지 않도록 기존 오브젝트 제거
            foreach (var name in new[] { "Player", "Background" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = Vector3.zero;

            var bg = new GameObject("Background");
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            bgSr.drawMode = SpriteDrawMode.Tiled;
            bgSr.size = new Vector2(64f, 64f); // 타일(4유닛)의 짝수 배 → 스냅 시 패턴 정렬 유지
            bgSr.sortingLayerName = GameLayers.Sorting.Background;
            bg.transform.position = new Vector3(0f, 0f, 1f);
            bg.AddComponent<InfiniteBackground>();

            ApplyLightsToAllSortingLayers();

            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
                follow.Target = player.transform;
                follow.SnapToTarget();
                bg.GetComponent<InfiniteBackground>().FollowTarget = cam.transform;
                EditorUtility.SetDirty(follow);
            }
            else
            {
                Debug.LogWarning("[Samkuk] Main Camera를 찾지 못했습니다.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
