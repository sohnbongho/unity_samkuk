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
        [SerializeField] Button shopButton;
        [SerializeField] Button recordsButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button quitButton;
        [SerializeField] Button soundButton;

        [Header("하위 화면")]
        [SerializeField] Button shopCloseButton;
        [SerializeField] Button recordsCloseButton;
        [SerializeField] MetaShopUI shopUi;
        [SerializeField] Text recordsText;

        [Header("표시")]
        [SerializeField] Text goldLabel;
        [SerializeField] Text resetLabel;
        [SerializeField] Text soundLabel;
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
            Bind(shopButton, OpenShop);
            Bind(recordsButton, OpenRecords);
            Bind(resetButton, OnResetClicked);
            Bind(quitButton, Quit);
            Bind(soundButton, CycleSfxVolume);
            Bind(shopCloseButton, ShowMain);
            Bind(recordsCloseButton, ShowMain);

            shop = new MetaShop(catalog);
            shop.Changed += RefreshGold;
            if (shopUi != null) shopUi.Bind(shop);
        }

        void Start()
        {
            ShowMain();
            RefreshSoundLabel();
        }

        void OnDestroy()
        {
            if (shop != null) shop.Changed -= RefreshGold;
        }

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

        public void StartGame() => SceneLoader?.Invoke(GameManager.GameSceneName);

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
            Check(catalog, nameof(catalog));
            return missing;
        }
    }
}
