using System.IO;
using Samkuk.Data;
using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 13: 맵 편집기(맵툴) 씬 만들기. 화면은 <see cref="MapEditorController"/> 가 실행 중에 코드로 만들므로
    /// 씬에는 카메라, 이벤트 시스템, 컨트롤러(+성 목록 연결)만 둔다. 성 에셋(12-1)과 지형 테마(12-6)가 필요하다.
    /// 타이틀 왼쪽 아래의 [맵 편집기] 버튼(에디터/개발 빌드)이나 메뉴 <c>Samkuk &gt; Play Map Editor</c> 로 연다.
    /// </summary>
    public static class Step13MapEditorSetup
    {
        public const string ScenePath = "Assets/Scenes/MapEditorScene.unity";
        const string CatalogPath = "Assets/ScriptableObjects/CastleCatalog.asset";
        const string PlayingKey = "samkuk.mapEditor.playing";

        [MenuItem("Samkuk/Step 13 - Map Editor Scene (맵 편집기)")]
        public static void Run()
        {
            if (!File.Exists(CatalogPath)) Step12CastleSetup.Run(); // 성 에셋이 없으면 먼저 만든다
            AssetDatabase.Refresh();

            // 주의: NewScene 이후에 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<CastleCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Samkuk] CastleCatalog 가 없습니다. Step 12-1 을 먼저 실행하세요.");
                return;
            }
            if (Resources.Load<TerrainThemeCatalog>(TerrainThemeCatalog.ResourceName) == null)
                Debug.LogWarning("[Samkuk] 지형 테마(Resources/TerrainThemeCatalog)가 없습니다. 메뉴 Step 12-6 을 먼저 실행하세요. (없으면 편집기는 빈 화면입니다)");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.09f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            // URP 2D 는 조명이 비추는 정렬 레이어의 스프라이트만 제대로 보여 준다 (전투 씬의 전역 조명과 같게)
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 1f;
            var layerIds = new int[SortingLayer.layers.Length];
            for (int i = 0; i < layerIds.Length; i++) layerIds[i] = SortingLayer.layers[i].id;
            light.targetSortingLayers = layerIds;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var editor = new GameObject("MapEditor");
            var controller = editor.AddComponent<MapEditorController>();
            var so = new SerializedObject(controller);
            so.FindProperty("castleCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Step12StrategySetup.RegisterBuildScenes();
            Debug.Log("[Samkuk] Step 13 완료: MapEditorScene 생성 (메뉴 Samkuk > Play Map Editor, 또는 타이틀 왼쪽 아래 [맵 편집기] 버튼. 설명은 docs/MAP_EDITOR.md)");
        }

        // ───────────────────────── 바로 실행 ─────────────────────────

        /// <summary>맵 편집기 씬에서 곧장 플레이한다 (끝나면 시작 씬은 타이틀로 돌아간다).</summary>
        [MenuItem("Samkuk/Play Map Editor (맵 편집기 실행)")]
        public static void PlayMapEditor()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(ScenePath)) Run();
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene == null) return;

            SessionState.SetBool(PlayingKey, true);
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void RestoreStartSceneAfterPlay()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PlayingKey, false)) return;
                SessionState.SetBool(PlayingKey, false);
                Step9TitleSetup.RestorePlayFromTitle();
            };
        }
    }
}
