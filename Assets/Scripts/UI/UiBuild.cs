using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 실행 중에 코드로 만드는 창(로그인 창, 협동 방 창)의 공통 조립 헬퍼. 가운데 기준 배치, 글자, 입력칸, 버튼.
    /// 스킨은 UiSkin 을 직접 호출한다(이름 규칙 스킨은 셋업이 만든 캔버스에만 돈다).
    /// </summary>
    public static class UiBuild
    {
        /// <summary>부모 가운데 기준으로 (x, y) 에 w x h 로 놓는다.</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Text Text(string name, Transform parent, Font font, int size, TextAnchor anchor, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        public static InputField Input(string name, Transform parent, Font font, string placeholder, float y, bool password)
            => Input(name, parent, font, placeholder, 0f, y, 500f, 66f, password);

        public static InputField Input(string name, Transform parent, Font font, string placeholder, float x, float y, float w, float h, bool password)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, w, h);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.05f, 0.04f, 0.03f, 0.9f);
            UiSkin.StyleSlot(img);

            var text = Text("Text", go.transform, font, 30, TextAnchor.MiddleLeft, "");
            text.supportRichText = false;
            Stretch(text.rectTransform, 18f);
            var hint = Text("Placeholder", go.transform, font, 30, TextAnchor.MiddleLeft, placeholder);
            hint.color = new Color(1f, 1f, 1f, 0.35f);
            hint.fontStyle = FontStyle.Italic;
            Stretch(hint.rectTransform, 18f);

            var field = go.GetComponent<InputField>();
            field.targetGraphic = img;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = 32;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.caretColor = Color.white;
            field.customCaretColor = true;
            return field;
        }

        public static Button Button(string name, Transform parent, Font font, string label, float y, Vector2 size, Color color)
            => Button(name, parent, font, label, 0f, y, size, color);

        public static Button Button(string name, Transform parent, Font font, string label, float x, float y, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, size.x, size.y);
            var img = go.GetComponent<Image>();
            img.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            UiSkin.StyleButton(button);
            var text = Text("Label", go.transform, font, 30, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform, 0f);
            return button;
        }

        /// <summary>버튼의 글자를 바꾼다 (Button 헬퍼가 만든 "Label" 자식).</summary>
        public static void SetLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.transform.Find("Label")?.GetComponent<Text>();
            if (text != null) text.text = label;
        }

        public static void Stretch(RectTransform rt, float padX)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, 0f);
            rt.offsetMax = new Vector2(-padX, 0f);
        }
    }
}
