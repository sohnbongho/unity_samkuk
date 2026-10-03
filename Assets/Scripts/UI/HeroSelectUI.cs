using System;
using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>장수 선택 화면의 표시/입력을 추상화한 인터페이스 (테스트에서 대체 가능).</summary>
    public interface IHeroSelectView
    {
        bool IsVisible { get; }
        void Show(IReadOnlyList<HeroData> heroes, Action<int> onChosen);
        void Hide();
    }

    [Serializable]
    public class HeroCardView
    {
        public Button button;
        public Text hotkey;
        public Text heroName;
        public Text title;
        public Text description;
        public Image portrait;
    }

    /// <summary>장수 선택 카드 UI. 마우스 클릭 또는 1~5 키로 선택한다.</summary>
    public class HeroSelectUI : MonoBehaviour, IHeroSelectView
    {
        [SerializeField] GameObject panel;
        [SerializeField] HeroCardView[] cards;

        Action<int> onChosen;
        int visibleCards;

        public bool IsVisible => panel != null && panel.activeSelf;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Show(IReadOnlyList<HeroData> heroes, Action<int> chosenCallback)
        {
            onChosen = chosenCallback;
            visibleCards = Mathf.Min(heroes.Count, cards.Length);

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                bool active = i < heroes.Count;
                card.button.gameObject.SetActive(active);
                if (!active) continue;

                var hero = heroes[i];
                if (card.hotkey != null) card.hotkey.text = $"[{i + 1}]";
                card.heroName.text = hero.displayName;
                card.title.text = hero.title;
                card.description.text = BuildDescription(hero);
                if (card.portrait != null) card.portrait.color = hero.tint;

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

        static string BuildDescription(HeroData hero)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(hero.description)) lines.Add(hero.description);

            string stats = hero.StatSummary();
            if (!string.IsNullOrEmpty(stats)) lines.Add(stats);

            if (hero.startingWeapon != null) lines.Add($"무기: {hero.startingWeapon.displayName}");
            if (hero.skill != null) lines.Add($"스킬: {hero.skill.displayName}\n{hero.skill.description}");
            return string.Join("\n\n", lines);
        }

        void Choose(int index) => onChosen?.Invoke(index);

        void Update()
        {
            if (!IsVisible) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (visibleCards >= 1 && kb.digit1Key.wasPressedThisFrame) Choose(0);
            else if (visibleCards >= 2 && kb.digit2Key.wasPressedThisFrame) Choose(1);
            else if (visibleCards >= 3 && kb.digit3Key.wasPressedThisFrame) Choose(2);
            else if (visibleCards >= 4 && kb.digit4Key.wasPressedThisFrame) Choose(3);
            else if (visibleCards >= 5 && kb.digit5Key.wasPressedThisFrame) Choose(4);
        }
    }
}
