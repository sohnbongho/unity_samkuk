using System;
using System.Collections.Generic;
using Samkuk.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>레벨업 선택 화면의 표시/입력을 추상화한 인터페이스 (테스트에서 대체 가능).</summary>
    public interface ILevelUpView
    {
        bool IsVisible { get; }
        void Show(IReadOnlyList<UpgradeOption> options, Action<int> onChosen);
        void Hide();
    }

    [Serializable]
    public class UpgradeCardView
    {
        public Button button;
        public Text title;
        public Text description;
        public Text hotkey;
    }

    /// <summary>레벨업 선택 카드 UI. 마우스 클릭 또는 1/2/3 키로 선택한다.</summary>
    public class LevelUpUI : MonoBehaviour, ILevelUpView
    {
        [SerializeField] GameObject panel;
        [SerializeField] UpgradeCardView[] cards;

        static readonly Color EvolveColor = new Color(0.62f, 0.46f, 0.1f, 1f);

        Action<int> onChosen;
        int visibleCards;
        Color[] baseColors;

        public bool IsVisible => panel != null && panel.activeSelf;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Show(IReadOnlyList<UpgradeOption> options, Action<int> chosenCallback)
        {
            onChosen = chosenCallback;
            visibleCards = Mathf.Min(options.Count, cards.Length);

            // 카드 기본 색은 처음 표시할 때 기억해 두고, 진화 카드만 금빛으로 강조한다
            if (baseColors == null)
            {
                baseColors = new Color[cards.Length];
                for (int i = 0; i < cards.Length; i++)
                    baseColors[i] = cards[i].button.targetGraphic != null ? cards[i].button.targetGraphic.color : Color.white;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                bool active = i < options.Count;
                card.button.gameObject.SetActive(active);
                if (!active) continue;

                card.title.text = options[i].Title;
                if (card.button.targetGraphic != null)
                    card.button.targetGraphic.color = options[i].Kind == UpgradeKind.Evolve ? EvolveColor : baseColors[i];
                card.description.text = options[i].Description;
                if (card.hotkey != null) card.hotkey.text = $"[{i + 1}]";

                int index = i;
                card.button.onClick.RemoveAllListeners();
                card.button.onClick.AddListener(() => Choose(index));
            }

            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        void Choose(int index)
        {
            var callback = onChosen;
            callback?.Invoke(index);
        }

        void Update()
        {
            if (!IsVisible) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (visibleCards >= 1 && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)) Choose(0);
            else if (visibleCards >= 2 && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)) Choose(1);
            else if (visibleCards >= 3 && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)) Choose(2);
        }
    }
}
