using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 영구 강화 상점 화면. MetaShop의 항목 수만큼 행을 코드로 만들어 컨테이너에 채운다
    /// (강화를 추가해도 씬을 다시 구성할 필요가 없다).
    /// </summary>
    public class MetaShopUI : MonoBehaviour
    {
        const float RowHeight = 96f;

        [SerializeField, Tooltip("행이 채워질 세로 레이아웃 컨테이너")] RectTransform container;
        [SerializeField] Text goldLabel;

        class Row
        {
            public ShopEntry entry;
            public Text info;
            public Button button;
            public Text buttonLabel;
            public Image buttonImage;
        }

        readonly List<Row> rows = new List<Row>();
        MetaShop shop;

        public int RowCount => rows.Count;

        /// <summary>상점 모델을 연결하고 행을 만든다. 다시 호출하면 행을 새로 만든다.</summary>
        public void Bind(MetaShop model)
        {
            if (shop != null) shop.Changed -= Refresh;
            shop = model;
            shop.Changed += Refresh;
            Rebuild();
        }

        void OnDestroy()
        {
            if (shop != null) shop.Changed -= Refresh;
        }

        void Rebuild()
        {
            foreach (var r in rows)
                if (r.info != null) Destroy(r.info.transform.parent.gameObject);
            rows.Clear();

            shop.Refresh();
            foreach (var entry in shop.Entries) rows.Add(CreateRow(entry.Upgrade));
            Refresh();
        }

        /// <summary>모델의 현재 상태로 모든 행과 골드 표시를 갱신한다.</summary>
        public void Refresh()
        {
            if (shop == null) return;

            if (goldLabel != null) goldLabel.text = $"보유 골드  {shop.Gold}";

            for (int i = 0; i < rows.Count && i < shop.Entries.Count; i++)
            {
                var entry = shop.Entries[i];
                var row = rows[i];
                row.entry = entry;

                string next = entry.IsMax ? "" : $" → {entry.NextEffectText}";
                row.info.text =
                    $"{entry.Upgrade.displayName}    Lv.{entry.Level} / {entry.MaxLevel}\n" +
                    $"<size=22>{entry.Upgrade.description}   <color=#ffd966>현재 {entry.EffectText}{next}</color></size>";

                row.buttonLabel.text = entry.IsMax ? "MAX" : $"구매  {entry.Cost}G";
                row.button.interactable = entry.CanBuy;
                row.buttonImage.color = entry.CanBuy ? UiTheme.Get().positive : UiTheme.Get().disabled;
            }
        }

        // ───────────────────────── 행 생성 ─────────────────────────

        Row CreateRow(Data.MetaUpgradeData upgrade)
        {
            var font = UiFont.Get();

            var rowGo = new GameObject($"Row_{upgrade.name}", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(container, false);
            UiSkin.StyleCard(rowGo.GetComponent<Image>()); // 카드 바탕 + 금빛 프레임
            var le = rowGo.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeight;
            le.minHeight = RowHeight;

            var group = rowGo.GetComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(24, 16, 8, 8);
            group.spacing = 16f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;

            // 설명 텍스트 (남는 폭을 모두 차지)
            var infoGo = new GameObject("Info", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            infoGo.transform.SetParent(rowGo.transform, false);
            var info = infoGo.GetComponent<Text>();
            info.font = font; info.fontSize = 30; info.color = Color.white;
            info.alignment = TextAnchor.MiddleLeft;
            info.supportRichText = true;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.raycastTarget = false;
            infoGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // 구매 버튼
            var btnGo = new GameObject("Buy", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(rowGo.transform, false);
            var btnImage = btnGo.GetComponent<Image>();
            var button = btnGo.GetComponent<Button>();
            button.targetGraphic = btnImage;
            UiSkin.StyleButton(button, recolor: false); // 색은 구매 가능 여부에 따라 Refresh 가 정한다
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            var btnLe = btnGo.GetComponent<LayoutElement>();
            btnLe.preferredWidth = 230f;
            btnLe.minWidth = 160f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(btnGo.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = font; label.fontSize = 28; label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            var data = upgrade;
            button.onClick.AddListener(() =>
            {
                if (shop.Purchase(data)) AudioManager.Play(SfxId.Buy);
            });

            return new Row { info = info, button = button, buttonLabel = label, buttonImage = btnImage };
        }
    }
}
