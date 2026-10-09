using System.Collections.Generic;
using System.IO;
using Samkuk.Data;
using Samkuk.Strategy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 12-2: 내정 화면(전략 지도 + 성 화면) 씬 만들기. 화면 자체는 <see cref="StrategyUI"/> 가 실행 중에 코드로 만들므로
    /// 씬에는 카메라, 이벤트 시스템, 캔버스(+StrategyUI)만 둔다. 성 에셋(12-1)과 지도 그림
    /// (<c>tools/castle_art/generate.ps1 -Only Map</c>)이 필요하다. 타이틀의 [내정] 버튼은 Step 9-2 가 만든다.
    /// </summary>
    public static class Step12StrategySetup
    {
        public const string ScenePath = "Assets/Scenes/StrategyScene.unity";
        public const string MapDir = "Assets/Sprites/Strategy";
        const string MapPath = MapDir + "/StrategyMap.png";
        const string CatalogPath = "Assets/ScriptableObjects/CastleCatalog.asset";

        [MenuItem("Samkuk/Step 12-2 - Strategy Scene (전략 지도 + 성 화면)")]
        public static void Run()
        {
            if (!File.Exists(CatalogPath)) Step12CastleSetup.Run(); // 성 에셋이 없으면 먼저 만든다
            AssetDatabase.Refresh();                                 // 새 지도 PNG 를 스프라이트로 가져온다

            // 주의: NewScene 이후에 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<CastleCatalog>(CatalogPath);
            var map = AssetDatabase.LoadAssetAtPath<Sprite>(MapPath);
            if (catalog == null)
            {
                Debug.LogError("[Samkuk] CastleCatalog 가 없습니다. Step 12-1 을 먼저 실행하세요.");
                return;
            }
            if (map == null)
                Debug.LogWarning($"[Samkuk] 전략 지도 그림이 없습니다 ({MapPath}). tools/castle_art/generate.ps1 -Only Map 후 다시 실행하세요. (그림 없이도 동작은 합니다)");

            BuildCamera();
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var ui = BuildCanvas(catalog, map);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterBuildScenes();

            var missing = ui.MissingReferences();
            if (missing.Count > 0)
                Debug.LogError($"[Samkuk] 내정 씬 참조 연결 실패: {string.Join(", ", missing)}");
            else
                Debug.Log("[Samkuk] Step 12-2 완료: StrategyScene 생성 (타이틀의 [내정] 버튼으로 진입. 타이틀 버튼은 Step 9-2 를 다시 실행해야 생깁니다)");
        }

        /// <summary>타이틀(0) → 게임(1) → 내정(2) → 맵 편집기(3) 순서로 빌드 설정에 등록한다. 없는 씬은 건너뛴다.</summary>
        public static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in new[] { Step9TitleSetup.TitleScenePath, Step9TitleSetup.BattleScenePath, ScenePath, Step13MapEditorSetup.ScenePath })
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.09f);
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
        }

        static StrategyUI BuildCanvas(CastleCatalog catalog, Sprite map)
        {
            var canvasGo = new GameObject("StrategyCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = StrategyUI.ReferenceResolution; // 내정은 지도/명령 공간 때문에 2560x1440 기준
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // 비율이 달라도 내정 화면 전체가 보이게

            var ui = canvasGo.AddComponent<StrategyUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("mapSprite").objectReferenceValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }
    }
}
