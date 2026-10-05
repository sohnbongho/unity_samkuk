using System;
using Samkuk.Audio;
using Samkuk.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>결과 화면의 표시를 추상화한 인터페이스 (테스트에서 대체 가능).</summary>
    public interface IResultView
    {
        bool IsVisible { get; }
        void Show(RunResult result, Action onRetry, Action onTitle);
        void Hide();
    }

    /// <summary>결과 화면: 클리어/패배, 생존 시간, 처치 수, 레벨, 획득 골드, 기록 갱신, 버튼.</summary>
    public class ResultUI : MonoBehaviour, IResultView
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text titleLabel;
        [SerializeField] Text detailsLabel;
        [SerializeField] Button retryButton;
        [SerializeField] Button titleButton;
        [SerializeField, Tooltip("내정으로 돌아가기 (내정에서 출진한 판에서만 보임, 없어도 동작)")] Button strategyButton;

        Action onStrategy;

        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>[내정으로] 동작을 정한다 (null 이면 버튼을 숨긴다). Show 전에 호출한다.</summary>
        public void SetStrategyReturn(Action onStrategyReturn) => onStrategy = onStrategyReturn;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Show(RunResult r, Action onRetry, Action onTitle)
        {
            if (titleLabel != null)
            {
                titleLabel.text = r.cleared ? "스테이지 클리어!" : "게임 오버";
                titleLabel.color = r.cleared ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.4f);
            }
            if (detailsLabel != null) detailsLabel.text = Format(r);

            Bind(retryButton, onRetry);
            Bind(titleButton, onTitle);
            if (strategyButton != null)
            {
                strategyButton.gameObject.SetActive(onStrategy != null);
                Bind(strategyButton, onStrategy);
            }
            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        static void Bind(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                action?.Invoke();
            });
        }

        /// <summary>결과 요약 텍스트.</summary>
        public static string Format(RunResult r)
        {
            int sec = Mathf.FloorToInt(r.seconds);
            string time = $"{sec / 60:00}:{sec % 60:00}";
            string hero = string.IsNullOrEmpty(r.heroName) ? "" : $"장수      {r.heroName}\n";
            string castle = string.IsNullOrEmpty(r.castleName) ? "" : $"출진 성   {r.castleName}\n";

            return castle + hero +
                   $"생존 시간   {time}{(r.newBestTime ? "   ★ 최고 기록!" : "")}\n" +
                   $"처치 수      {r.kills}{(r.newBestKills ? "   ★ 최고 기록!" : "")}\n" +
                   $"도달 레벨   {r.level}\n\n" +
                   $"획득 골드   +{r.goldEarned}\n" +
                   $"보유 골드   {r.totalGold}";
        }
    }
}
