using System.IO;
using Samkuk.Core;
using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 14-4: 월드 정렬과 드리운 그림자의 프로젝트 쪽 준비.
    ///  ① 정렬 레이어 "World" 를 Pickup 과 Enemy 사이에 추가(TagManager) — 캐릭터와 서 있는 소품이 쓴다
    ///  ② 2D 렌더러(Renderer2D.asset)의 투명 정렬을 커스텀 축 (0, 1, 0) 으로 — 같은 레이어 안에서 발 위치(y)로 앞뒤 결정
    ///  ③ Player/Enemy 프리팹의 몸 렌더러를 World 레이어 + 피벗 기준점으로 (실행 중에도 코드가 맞추지만 프리팹을 맞춰 두면 씬 뷰에서도 같다)
    ///  ④ 전투/맵 편집기 씬의 Light2D 가 새 레이어도 비추게
    /// 서 있는 소품 플래그는 Step 12-6 이 채운다. 멱등.
    /// </summary>
    public static class Step14WorldSetup
    {
        const string TagManagerPath = "ProjectSettings/TagManager.asset";
        const string Renderer2DPath = "Assets/Settings/Renderer2D.asset";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        static readonly string[] ScenePaths = { "Assets/Scenes/BattleScene.unity", "Assets/Scenes/MapEditorScene.unity" };

        [MenuItem("Samkuk/Step 14-4 - World Sorting (월드 정렬·그림자)")]
        public static void Run()
        {
            bool layerAdded = AddWorldSortingLayer();
            bool rendererChanged = SetRendererSortAxis();
            int prefabs = ConfigurePrefab(PlayerPrefabPath) + ConfigurePrefab(EnemyPrefabPath);
            int lights = 0;
            foreach (var path in ScenePaths)
                if (File.Exists(path)) lights += ApplyLightsToAllLayers(path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Samkuk] Step 14-4 완료: World 정렬 레이어 {(layerAdded ? "추가" : "이미 있음")}, 정렬 축 {(rendererChanged ? "커스텀 (0,1,0) 으로" : "이미 설정")}, 프리팹 {prefabs}개, 조명 {lights}개 갱신");
        }

        /// <summary>"World" 정렬 레이어가 없으면 Pickup 다음에 넣는다. 돌려주는 값: 새로 넣었는가.</summary>
        public static bool AddWorldSortingLayer()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0) { Debug.LogWarning("[Samkuk] TagManager.asset 을 열지 못했습니다."); return false; }
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("m_SortingLayers");
            int insertAt = layers.arraySize, maxId = 0;
            for (int i = 0; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                string name = el.FindPropertyRelative("name").stringValue;
                maxId = Mathf.Max(maxId, el.FindPropertyRelative("uniqueID").intValue);
                if (name == GameLayers.Sorting.World) return false;
                if (name == GameLayers.Sorting.Pickup) insertAt = i + 1;
            }
            layers.InsertArrayElementAtIndex(insertAt);
            var added = layers.GetArrayElementAtIndex(insertAt);
            added.FindPropertyRelative("name").stringValue = GameLayers.Sorting.World;
            added.FindPropertyRelative("uniqueID").intValue = maxId + 1;
            added.FindPropertyRelative("locked").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>2D 렌더러의 투명 정렬을 커스텀 축 (0, 1, 0) 으로. 돌려주는 값: 바뀌었는가.</summary>
        public static bool SetRendererSortAxis()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Renderer2DPath);
            if (data == null) { Debug.LogWarning($"[Samkuk] {Renderer2DPath} 를 찾지 못했습니다."); return false; }
            var so = new SerializedObject(data);
            var mode = so.FindProperty("m_TransparencySortMode");
            var axis = so.FindProperty("m_TransparencySortAxis");
            bool changed = mode.intValue != (int)TransparencySortMode.CustomAxis || axis.vector3Value != Vector3.up;
            mode.intValue = (int)TransparencySortMode.CustomAxis;
            axis.vector3Value = Vector3.up;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return changed;
        }

        static int ConfigurePrefab(string path)
        {
            if (!File.Exists(path)) return 0;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sr = root.GetComponentInChildren<SpriteRenderer>();
                if (sr == null) return 0;
                WorldSorting.Configure(sr);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return 1;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static int ApplyLightsToAllLayers(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var ids = new int[SortingLayer.layers.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = SortingLayer.layers[i].id;
            int count = 0;
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            {
                light.targetSortingLayers = ids;
                EditorUtility.SetDirty(light);
                count++;
            }
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return count;
        }
    }
}
