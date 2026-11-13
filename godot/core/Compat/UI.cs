using System;
using System.Collections.Generic;

namespace Samkuk.Compat
{
    /// <summary>
    /// 사각 배치 값을 가진 트랜스폼 (유니티 RectTransform 자리). 장면 모형의 모든 트랜스폼이 이 형식이라
    /// UI 로 쓰는 오브젝트는 따로 바꿀 것 없이 이 값들을 채우면 된다. 단위는 UI 기준 해상도의 픽셀, y 는 위가 +.
    /// </summary>
    public sealed class RectTransform : Transform
    {
        public enum Axis { Horizontal, Vertical }

        public Vector2 anchorMin = new Vector2(0.5f, 0.5f), anchorMax = new Vector2(0.5f, 0.5f);
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta = new Vector2(100f, 100f);
        /// <summary>배치 계산 결과: 캔버스 좌표의 사각형 (왼쪽 아래 원점). <see cref="UI.UiLayout"/> 이 채운다.</summary>
        public Rect screenRect;

        /// <summary>부모 사각형의 크기. 최상위(캔버스)는 기준 해상도.</summary>
        Vector2 ParentSize => parent is RectTransform p ? p.rect.size : UI.CanvasScaler.CurrentReference;

        /// <summary>자기 사각형 (피벗 기준 좌표). 배치 그룹이 정하는 크기는 화면 층이 계산하므로 여기 값은 앵커·sizeDelta 만 본다.</summary>
        public Rect rect
        {
            get
            {
                var canvas = GetComponent<UI.Canvas>();
                Vector2 size;
                if (canvas != null && parent == null) size = UI.CanvasScaler.ReferenceOf(gameObject);
                else
                {
                    var ps = ParentSize;
                    size = new Vector2(ps.x * (anchorMax.x - anchorMin.x) + sizeDelta.x, ps.y * (anchorMax.y - anchorMin.y) + sizeDelta.y);
                }
                return new Rect(-pivot.x * size.x, -pivot.y * size.y, size.x, size.y);
            }
        }

        /// <summary>앵커 사각형의 왼쪽 아래에서 자기 왼쪽 아래까지.</summary>
        public Vector2 offsetMin
        {
            get => new Vector2(anchoredPosition.x - sizeDelta.x * pivot.x, anchoredPosition.y - sizeDelta.y * pivot.y);
            set
            {
                var d = value - offsetMin;
                sizeDelta -= d;
                anchoredPosition += new Vector2(d.x * (1f - pivot.x), d.y * (1f - pivot.y));
            }
        }

        /// <summary>앵커 사각형의 오른쪽 위에서 자기 오른쪽 위까지.</summary>
        public Vector2 offsetMax
        {
            get => new Vector2(anchoredPosition.x + sizeDelta.x * (1f - pivot.x), anchoredPosition.y + sizeDelta.y * (1f - pivot.y));
            set
            {
                var d = value - offsetMax;
                sizeDelta += d;
                anchoredPosition += new Vector2(d.x * pivot.x, d.y * pivot.y);
            }
        }

        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            var ps = ParentSize;
            if (axis == Axis.Horizontal) sizeDelta = new Vector2(size - ps.x * (anchorMax.x - anchorMin.x), sizeDelta.y);
            else sizeDelta = new Vector2(sizeDelta.x, size - ps.y * (anchorMax.y - anchorMin.y));
        }

        internal override void CopyStateFrom(Component source)
        {
            base.CopyStateFrom(source);
            if (source is RectTransform s)
            {
                anchorMin = s.anchorMin; anchorMax = s.anchorMax; pivot = s.pivot; anchoredPosition = s.anchoredPosition; sizeDelta = s.sizeDelta;
            }
        }
    }

    /// <summary>글꼴 손잡이: 파일 경로 또는 OS 글꼴 이름들. 실제 글꼴은 화면 층이 만든다.</summary>
    public sealed class Font : Object
    {
        public string path;
        public string[] osFontNames;
        public string name;
        public int fontSize = 16;
        /// <summary>글자를 그리는 재질 이름표 (세계에 뜨는 글자가 쓴다).</summary>
        public Material material = new Material(Shader.Find("GUI/Text Shader"));

        public static Font CreateDynamicFontFromOSFont(string[] fontnames, int size) => new Font { osFontNames = fontnames, fontSize = size, name = fontnames != null && fontnames.Length > 0 ? fontnames[0] : "" };
        public static Font CreateDynamicFontFromOSFont(string fontname, int size) => CreateDynamicFontFromOSFont(new[] { fontname }, size);
        public static string[] GetOSInstalledFontNames() => OsFontNames != null ? OsFontNames() : new string[0];
        /// <summary>화면 층이 설치된 글꼴 이름 목록을 꽂는다.</summary>
        public static Func<string[]> OsFontNames;
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum TextAlignment { Left, Center, Right }

    /// <summary>화면 크기 (화면 층이 채운다. 테스트 기본값 1920x1080).</summary>
    public static class Screen
    {
        public static int width = 1920, height = 1080;
        public struct Resolution { public int width, height; }
        public static Resolution currentResolution = new Resolution { width = 1920, height = 1080 };
        public static Action<int, int, FullScreenMode> SetResolutionAction;
        public static void SetResolution(int w, int h, FullScreenMode mode) => SetResolutionAction?.Invoke(w, h, mode);
    }
}

namespace Samkuk.Compat.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T value);

    /// <summary>듣는 쪽을 여럿 달 수 있는 알림 (버튼 눌림 등).</summary>
    public class UnityEvent
    {
        readonly List<UnityAction> listeners = new List<UnityAction>();
        public void AddListener(UnityAction call) { if (call != null) listeners.Add(call); }
        public void RemoveListener(UnityAction call) => listeners.Remove(call);
        public void RemoveAllListeners() => listeners.Clear();
        public int ListenerCount => listeners.Count;
        public void Invoke() { foreach (var l in listeners.ToArray()) l(); }
    }

    public class UnityEvent<T>
    {
        readonly List<UnityAction<T>> listeners = new List<UnityAction<T>>();
        public void AddListener(UnityAction<T> call) { if (call != null) listeners.Add(call); }
        public void RemoveListener(UnityAction<T> call) => listeners.Remove(call);
        public void RemoveAllListeners() => listeners.Clear();
        public void Invoke(T value) { foreach (var l in listeners.ToArray()) l(value); }
    }
}

namespace Samkuk.Compat.UI
{
    using Samkuk.Compat.Events;

    // UI 모형 (G3): uGUI 와 같은 이름의 "데이터" 컴포넌트. 규칙 코드와 테스트는 글자·색·눌림을 여기서 읽고 쓰고,
    // 화면 층(Godot)이 이 나무를 Control 노드로 비춘다(사각 배치 → 앵커/오프셋, Text → Label, Image → TextureRect/NinePatch, Button → 눌림 전달).
    // 배치 그룹(가로/세로/격자)의 실제 계산은 Godot 컨테이너가 한다.

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        public int sortingOrder;
        public bool overrideSorting;
        public float scaleFactor = 1f;
    }

    public class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }

        public ScaleMode uiScaleMode = ScaleMode.ConstantPixelSize;
        public Vector2 referenceResolution = new Vector2(800f, 600f);
        public ScreenMatchMode screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
        public float matchWidthOrHeight;
        public float scaleFactor = 1f;

        /// <summary>캔버스 밖에서 크기를 물을 때의 기준 (화면 크기).</summary>
        public static Vector2 CurrentReference => new Vector2(Screen.width, Screen.height);

        /// <summary>이 캔버스의 기준 해상도 (스케일러가 없으면 화면 크기).</summary>
        public static Vector2 ReferenceOf(GameObject canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            return scaler != null && scaler.uiScaleMode == ScaleMode.ScaleWithScreenSize ? scaler.referenceResolution : CurrentReference;
        }
    }

    public class GraphicRaycaster : Behaviour { }

    public class CanvasGroup : Behaviour
    {
        public float alpha = 1f;
        public bool interactable = true, blocksRaycasts = true, ignoreParentGroups;
    }

    /// <summary>그려지는 UI 의 바탕.</summary>
    public class Graphic : Behaviour
    {
        public Color color = Color.white;
        public bool raycastTarget = true;
        public object material;
        public RectTransform rectTransform => (RectTransform)transform;
        public Canvas canvas => GetComponentInParent<Canvas>();
        public void SetAllDirty() { }
    }

    public class Image : Graphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }

        public Sprite sprite;
        public Sprite overrideSprite { get => sprite; set => sprite = value; }
        public Type type = Type.Simple;
        public bool preserveAspect;
        public float fillAmount = 1f;
        public FillMethod fillMethod = FillMethod.Horizontal;
        public int fillOrigin;
        public bool fillCenter = true;
        public float pixelsPerUnitMultiplier = 1f;

        public void SetNativeSize()
        {
            if (sprite != null) rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
        }
    }

    public class RawImage : Graphic
    {
        public Texture2D texture;
        public Rect uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    public class Text : Graphic
    {
        public string text = "";
        public Font font;
        public int fontSize = 14;
        public FontStyle fontStyle = FontStyle.Normal;
        public TextAnchor alignment = TextAnchor.UpperLeft;
        public HorizontalWrapMode horizontalOverflow = HorizontalWrapMode.Wrap;
        public VerticalWrapMode verticalOverflow = VerticalWrapMode.Truncate;
        public bool supportRichText = true;
        public float lineSpacing = 1f;
        public bool resizeTextForBestFit;
        public int resizeTextMinSize = 10, resizeTextMaxSize = 40;
    }

    /// <summary>글자 뒤 그림자/외곽선 (값만).</summary>
    public class Shadow : Behaviour
    {
        public Color effectColor = new Color(0f, 0f, 0f, 0.5f);
        public Vector2 effectDistance = new Vector2(1f, -1f);
        public bool useGraphicAlpha = true;
    }
    public class Outline : Shadow { }

    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public static ColorBlock defaultColorBlock => new ColorBlock
        {
            normalColor = Color.white, highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f), pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f),
            selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f), disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f), colorMultiplier = 1f, fadeDuration = 0.1f,
        };
    }

    public struct Navigation
    {
        public enum Mode { None, Horizontal, Vertical, Automatic, Explicit }
        public Mode mode;
        public static Navigation defaultNavigation => new Navigation { mode = Mode.Automatic };
    }

    /// <summary>고를 수 있는 UI 의 바탕 (버튼, 토글, 입력 칸).</summary>
    public class Selectable : Behaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }

        public bool interactable = true;
        public Graphic targetGraphic;
        public Transition transition = Transition.ColorTint;
        public ColorBlock colors = ColorBlock.defaultColorBlock;
        public Navigation navigation = Navigation.defaultNavigation;
        public Image image { get => targetGraphic as Image; set => targetGraphic = value; }

        public virtual void Select() => EventSystem.EnsureCurrent().SetSelectedGameObject(gameObject);
        public bool IsInteractable() => interactable && isActiveAndEnabled;
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick = new ButtonClickedEvent();

        /// <summary>눌림 (화면 층의 실제 클릭, 테스트). 꺼져 있거나 못 누르는 상태면 무시한다.</summary>
        public void Press()
        {
            if (IsInteractable()) onClick.Invoke();
        }
    }

    public class Toggle : Selectable
    {
        public class ToggleEvent : UnityEvent<bool> { }
        public ToggleEvent onValueChanged = new ToggleEvent();
        public Graphic graphic;
        bool on;

        public bool isOn
        {
            get => on;
            set
            {
                if (on == value) return;
                on = value;
                onValueChanged.Invoke(on);
            }
        }
        public void SetIsOnWithoutNotify(bool value) => on = value;
    }

    public class InputField : Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public class OnChangeEvent : UnityEvent<string> { }
        public class EndEditEvent : UnityEvent<string> { }

        public Text textComponent;
        public Graphic placeholder;
        public ContentType contentType = ContentType.Standard;
        public LineType lineType = LineType.SingleLine;
        public int characterLimit;
        public OnChangeEvent onValueChanged = new OnChangeEvent();
        public EndEditEvent onEndEdit = new EndEditEvent();
        public EndEditEvent onSubmit = new EndEditEvent();
        public int caretPosition;
        public Color caretColor = Color.white;
        public bool customCaretColor;
        public bool isFocused { get; private set; }
        string value = "";

        public string text
        {
            get => value;
            set
            {
                string v = value ?? "";
                if (characterLimit > 0 && v.Length > characterLimit) v = v.Substring(0, characterLimit);
                if (this.value == v) return;
                this.value = v;
                onValueChanged.Invoke(v);
            }
        }
        public void SetTextWithoutNotify(string v) => value = v ?? "";
        public void ActivateInputField() { isFocused = true; Select(); }
        public void DeactivateInputField() { isFocused = false; }
    }

    public class ScrollRect : Behaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content, viewport;
        public bool horizontal = true, vertical = true;
        public MovementType movementType = MovementType.Elastic;
        public float scrollSensitivity = 1f;
        public float verticalNormalizedPosition = 1f, horizontalNormalizedPosition;
        public Vector2 normalizedPosition
        {
            get => new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition);
            set { horizontalNormalizedPosition = value.x; verticalNormalizedPosition = value.y; }
        }
    }

    public class Mask : Behaviour { public bool showMaskGraphic = true; }
    public class RectMask2D : Behaviour { }

    [Serializable]
    public class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
        public int horizontal => left + right;
        public int vertical => top + bottom;
    }

    public class LayoutGroup : Behaviour
    {
        public RectOffset padding = new RectOffset();
        public TextAnchor childAlignment = TextAnchor.UpperLeft;
    }

    public class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing;
        public bool childControlWidth = true, childControlHeight = true;
        public bool childForceExpandWidth = true, childForceExpandHeight = true;
        public bool childScaleWidth, childScaleHeight, reverseArrangement;
    }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class GridLayoutGroup : LayoutGroup
    {
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public enum Axis { Horizontal, Vertical }
        public enum Corner { UpperLeft, UpperRight, LowerLeft, LowerRight }
        public Vector2 cellSize = new Vector2(100f, 100f);
        public Vector2 spacing;
        public Constraint constraint = Constraint.Flexible;
        public int constraintCount = 2;
        public Axis startAxis = Axis.Horizontal;
        public Corner startCorner = Corner.UpperLeft;
    }

    public class LayoutElement : Behaviour
    {
        public bool ignoreLayout;
        public float minWidth = -1f, minHeight = -1f, preferredWidth = -1f, preferredHeight = -1f, flexibleWidth = -1f, flexibleHeight = -1f;
        public int layoutPriority = 1;
    }

    public class ContentSizeFitter : Behaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit = FitMode.Unconstrained, verticalFit = FitMode.Unconstrained;
    }

    public class AspectRatioFitter : Behaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode = AspectMode.None;
        public float aspectRatio = 1f;
    }

    /// <summary>지금 고른 UI 를 기억한다 (키보드 조작).</summary>
    public class EventSystem : Behaviour
    {
        static EventSystem fallback;
        public static EventSystem current
        {
            get
            {
                var found = Object.FindAnyObjectByType<EventSystem>();
                return found;
            }
        }

        public GameObject currentSelectedGameObject { get; private set; }
        public GameObject firstSelectedGameObject;
        /// <summary>마우스가 UI 위에 있는가 (화면 층이 채운다).</summary>
        public static bool PointerOverUi;

        public void SetSelectedGameObject(GameObject selected) => currentSelectedGameObject = selected;
        public bool IsPointerOverGameObject() => PointerOverUi;

        /// <summary>장면에 이벤트 시스템이 없어도 고르기가 죽지 않게 (테스트).</summary>
        internal static EventSystem EnsureCurrent()
        {
            var c = current;
            if (c != null) return c;
            if (fallback == null || fallback.destroyed) fallback = new EventSystem();
            return fallback;
        }
    }

    public class BaseInputModule : Behaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class InputSystemUIInputModule : BaseInputModule { public void AssignDefaultActions() { } }
}
