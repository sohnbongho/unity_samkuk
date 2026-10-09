using System;
using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 타이틀 화면: 시작 / 영구 강화 상점 / 기록 / 저장 초기화(2단계 확인) / 종료.
    /// ESC로 하위 화면(상점, 기록)에서 메인으로 돌아간다.
    /// </summary>
    public class TitleController : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject shopPanel;
        [SerializeField] GameObject recordsPanel;

        [Header("메인 버튼")]
        [SerializeField] Button startButton;
        [SerializeField, Tooltip("내정(전략 지도) 화면으로. 없어도 동작(Step 9-2 를 다시 실행하면 생김)")] Button strategyButton;
        [SerializeField] Button shopButton;
        [SerializeField] Button recordsButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button quitButton;
        [SerializeField] Button soundButton;
        [SerializeField] Button shakeButton;
        [SerializeField, Tooltip("화면 해상도 순환 (없어도 동작: Step 9-2 를 다시 실행하면 생김)")] Button resolutionButton;
        [SerializeField, Tooltip("창 모드/전체화면 순환 (없어도 동작)")] Button windowModeButton;
        [SerializeField, Tooltip("HD-2D 조명 켜기/끄기 (없어도 동작: Step 9-2 를 다시 실행하면 생김)")] Button lightingButton;
        [SerializeField, Tooltip("HD-2D 후처리 켜기/끄기 (없어도 동작)")] Button postFxButton;

        [Header("하위 화면")]
        [SerializeField] Button shopCloseButton;
        [SerializeField] Button recordsCloseButton;
        [SerializeField] MetaShopUI shopUi;
        [SerializeField] Text recordsText;

        [Header("표시")]
        [SerializeField] Text goldLabel;
        [SerializeField] Text resetLabel;
        [SerializeField] Text soundLabel;
        [SerializeField] Text shakeLabel;
        [SerializeField] Text resolutionLabel;
        [SerializeField] Text windowModeLabel;
        [SerializeField] Text lightingLabel;
        [SerializeField] Text postFxLabel;
        [SerializeField] MetaCatalog catalog;
        [SerializeField, Tooltip("저장 초기화 확인 대기 시간(초)")] float resetConfirmSeconds = 3f;

        const string ResetIdleText = "저장 초기화";
        const string ResetArmedText = "정말 초기화? 한 번 더 클릭";

        float resetArmedLeft;
        MetaShop shop;

        /// <summary>씬 로드 방법 (테스트에서 대체 가능).</summary>
        public Action<string> SceneLoader { get; set; } = name => SceneManager.LoadScene(name);
        /// <summary>종료 동작 (테스트에서 대체 가능).</summary>
        public Action QuitAction { get; set; } = DefaultQuit;

        public bool ResetArmed => resetArmedLeft > 0f;
        public float ResetConfirmSeconds { get => resetConfirmSeconds; set => resetConfirmSeconds = value; }
        public MetaShop Shop => shop;
        public bool IsMainVisible => mainPanel != null && mainPanel.activeSelf;
        public bool IsShopVisible => shopPanel != null && shopPanel.activeSelf;
        public bool IsRecordsVisible => recordsPanel != null && recordsPanel.activeSelf;

        void Awake()
        {
            Time.timeScale = 1f;
            UiFont.Apply(gameObject);

            Bind(startButton, StartGame);
            Bind(strategyButton, OpenStrategy);
            Bind(shopButton, OpenShop);
            Bind(recordsButton, OpenRecords);
            Bind(resetButton, OnResetClicked);
            Bind(quitButton, Quit);
            Bind(soundButton, CycleSfxVolume);
            Bind(shakeButton, ToggleScreenShake);
            Bind(resolutionButton, CycleResolution);
            Bind(windowModeButton, CycleWindowMode);
            Bind(lightingButton, ToggleLighting);
            Bind(postFxButton, TogglePostFx);
            Bind(shopCloseButton, ShowMain);
            Bind(recordsCloseButton, ShowMain);

            shop = new MetaShop(catalog);
            shop.Changed += RefreshGold;
            if (shopUi != null) shopUi.Bind(shop);
        }

        void Start()
        {
            DisplaySettings.ApplyOnce(SaveSystem.Current); // 저장된 해상도/창 모드를 실행 후 처음 한 번만 적용
            ShowMain();
            RefreshSoundLabel();
            RefreshShakeLabel();
            RefreshDisplayLabels();
            RefreshLightingLabel();
            CreateVersionLabel();
        }

        void OnDestroy()
        {
            if (shop != null) shop.Changed -= RefreshGold;
        }

        /// <summary>화면 오른쪽 아래에 버전을 작게 보여 준다 (친구가 "어느 빌드"인지 알려 줄 수 있게). 씬을 다시 만들 필요 없이 코드로 만든다.</summary>
        void CreateVersionLabel()
        {
            if (transform.Find("VersionLabel") != null) return;

            var go = new GameObject("VersionLabel", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-30f, 20f);
            rt.sizeDelta = new Vector2(500f, 44f);

            var label = go.GetComponent<Text>();
            label.font = UiFont.Get();
            label.fontSize = 26;
            label.alignment = TextAnchor.LowerRight;
            label.color = new Color(1f, 1f, 1f, 0.55f);
            label.raycastTarget = false;
            label.text = VersionText();
        }

        /// <summary>"v1.0" 처럼 버전 문구. 개발용 빌드(Development Build)면 (dev) 를 붙인다.</summary>
        public static string VersionText() => $"v{Application.version}" + (Debug.isDebugBuild && !Application.isEditor ? " (dev)" : "");

        /// <summary>버튼에 동작을 연결한다. 누를 때마다 클릭음이 난다.</summary>
        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                action();
            });
        }

        void Update()
        {
            // 저장 초기화 확인 대기 (시간이 지나면 해제)
            if (resetArmedLeft > 0f)
            {
                resetArmedLeft -= Time.unscaledDeltaTime;
                if (resetArmedLeft <= 0f) DisarmReset();
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !IsMainVisible) ShowMain();
        }

        // ───────────────────────── 화면 전환 ─────────────────────────

        public void ShowMain()
        {
            SetActive(mainPanel, true);
            SetActive(shopPanel, false);
            SetActive(recordsPanel, false);
            DisarmReset();
            RefreshGold();
        }

        public void OpenShop()
        {
            SetActive(mainPanel, false);
            SetActive(shopPanel, true);
            shop.Refresh();
            if (shopUi != null) shopUi.Refresh();
        }

        public void OpenRecords()
        {
            SetActive(mainPanel, false);
            SetActive(recordsPanel, true);
            if (recordsText != null) recordsText.text = RecordsFormatter.Format(SaveSystem.Current);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        void RefreshGold()
        {
            if (goldLabel != null) goldLabel.text = $"보유 골드  {SaveSystem.Current.gold}";
        }

        // ───────────────────────── 동작 ─────────────────────────

        public void StartGame()
        {
            GameSession.SortieCastle = null; // 타이틀의 [시작]은 성 없이 시작하는 판
            GameSession.SortieOrigin = null;
            GameSession.MapTest = false;
            SceneLoader?.Invoke(GameManager.BattleSceneName);
        }

        /// <summary>내정 모드(전략 지도)로 간다.</summary>
        public void OpenStrategy() => SceneLoader?.Invoke(GameManager.StrategySceneName);

        public void Quit() => QuitAction?.Invoke();

        /// <summary>첫 클릭은 확인 대기, 대기 시간 안의 두 번째 클릭이 실제로 저장을 지운다.</summary>
        public void OnResetClicked()
        {
            if (!ResetArmed)
            {
                resetArmedLeft = Mathf.Max(0.01f, resetConfirmSeconds);
                if (resetLabel != null) resetLabel.text = ResetArmedText;
                return;
            }

            SaveSystem.Delete();
            DisarmReset();
            shop = new MetaShop(catalog);
            shop.Changed += RefreshGold;
            if (shopUi != null) shopUi.Bind(shop);
            RefreshGold();
            RefreshSoundLabel();
            RefreshShakeLabel();
            DisplaySettings.Apply(SaveSystem.Current); // 저장 초기화 = 화면 설정도 기본값으로
            RefreshDisplayLabels();
        }

        void DisarmReset()
        {
            resetArmedLeft = 0f;
            if (resetLabel != null) resetLabel.text = ResetIdleText;
        }

        /// <summary>효과음 볼륨을 다음 단계(끔 → 작게 → 보통 → 크게)로 바꾸고 저장한다.</summary>
        public void CycleSfxVolume()
        {
            var save = SaveSystem.Current;
            save.sfxVolume = AudioManager.NextVolumeStep(save.sfxVolume);
            SaveSystem.SaveCurrent();

            var manager = AudioManager.Instance;
            if (manager != null) manager.SetSfxVolume(save.sfxVolume);

            RefreshSoundLabel();
            AudioManager.Play(SfxId.Click); // 바뀐 볼륨으로 들려준다 (끔이면 소리 없음)
        }

        /// <summary>화면 흔들림을 켜고 끈다 (멀미가 나는 사람을 위한 설정). 저장된다.</summary>
        public void ToggleScreenShake()
        {
            var save = SaveSystem.Current;
            save.screenShake = !save.screenShake;
            SaveSystem.SaveCurrent();
            RefreshShakeLabel();
        }

        /// <summary>HD-2D 조명(전역광 색조, 소품 점광원, 플레이어 빛)을 켜고 끈다. 저장된다.</summary>
        public void ToggleLighting()
        {
            var save = SaveSystem.Current;
            save.hd2dLighting = !save.hd2dLighting;
            SaveSystem.SaveCurrent();
            RefreshLightingLabel();
            RefreshPostFxLabel();
        }

        void RefreshLightingLabel()
        {
            if (lightingLabel != null) lightingLabel.text = $"조명 연출: {(SaveSystem.Current.hd2dLighting ? "켬" : "끔")}";
        }

        /// <summary>HD-2D 후처리(블룸, 비네트, 색 보정)를 켜고 끈다. 저장된다.</summary>
        public void TogglePostFx()
        {
            var save = SaveSystem.Current;
            save.hd2dPostFx = !save.hd2dPostFx;
            SaveSystem.SaveCurrent();
            RefreshPostFxLabel();
        }

        void RefreshPostFxLabel()
        {
            if (postFxLabel != null) postFxLabel.text = $"화면 효과: {(SaveSystem.Current.hd2dPostFx ? "켬" : "끔")}";
        }

        /// <summary>모니터(바탕화면) 크기 (테스트에서 대체 가능). 이보다 큰 해상도는 고를 수 없다.</summary>
        public Func<Vector2Int> DesktopSize { get; set; } = () => new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);

        /// <summary>해상도를 다음 프리셋으로 바꾸고 저장/적용한다 (테두리 없는 전체화면에서는 의미가 없어 무시).</summary>
        public void CycleResolution()
        {
            var save = SaveSystem.Current;
            if (!DisplaySettings.CanChooseResolution(save)) return;

            var desktop = DesktopSize();
            var next = DisplaySettings.NextResolution(save, desktop.x, desktop.y);
            save.displayWidth = next.x;
            save.displayHeight = next.y;
            SaveSystem.SaveCurrent();
            DisplaySettings.Apply(save);
            RefreshDisplayLabels();
        }

        /// <summary>창 모드 / 전체화면 / 전용 전체화면 순환.</summary>
        public void CycleWindowMode()
        {
            var save = SaveSystem.Current;
            save.windowMode = DisplaySettings.NextMode(save.windowMode);
            SaveSystem.SaveCurrent();
            DisplaySettings.Apply(save);
            RefreshDisplayLabels();
        }

        void RefreshDisplayLabels()
        {
            var save = SaveSystem.Current;
            if (resolutionLabel != null) resolutionLabel.text = DisplaySettings.ResolutionLabel(save);
            if (resolutionButton != null) resolutionButton.interactable = DisplaySettings.CanChooseResolution(save);
            if (windowModeLabel != null) windowModeLabel.text = $"화면: {DisplaySettings.ModeName(save.windowMode)}";
        }

        void RefreshShakeLabel()
        {
            if (shakeLabel != null) shakeLabel.text = $"화면 흔들림: {(SaveSystem.Current.screenShake ? "켬" : "끔")}";
        }

        void RefreshSoundLabel()
        {
            if (soundLabel != null) soundLabel.text = $"효과음: {AudioManager.VolumeName(SaveSystem.Current.sfxVolume)}";
        }

        static void DefaultQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>연결되지 않은 필수 참조의 이름 목록 (셋업 검증/테스트용).</summary>
        public List<string> MissingReferences()
        {
            var missing = new List<string>();
            void Check(UnityEngine.Object o, string name) { if (o == null) missing.Add(name); }

            Check(mainPanel, nameof(mainPanel));
            Check(shopPanel, nameof(shopPanel));
            Check(recordsPanel, nameof(recordsPanel));
            Check(startButton, nameof(startButton));
            Check(shopButton, nameof(shopButton));
            Check(recordsButton, nameof(recordsButton));
            Check(resetButton, nameof(resetButton));
            Check(quitButton, nameof(quitButton));
            Check(shopCloseButton, nameof(shopCloseButton));
            Check(recordsCloseButton, nameof(recordsCloseButton));
            Check(shopUi, nameof(shopUi));
            Check(recordsText, nameof(recordsText));
            Check(goldLabel, nameof(goldLabel));
            Check(resetLabel, nameof(resetLabel));
            Check(soundButton, nameof(soundButton));
            Check(soundLabel, nameof(soundLabel));
            Check(shakeButton, nameof(shakeButton));
            Check(shakeLabel, nameof(shakeLabel));
            Check(catalog, nameof(catalog));
            return missing;
        }
    }
}
