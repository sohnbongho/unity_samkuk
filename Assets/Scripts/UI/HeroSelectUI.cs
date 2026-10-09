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

        /// <summary>초상화가 없는 장수를 위한 기본 실루엣 (처음 한 번 저장해 두었다가 되돌린다).</summary>
        [NonSerialized] public Sprite fallbackSprite;
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
                ShowPortrait(card, hero);

                int index = i;
                card.button.onClick.RemoveAllListeners();
                card.button.onClick.AddListener(() => Choose(index));
            }

            panel.SetActive(true);
        }

        /// <summary>초상화가 있으면 원래 색 그대로, 없으면 기본 실루엣에 장수 색을 입힌다.</summary>
        static void ShowPortrait(HeroCardView card, HeroData hero)
        {
            if (card.portrait == null) return;

            if (card.fallbackSprite == null) card.fallbackSprite = card.portrait.sprite;
            if (hero.portrait != null)
            {
                card.portrait.sprite = hero.portrait;
                card.portrait.color = Color.white;
            }
            else
            {
                card.portrait.sprite = card.fallbackSprite;
                card.portrait.color = hero.tint;
            }
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

            // 무기 목록: 전용 무기 + 공용 무기. "이 장수를 고르면 이런 무기로 싸운다"가 선택의 이유가 되도록 전부 보여 준다
            var weaponLines = new List<string>();
            if (hero.startingWeapon != null) weaponLines.Add($"전용 무기: {hero.startingWeapon.displayName}");
            string common = hero.WeaponListSummary();
            if (!string.IsNullOrEmpty(common)) weaponLines.Add($"공용 무기: {common}");
            if (weaponLines.Count > 0) lines.Add(string.Join("\n", weaponLines));
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
