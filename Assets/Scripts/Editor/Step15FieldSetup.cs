using System.IO;
using Samkuk.Data;
using Samkuk.Field;
using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 15: 야전(남코 삼국지2식 부대 전투) 테스트 씬 만들기. 화면은 <see cref="FieldBattleController"/> 가 실행 중에 코드로 만들므로
    /// 씬에는 고정 카메라(도트 규격), 전역 조명, 이벤트 시스템, 컨트롤러(+장수 목록·바닥 타일·부대 시트 참조)만 둔다.
    /// 그림은 <c>tools/field_art/generate.ps1</c> 이 만든 <c>Assets/Sprites/Field</c>(없으면 코드 색 덩어리로 대신 동작).
    /// 타이틀 왼쪽 아래 [야전 테스트] 버튼(에디터/개발 빌드)이나 메뉴 <c>Samkuk &gt; Play Field Battle</c> 로 연다. 설명은 docs/FIELD_BATTLE.md.
    /// </summary>
    public static class Step15FieldSetup
    {
        public const string ScenePath = "Assets/Scenes/FieldBattleScene.unity";
        const string HeroCatalogPath = "Assets/ScriptableObjects/HeroCatalog.asset";
        const string PlayingKey = "samkuk.fieldBattle.playing";

        [MenuItem("Samkuk/Step 15 - Field Battle Scene (야전 테스트)")]
        public static void Run()
        {
            AssetDatabase.Refresh();

            // 주의: NewScene 이후에 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var heroes = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HeroCatalogPath);
            if (heroes == null) Debug.LogWarning("[Samkuk] HeroCatalog 가 없습니다 (Step 8). 장수 이름 없이 '주인공'으로 동작합니다.");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Samkuk.Core.PixelArt.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.08f);
            camGo.transform.position = new Vector3(0f, FieldBattleController.CameraY, -10f);
            camGo.AddComponent<AudioListener>();
            // 도트 규격 카메라 (전투 씬과 같음: 켜면 반높이 5.625, 끄면 6 — 둘 다 전장이 들어온다)
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            BattlePixelCamera.Configure(ppc);
            camGo.AddComponent<BattlePixelCamera>();

            // URP 2D 는 조명이 비추는 정렬 레이어의 스프라이트만 제대로 보여 준다
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var layerIds = new int[SortingLayer.layers.Length];
            for (int i = 0; i < layerIds.Length; i++) layerIds[i] = SortingLayer.layers[i].id;
            light.targetSortingLayers = layerIds;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var go = new GameObject("FieldBattle");
            var controller = go.AddComponent<FieldBattleController>();
            var so = new SerializedObject(controller);
            so.FindProperty("heroCatalog").objectReferenceValue = heroes;

            // 바닥 타일: CastleTerrain 순서. 부대 시트: FieldBattleController.SheetIndex 순서
            var tiles = so.FindProperty("tileSprites");
            var terrains = System.Enum.GetNames(typeof(CastleTerrain));
            tiles.arraySize = terrains.Length;
            int missing = 0;
            for (int i = 0; i < terrains.Length; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FieldArtSpec.Dir}/{FieldArtSpec.TileFile(terrains[i])}.png");
                if (sprite == null) missing++;
                tiles.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
            }
            var sheets = so.FindProperty("unitSheets");
            sheets.arraySize = 8;
            foreach (FieldSide side in new[] { FieldSide.Player, FieldSide.Enemy })
            {
                for (int c = 0; c < 4; c++)
                {
                    bool general = c == 3;
                    var cls = general ? UnitClass.Cavalry : (UnitClass)c;
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FieldArtSpec.Dir}/{FieldArtSpec.UnitFile(cls, general, side)}.png");
                    if (tex == null) missing++;
                    sheets.GetArrayElementAtIndex(FieldBattleController.SheetIndex(cls, general, side)).objectReferenceValue = tex;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if (missing > 0)
                Debug.LogWarning($"[Samkuk] 야전 그림 {missing}장이 없습니다. tools/field_art/generate.ps1 을 실행한 뒤 Step 15 를 다시 돌리세요 (없어도 색 덩어리로 동작).");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Step12StrategySetup.RegisterBuildScenes();
            Debug.Log("[Samkuk] Step 15 완료: FieldBattleScene 생성 (메뉴 Samkuk > Play Field Battle, 또는 타이틀 왼쪽 아래 [야전 테스트] 버튼. 설명은 docs/FIELD_BATTLE.md)");
        }

        // ───────────────────────── 바로 실행 ─────────────────────────

        /// <summary>야전 씬에서 곧장 플레이한다 (끝나면 시작 씬은 타이틀로 돌아간다).</summary>
        [MenuItem("Samkuk/Play Field Battle (야전 테스트 실행)")]
        public static void PlayFieldBattle()
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
