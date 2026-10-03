using Samkuk.Upgrades;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 캔버스 아래 UI에 중국풍 테마를 입힌다. 이름 규칙으로 역할을 알아본다:
    ///   Card*/Hero* + Button → 카드(바탕 + 금빛 프레임), 그 밖의 Button → 버튼(StartButton 은 진홍, Reset/Quit 은 차분한 색),
    ///   Box → 대화상자 패널, HpBar/ExpBar/BossBar/SkillHud → 얇은 프레임, 이름이 Title 인 Text → 금색 + 그림자.
    /// 여러 번 적용해도 프레임이 중복되지 않는다. 런타임에 만드는 UI(상점 행 등)는 정적 메서드로 직접 입힌다.
    /// </summary>
    public class UiSkin : MonoBehaviour
    {
        public const string FrameName = "Frame";

        void Awake() => Apply(transform);

        /// <summary>root 아래(비활성 포함)의 모든 UI에 테마를 입힌다.</summary>
        public static void Apply(Transform root)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                string n = img.name;
                if (n == FrameName) continue;

                var button = img.GetComponent<Button>();
                if (button != null)
                {
                    if (IsCardName(n)) StyleCard(img);
                    else StyleButton(button);
                }
                else if (n == "Box") StylePanel(img);
                else if (n == "HpBar" || n == "ExpBar" || n == "BossBar" || n == "SkillHud") StyleSlot(img);
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name == "Title" && !IsCardName(text.transform.parent != null ? text.transform.parent.name : ""))
                    StyleTitle(text);
            }
        }

        /// <summary>"Card1", "Hero3" 처럼 이름이 Card/Hero + 숫자인가 (HeroSelectPanel 같은 이름은 아님).</summary>
        public static bool IsCardName(string name)
        {
            if (string.IsNullOrEmpty(name) || !(name.StartsWith("Card") || name.StartsWith("Hero"))) return false;
            return char.IsDigit(name[name.Length - 1]);
        }

        // ───────────────────────── 역할별 스타일 ─────────────────────────

        public static void StylePanel(Image img)
        {
            var t = UiTheme.Get();
            SetSliced(img, t.fill, t.panel);
            AddFrame(img, t.frame);
        }

        /// <summary>카드 바탕 + 프레임. recolor 가 false 면 현재 색을 유지한다 (색을 직접 관리하는 UI용).</summary>
        public static void StyleCard(Image img, bool recolor = true)
        {
            var t = UiTheme.Get();
            SetSliced(img, t.fill, recolor ? t.card : img.color);
            AddFrame(img, t.frame);
        }

        /// <summary>버튼 바탕 + 얇은 프레임. recolor 가 false 면 현재 색을 유지한다.</summary>
        public static void StyleButton(Button button, bool recolor = true)
        {
            var img = button.targetGraphic as Image;
            if (img == null) return;

            var t = UiTheme.Get();
            SetSliced(img, t.fill, recolor ? ButtonColor(button.name, t) : img.color);
            AddFrame(img, t.frameThin);
        }

        /// <summary>색은 그대로 두고 얇은 프레임만 두른다 (체력/경험치 바, 스킬 슬롯).</summary>
        public static void StyleSlot(Image img) => AddFrame(img, UiTheme.Get().frameThin);

        public static void StyleTitle(Text text)
        {
            text.color = UiTheme.Get().gold;
            if (text.GetComponent<Shadow>() == null)
            {
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectDistance = new Vector2(2f, -2f);
            }
        }

        static Color ButtonColor(string name, UiTheme t)
        {
            if (name == "StartButton") return t.primaryButton;
            if (name == "ResetButton" || name == "QuitButton") return t.mutedButton;
            return t.button;
        }

        /// <summary>레벨업 카드 종류별 바탕색 (진화는 금색).</summary>
        public static Color CardTint(UpgradeKind kind)
        {
            var t = UiTheme.Get();
            switch (kind)
            {
                case UpgradeKind.Evolve: return t.evolve;
                case UpgradeKind.NewWeapon: return t.newWeapon;
                case UpgradeKind.WeaponLevelUp: return t.weaponUp;
                case UpgradeKind.Passive: return t.passive;
                case UpgradeKind.Heal: return t.heal;
                default: return t.card;
            }
        }

        // ───────────────────────── 부품 ─────────────────────────

        static void SetSliced(Image img, Sprite sprite, Color color)
        {
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
        }

        /// <summary>대상을 덮는 테두리 이미지를 자식으로 만든다 (이미 있으면 스프라이트만 갱신). 입력과 레이아웃에는 영향이 없다.</summary>
        public static Image AddFrame(Image target, Sprite sprite)
        {
            if (sprite == null) return null;

            Image frame;
            var existing = target.transform.Find(FrameName);
            if (existing != null)
            {
                frame = existing.GetComponent<Image>();
            }
            else
            {
                var go = new GameObject(FrameName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(target.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.GetComponent<LayoutElement>().ignoreLayout = true; // 부모가 레이아웃 그룹이어도 자리를 차지하지 않음
                frame = go.GetComponent<Image>();
                frame.raycastTarget = false;
            }

            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
            frame.transform.SetAsLastSibling();
            return frame;
        }
    }
}
