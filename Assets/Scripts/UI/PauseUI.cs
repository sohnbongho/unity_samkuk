using System;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>일시정지 화면의 표시를 추상화한 인터페이스 (테스트에서 대체 가능).</summary>
    public interface IPauseView
    {
        bool IsVisible { get; }
        void Show(Action onResume, Action onRestart, Action onTitle);
        void Hide();
    }

    /// <summary>일시정지 메뉴: 계속하기 / 다시 시작 / 타이틀로.</summary>
    public class PauseUI : MonoBehaviour, IPauseView
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button titleButton;

        public bool IsVisible => panel != null && panel.activeSelf;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Show(Action onResume, Action onRestart, Action onTitle)
        {
            Bind(resumeButton, onResume);
            Bind(restartButton, onRestart);
            Bind(titleButton, onTitle);
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
            button.onClick.AddListener(() => action?.Invoke());
        }
    }
}
