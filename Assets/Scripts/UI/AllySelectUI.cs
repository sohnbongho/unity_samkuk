using System;
using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>아군 선택 화면의 표시/입력을 추상화한 인터페이스 (테스트에서 대체 가능).</summary>
    public interface IAllySelectView
    {
        bool IsVisible { get; }

        /// <summary>
        /// candidates 중 최대 maxPick 명을 고르게 한다. preselected 는 처음부터 골라 둘 장수.
        /// 확정하면 고른 장수 목록(0명도 가능)으로 onConfirmed 를 부른다.
        /// </summary>
        void Show(IReadOnlyList<HeroData> candidates, int maxPick, IReadOnlyList<HeroData> preselected,
            Action<IReadOnlyList<HeroData>> onConfirmed);

        void Hide();
    }

    [Serializable]
    public class AllyCardView
    {
        public Button button;
        public Text hotkey;
        public Text heroName;
        public Text title;
        public Text description;
        public Image portrait;
        [Tooltip("골랐을 때 켜지는 표시 (예: '동행' 글자)")] public GameObject selectedMark;

        [NonSerialized] public Sprite fallbackSprite;
    }

    /// <summary>
    /// 아군 선택 카드 UI. 카드를 클릭하거나 1~4 키로 고르고/해제하며, 출발 버튼이나 Enter/Space 로 확정한다.
    /// 정원이 차면 가장 먼저 고른 장수가 빠지고 새로 고른 장수가 들어간다.
    /// </summary>
    public class AllySelectUI : MonoBehaviour, IAllySelectView
    {
        [SerializeField] GameObject panel;
        [SerializeField] AllyCardView[] cards;
        [SerializeField] Text countLabel;
        [SerializeField] Button confirmButton;

        readonly List<HeroData> candidates = new List<HeroData>();
        readonly List<int> picked = new List<int>(); // 고른 순서대로 후보 번호
        Action<IReadOnlyList<HeroData>> onConfirmed;
        int maxPick;
        int shownFrame = -1;

        public bool IsVisible => panel != null && panel.activeSelf;
        /// <summary>지금 고른 후보 수.</summary>
        public int PickedCount => picked.Count;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Show(IReadOnlyList<HeroData> heroes, int maxPickCount, IReadOnlyList<HeroData> preselected,
            Action<IReadOnlyList<HeroData>> confirmedCallback)
        {
            onConfirmed = confirmedCallback;
            maxPick = Mathf.Max(0, maxPickCount);
            candidates.Clear();
            picked.Clear();
            shownFrame = Time.frameCount; // 장수를 고른 같은 프레임의 키 입력이 아군 선택으로 새지 않게

            int visible = Mathf.Min(heroes.Count, cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                bool active = i < visible;
                card.button.gameObject.SetActive(active);
                if (!active) continue;

                var hero = heroes[i];
                candidates.Add(hero);

                if (card.hotkey != null) card.hotkey.text = $"[{i + 1}]";
                card.heroName.text = hero.displayName;
                card.title.text = hero.title;
                card.description.text = BuildDescription(hero);
                ShowPortrait(card, hero);

                int index = i;
                card.button.onClick.RemoveAllListeners();
                card.button.onClick.AddListener(() => Toggle(index));
            }

            if (preselected != null)
            {
                foreach (var hero in preselected)
                {
                    int idx = candidates.IndexOf(hero);
                    if (idx >= 0 && !picked.Contains(idx) && picked.Count < maxPick) picked.Add(idx);
                }
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(Confirm);
            }

            Refresh();
            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        static void ShowPortrait(AllyCardView card, HeroData hero)
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

        static string BuildDescription(HeroData hero)
        {
            var lines = new List<string>();
            if (hero.startingWeapon != null) lines.Add($"무기: {hero.startingWeapon.displayName}");
            string stats = hero.StatSummary();
            if (!string.IsNullOrEmpty(stats)) lines.Add(stats);
            if (!string.IsNullOrEmpty(hero.description)) lines.Add(hero.description);
            return string.Join("\n\n", lines);
        }

        /// <summary>index 번째 후보를 고르거나 해제한다. 정원이 차 있으면 가장 먼저 고른 장수를 뺀다.</summary>
        public void Toggle(int index)
        {
            if (index < 0 || index >= candidates.Count || maxPick <= 0) return;

            if (!picked.Remove(index))
            {
                if (picked.Count >= maxPick) picked.RemoveAt(0);
                picked.Add(index);
            }
            Refresh();
        }

        /// <summary>지금 고른 장수들로 확정한다 (0명도 가능).</summary>
        public void Confirm()
        {
            if (!IsVisible) return;

            var result = new List<HeroData>(picked.Count);
            foreach (int idx in picked) result.Add(candidates[idx]);
            onConfirmed?.Invoke(result);
        }

        void Refresh()
        {
            if (countLabel != null)
                countLabel.text = $"함께 싸울 장수를 고르세요 ({picked.Count}/{maxPick})";

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].selectedMark != null)
                    cards[i].selectedMark.SetActive(i < candidates.Count && picked.Contains(i));
            }
        }

        void Update()
        {
            if (!IsVisible || Time.frameCount == shownFrame) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (candidates.Count >= 1 && kb.digit1Key.wasPressedThisFrame) Toggle(0);
            else if (candidates.Count >= 2 && kb.digit2Key.wasPressedThisFrame) Toggle(1);
            else if (candidates.Count >= 3 && kb.digit3Key.wasPressedThisFrame) Toggle(2);
            else if (candidates.Count >= 4 && kb.digit4Key.wasPressedThisFrame) Toggle(3);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Confirm();
        }
    }
}
