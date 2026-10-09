using Samkuk.Core;
using Samkuk.Data;
using Samkuk.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 9-2: 타이틀 씬(시작/영구 강화 상점/기록/저장 초기화/종료)을 만들고,
    /// 타이틀 → 게임 순서로 빌드 설정에 등록한다.
    /// </summary>
    public static class Step9TitleSetup
    {
        public const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        public const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
        const string MetaCatalogPath = "Assets/ScriptableObjects/MetaCatalog.asset";
        const string PlayFromTitleMenu = "Samkuk/Play From Title Scene";

        [MenuItem("Samkuk/Step 9-2 - Setup Title Scene")]
        public static void Run()
        {
            if (!System.IO.File.Exists(BattleScenePath))
            {
                Debug.LogError("[Samkuk] 전투 씬(BattleScene)이 없습니다.");
                return;
            }

            // 주의: NewScene 이후에 에셋을 로드한다 (씬 전환이 로드된 에셋 참조를 무효화할 수 있음).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<MetaCatalog>(MetaCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Samkuk] MetaCatalog 가 없습니다. Step 9-1 을 먼저 실행하세요.");
                return;
            }

            BuildCamera();
            BuildEventSystem();
            var controller = BuildCanvas(catalog);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
            RegisterBuildScenes();
            SetPlayFromTitle(true);

            var missing = controller.MissingReferences();
            if (missing.Count > 0)
                Debug.LogError($"[Samkuk] 타이틀 씬 참조 연결 실패: {string.Join(", ", missing)}");
            else
                Debug.Log("[Samkuk] Step 9-2 setup 완료 (Play 시 타이틀 씬에서 시작. 메뉴 Samkuk > Play From Title Scene 으로 끌 수 있음)");
        }

        // ───────────────────────── 빌드 설정 / 플레이 시작 씬 ─────────────────────────

        static void RegisterBuildScenes()
        {
            // 타이틀 0번, 게임 1번, (있으면) 내정 2번
            Step12StrategySetup.RegisterBuildScenes();
        }

        // 에디터를 다시 열거나 다른 셋업이 시작 씬을 바꿔도 Play 는 늘 타이틀에서 시작하게 한다.
        // (그렇지 않으면 마지막으로 열어 둔 씬, 예를 들어 내정 씬에서 곧장 시작해 마지막 성 화면이 뜬다.)
        // 메뉴에서 끄면 그 선택을 프로젝트별로 기억한다.
        const string PlayFromTitleOffKey = "samkuk.playFromTitle.off";

        static bool PlayFromTitleOptedOut => EditorUserSettings.GetConfigValue(PlayFromTitleOffKey) == "1";

        /// <summary>시작 씬을 다시 타이틀로 맞춘다 (맵 편집기를 바로 실행했다가 돌아올 때). 메뉴에서 꺼 둔 경우는 그대로 둔다.</summary>
        public static void RestorePlayFromTitle()
        {
            if (!PlayFromTitleOptedOut) SetPlayFromTitle(true);
            else EditorSceneManager.playModeStartScene = null;
        }

        [InitializeOnLoadMethod]
        static void EnsurePlayFromTitle()
        {
            EditorApplication.delayCall += () =>
            {
                if (PlayFromTitleOptedOut || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
                if (title != null && EditorSceneManager.playModeStartScene != title) EditorSceneManager.playModeStartScene = title;
            };
        }

        static void SetPlayFromTitle(bool on)
        {
            EditorSceneManager.playModeStartScene = on ? AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) : null;
        }

        [MenuItem(PlayFromTitleMenu)]
        static void TogglePlayFromTitle()
        {
            bool turnOn = EditorSceneManager.playModeStartScene == null;
            SetPlayFromTitle(turnOn);
            EditorUserSettings.SetConfigValue(PlayFromTitleOffKey, turnOn ? "0" : "1");
        }

        [MenuItem(PlayFromTitleMenu, true)]
        static bool TogglePlayFromTitleValidate()
        {
            Menu.SetChecked(PlayFromTitleMenu, EditorSceneManager.playModeStartScene != null);
            return true;
        }

        // ───────────────────────── 카메라 / 이벤트 시스템 ─────────────────────────

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

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ───────────────────────── UI ─────────────────────────

        static TitleController BuildCanvas(MetaCatalog catalog)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 배경
            var bg = NewRect("Background", canvasGo.transform);
            Stretch(bg);
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.07f, 0.12f, 1f);
            bgImage.raycastTarget = false;

            // 메인 패널
            var main = NewRect("MainPanel", canvasGo.transform);
            Stretch(main);

            var title = NewText("Title", main, font, 120, TextAnchor.MiddleCenter, "삼국지 서바이버");
            title.color = new Color(1f, 0.85f, 0.35f);
            Anchor(title.rectTransform, 0.5f, 0.8f, new Vector2(1600f, 170f));
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(4f, -4f);

            var subtitle = NewText("Subtitle", main, font, 40, TextAnchor.MiddleCenter, "황건적의 난");
            subtitle.color = new Color(0.85f, 0.75f, 0.7f);
            Anchor(subtitle.rectTransform, 0.5f, 0.69f, new Vector2(1000f, 60f));

            // 부제 아래 장식 구분선 (테마가 있을 때만)
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Resources/UiTheme.asset");
            if (theme != null && theme.divider != null)
            {
                var divider = NewRect("Divider", main);
                Anchor(divider, 0.5f, 0.635f, new Vector2(760f, 24f));
                var dividerImage = divider.gameObject.AddComponent<Image>();
                dividerImage.sprite = theme.divider;
                dividerImage.raycastTarget = false;
            }

            var gold = NewText("Gold", main, font, 38, TextAnchor.UpperRight, "보유 골드  0");
            gold.color = new Color(1f, 0.9f, 0.5f);
            gold.rectTransform.anchorMin = gold.rectTransform.anchorMax = new Vector2(1f, 1f);
            gold.rectTransform.pivot = new Vector2(1f, 1f);
            gold.rectTransform.sizeDelta = new Vector2(600f, 60f);
            gold.rectTransform.anchoredPosition = new Vector2(-40f, -30f);

            var start = NewButton("StartButton", main, font, "시작", 0.54f, new Vector2(480f, 88f), new Color(0.55f, 0.2f, 0.18f));
            var strategyBtn = NewButton("StrategyButton", main, font, "내정", 0.455f, new Vector2(480f, 80f));
            var shopBtn = NewButton("ShopButton", main, font, "영구 강화", 0.37f, new Vector2(480f, 80f));
            var recordsBtn = NewButton("RecordsButton", main, font, "기록", 0.285f, new Vector2(480f, 80f));
            var resetBtn = NewButton("ResetButton", main, font, "저장 초기화", 0.205f, new Vector2(480f, 66f), new Color(0.3f, 0.22f, 0.22f));
            var quitBtn = NewButton("QuitButton", main, font, "종료", 0.13f, new Vector2(480f, 66f));
            // 효과음 볼륨 버튼 (왼쪽 위): 누를 때마다 끔 → 작게 → 보통 → 크게
            var soundBtn = NewButton("SoundButton", main, font, "효과음: 보통", 1f, new Vector2(360f, 70f), new Color(0.22f, 0.28f, 0.4f));
            var soundRt = (RectTransform)soundBtn.transform;
            soundRt.anchorMin = soundRt.anchorMax = new Vector2(0f, 1f);
            soundRt.pivot = new Vector2(0f, 1f);
            soundRt.anchoredPosition = new Vector2(40f, -30f);
            var soundLabel = soundBtn.GetComponentInChildren<Text>();
            soundLabel.fontSize = 30;

            // 화면 흔들림 켜기/끄기 버튼 (효과음 버튼 아래)
            var shakeBtn = NewButton("ShakeButton", main, font, "화면 흔들림: 켬", 1f, new Vector2(360f, 70f), new Color(0.22f, 0.28f, 0.4f));
            var shakeRt = (RectTransform)shakeBtn.transform;
            shakeRt.anchorMin = shakeRt.anchorMax = new Vector2(0f, 1f);
            shakeRt.pivot = new Vector2(0f, 1f);
            shakeRt.anchoredPosition = new Vector2(40f, -110f);
            var shakeLabel = shakeBtn.GetComponentInChildren<Text>();
            shakeLabel.fontSize = 30;

            // 해상도 / 창 모드 버튼 (화면 흔들림 버튼 아래)
            var resolutionBtn = NewButton("ResolutionButton", main, font, "해상도: 자동", 1f, new Vector2(360f, 70f), new Color(0.22f, 0.28f, 0.4f));
            var resRt = (RectTransform)resolutionBtn.transform;
            resRt.anchorMin = resRt.anchorMax = new Vector2(0f, 1f);
            resRt.pivot = new Vector2(0f, 1f);
            resRt.anchoredPosition = new Vector2(40f, -190f);
            var resolutionLabel = resolutionBtn.GetComponentInChildren<Text>();
            resolutionLabel.fontSize = 30;

            var windowModeBtn = NewButton("WindowModeButton", main, font, "화면: 전체화면", 1f, new Vector2(360f, 70f), new Color(0.22f, 0.28f, 0.4f));
            var modeRt = (RectTransform)windowModeBtn.transform;
            modeRt.anchorMin = modeRt.anchorMax = new Vector2(0f, 1f);
            modeRt.pivot = new Vector2(0f, 1f);
            modeRt.anchoredPosition = new Vector2(40f, -270f);
            var windowModeLabel = windowModeBtn.GetComponentInChildren<Text>();
            windowModeLabel.fontSize = 30;

            // HD-2D 조명 켜기/끄기 버튼 (창 모드 버튼 아래)
            var lightingBtn = NewButton("LightingButton", main, font, "조명 연출: 켬", 1f, new Vector2(360f, 70f), new Color(0.22f, 0.28f, 0.4f));
            var lightRt = (RectTransform)lightingBtn.transform;
            lightRt.anchorMin = lightRt.anchorMax = new Vector2(0f, 1f);
            lightRt.pivot = new Vector2(0f, 1f);
            lightRt.anchoredPosition = new Vector2(40f, -350f);
            var lightingLabel = lightingBtn.GetComponentInChildren<Text>();
            lightingLabel.fontSize = 30;

            // HD-2D 후처리 켜기/끄기 버튼 (조명 버튼 아래)
            var postFxBtn = NewButton("PostFxButton", main, font, "화면 효과: 켬", 1f, new Vector2(360f, 70f), new Color(0.22f, 0.28f, 0.4f));
            var postRt = (RectTransform)postFxBtn.transform;
            postRt.anchorMin = postRt.anchorMax = new Vector2(0f, 1f);
            postRt.pivot = new Vector2(0f, 1f);
            postRt.anchoredPosition = new Vector2(40f, -430f);
            var postFxLabel = postFxBtn.GetComponentInChildren<Text>();
            postFxLabel.fontSize = 30;
            var resetLabel = resetBtn.GetComponentInChildren<Text>();
            resetLabel.fontSize = 28;
            quitBtn.GetComponentInChildren<Text>().fontSize = 28;

            // 상점 패널
            var shopPanel = NewRect("ShopPanel", canvasGo.transform);
            Stretch(shopPanel);
            shopPanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            var shopBox = NewRect("Box", shopPanel);
            shopBox.anchorMin = shopBox.anchorMax = new Vector2(0.5f, 0.5f);
            shopBox.pivot = new Vector2(0.5f, 0.5f);
            shopBox.sizeDelta = new Vector2(1100f, 1020f);
            shopBox.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.18f, 0.98f);

            var shopTitle = NewText("Title", shopBox, font, 60, TextAnchor.MiddleCenter, "영구 강화");
            SetTop(shopTitle.rectTransform, 16f, 80f);
            var shopGold = NewText("Gold", shopBox, font, 36, TextAnchor.MiddleCenter, "보유 골드  0");
            shopGold.color = new Color(1f, 0.9f, 0.5f);
            SetTop(shopGold.rectTransform, 96f, 50f);

            var container = NewRect("Rows", shopBox);
            container.anchorMin = new Vector2(0f, 0f);
            container.anchorMax = new Vector2(1f, 1f);
            container.offsetMin = new Vector2(30f, 120f);
            container.offsetMax = new Vector2(-30f, -160f);
            var layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var shopClose = NewButton("CloseButton", shopBox, font, "닫기  [ESC]", 0f, new Vector2(360f, 70f), pixelY: 55f);

            var shopUi = shopBox.gameObject.AddComponent<MetaShopUI>();
            var shopUiSo = new SerializedObject(shopUi);
            shopUiSo.FindProperty("container").objectReferenceValue = container;
            shopUiSo.FindProperty("goldLabel").objectReferenceValue = shopGold;
            shopUiSo.ApplyModifiedPropertiesWithoutUndo();
            shopPanel.gameObject.SetActive(false);

            // 기록 패널
            var recordsPanel = NewRect("RecordsPanel", canvasGo.transform);
            Stretch(recordsPanel);
            recordsPanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            var recBox = NewRect("Box", recordsPanel);
            recBox.anchorMin = recBox.anchorMax = new Vector2(0.5f, 0.5f);
            recBox.pivot = new Vector2(0.5f, 0.5f);
            recBox.sizeDelta = new Vector2(900f, 720f);
            recBox.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.18f, 0.98f);

            var recTitle = NewText("Title", recBox, font, 60, TextAnchor.MiddleCenter, "기록");
            SetTop(recTitle.rectTransform, 16f, 80f);
            var recText = NewText("Text", recBox, font, 34, TextAnchor.UpperLeft, "");
            recText.rectTransform.anchorMin = Vector2.zero;
            recText.rectTransform.anchorMax = Vector2.one;
            recText.rectTransform.offsetMin = new Vector2(90f, 130f);
            recText.rectTransform.offsetMax = new Vector2(-60f, -120f);
            recText.lineSpacing = 1.25f;
            var recClose = NewButton("CloseButton", recBox, font, "닫기  [ESC]", 0f, new Vector2(360f, 70f), pixelY: 55f);
            recordsPanel.gameObject.SetActive(false);

            // 컨트롤러 연결
            canvasGo.AddComponent<UiSkin>(); // 중국풍 테마 (UiTheme.asset 이 없으면 아무것도 바꾸지 않음)
            var controller = canvasGo.AddComponent<TitleController>();
            var so = new SerializedObject(controller);
            so.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
            so.FindProperty("shopPanel").objectReferenceValue = shopPanel.gameObject;
            so.FindProperty("recordsPanel").objectReferenceValue = recordsPanel.gameObject;
            so.FindProperty("startButton").objectReferenceValue = start;
            so.FindProperty("strategyButton").objectReferenceValue = strategyBtn;
            so.FindProperty("shopButton").objectReferenceValue = shopBtn;
            so.FindProperty("recordsButton").objectReferenceValue = recordsBtn;
            so.FindProperty("resetButton").objectReferenceValue = resetBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("shopCloseButton").objectReferenceValue = shopClose;
            so.FindProperty("recordsCloseButton").objectReferenceValue = recClose;
            so.FindProperty("shopUi").objectReferenceValue = shopUi;
            so.FindProperty("recordsText").objectReferenceValue = recText;
            so.FindProperty("goldLabel").objectReferenceValue = gold;
            so.FindProperty("resetLabel").objectReferenceValue = resetLabel;
            so.FindProperty("soundButton").objectReferenceValue = soundBtn;
            so.FindProperty("soundLabel").objectReferenceValue = soundLabel;
            so.FindProperty("shakeButton").objectReferenceValue = shakeBtn;
            so.FindProperty("shakeLabel").objectReferenceValue = shakeLabel;
            so.FindProperty("resolutionButton").objectReferenceValue = resolutionBtn;
            so.FindProperty("resolutionLabel").objectReferenceValue = resolutionLabel;
            so.FindProperty("windowModeButton").objectReferenceValue = windowModeBtn;
            so.FindProperty("windowModeLabel").objectReferenceValue = windowModeLabel;
            so.FindProperty("lightingButton").objectReferenceValue = lightingBtn;
            so.FindProperty("lightingLabel").objectReferenceValue = lightingLabel;
            so.FindProperty("postFxButton").objectReferenceValue = postFxBtn;
            so.FindProperty("postFxLabel").objectReferenceValue = postFxLabel;
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        static Button NewButton(string name, RectTransform parent, Font font, string label, float anchorY,
            Vector2 size, Color? color = null, float pixelY = 0f)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, pixelY);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = color ?? new Color(0.25f, 0.3f, 0.45f, 1f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var text = NewText("Label", rt, font, 38, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform);
            return btn;
        }

        static void Anchor(RectTransform rt, float x, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        static void SetTop(RectTransform rt, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor, string value)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = value;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
