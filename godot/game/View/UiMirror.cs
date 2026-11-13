using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using Samkuk.Compat;
using Samkuk.Compat.UI;
using Font = Godot.Font;
using Image = Samkuk.Compat.UI.Image;
using Mathf = Samkuk.Compat.Mathf;
using Button = Samkuk.Compat.UI.Button;
using CRect = Samkuk.Compat.Rect;
using CVec2 = Samkuk.Compat.Vector2;
using GColor = Godot.Color;
using GVec2 = Godot.Vector2;

namespace Samkuk.Game
{
    /// <summary>글꼴 한 곳: OS 의 한글 글꼴.</summary>
    public static class Fonts
    {
        static Font font;
        public static Font Default => font ??= new SystemFont
        {
            FontNames = new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "NanumGothic", "Arial" },
            Antialiasing = TextServer.FontAntialiasing.Gray,
        };
    }

    /// <summary>
    /// 장면 모형의 UI(캔버스 아래의 Text/Image/Button...)를 Godot Control 로 비춘다 (G3).
    /// 배치는 순수 로직의 <see cref="UiLayout"/> 가 계산하고, 여기서는 그 사각형대로 놓고 눌림을 버튼에 전한다.
    /// 그리는 순서 = 캔버스 정렬 순서 → 계층 순서. 캔버스 좌표(왼쪽 아래 원점, y 위)를 화면 좌표(왼쪽 위 원점)로 바꾼다.
    /// </summary>
    public partial class UiMirror : CanvasLayer
    {
        sealed class View
        {
            public Control node;
            public int seen;
            public LabelSettings label;
            public string textCache;
            public Sprite spriteCache;
        }

        struct Hit
        {
            public Rect2 rect;
            public Graphic graphic;
        }

        readonly Dictionary<Compat.Object, View> views = new Dictionary<Compat.Object, View>();
        readonly List<Hit> hits = new List<Hit>();
        readonly List<Canvas> canvases = new List<Canvas>();
        readonly List<Compat.Object> dead = new List<Compat.Object>();
        Control root;
        int frame, drawIndex;
        Selectable pressed;
        Selectable hovered;
        GVec2 mouse;
        float screenHeight;
        // 가리기(스크롤 영역): 가려지는 자식은 ClipContents 를 켠 상자 안에 넣는다. container = 지금 넣을 부모, origin = 그 부모의 화면 위치
        Control container;
        GVec2 origin;
        Rect2 clip;
        bool clipped;
        readonly Dictionary<Control, int> order = new Dictionary<Control, int>();
        float wheel;

        public override void _Ready()
        {
            Layer = 10;
            root = new Control { Name = "UiRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(root);
            UiLayout.MeasureText = Measure;
        }

        // 글자 크기 재기: 배치 그룹이 선호 크기를 물을 때 (캔버스 단위)
        static CVec2 Measure(Text text, float wrapWidth)
        {
            var size = Fonts.Default.GetMultilineStringSize(Plain(text.text ?? ""), HorizontalAlignment.Left, wrapWidth > 0f ? wrapWidth : -1f, Mathf.Max(1, text.fontSize));
            return new CVec2(size.X + 2f, size.Y * Mathf.Max(0.5f, text.lineSpacing));
        }

        static readonly Regex SizeAny = new Regex("<size=(\\d+)>", RegexOptions.Compiled);
        static readonly Regex ColorAny = new Regex("<color=([^>]+)>", RegexOptions.Compiled);
        static readonly Regex SizeTag = new Regex("^<size=(\\d+)>", RegexOptions.Compiled);
        static readonly Regex Tags = new Regex("</?(color|size|b|i)(=[^>]*)?>", RegexOptions.Compiled);
        /// <summary>유니티 리치 텍스트 꼬리표를 떼어 낸다 (색·크기 꼬리표는 한 군데뿐이라 글자만 남긴다).</summary>
        static string Plain(string s) => s.IndexOf('<') < 0 ? s : Tags.Replace(s, "");

        public void Sync(GVec2 screen)
        {
            frame++;
            drawIndex = 0;
            order.Clear();
            container = root;
            origin = GVec2.Zero;
            clipped = false;
            screenHeight = screen.Y;
            hits.Clear();
            mouse = GetViewport().GetMousePosition();

            canvases.Clear();
            foreach (var go in SceneRuntime.Objects)
            {
                if (!go.activeInHierarchy) continue;
                var canvas = go.GetComponent<Canvas>();
                // 맨 위 캔버스만 (다른 오브젝트의 자식이어도 된다. 캔버스 안의 캔버스는 바깥 것이 그린다)
                if (canvas != null && canvas.enabled && (go.transform.parent == null || go.transform.parent.GetComponentInParent<Canvas>() == null)) canvases.Add(canvas);
            }
            canvases.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

            foreach (var canvas in canvases)
            {
                var size = UiLayout.CanvasSize(canvas, new CVec2(screen.X, screen.Y), out float scale);
                UiLayout.Layout(canvas, size);
                Walk((RectTransform)canvas.transform, scale, 1f, true);
            }

            dead.Clear();
            foreach (var kv in views)
            {
                if (kv.Value.seen == frame) continue;
                if (kv.Key.destroyed) { kv.Value.node.QueueFree(); dead.Add(kv.Key); }
                else kv.Value.node.Visible = false;
            }
            foreach (var k in dead) views.Remove(k);

            UpdateHover();
            wheel = 0f;
        }

        void Walk(RectTransform rt, float scale, float alpha, bool interactive)
        {
            var go = rt.gameObject;
            var group = go.GetComponent<Samkuk.Compat.UI.CanvasGroup>();
            if (group != null && group.enabled)
            {
                alpha *= group.alpha;
                interactive &= group.blocksRaycasts;
            }

            var abs = ToScreen(rt.screenRect, scale);
            var rect = new Rect2(abs.Position - origin, abs.Size);   // 지금 부모 상자 안에서의 자리

            // 스크롤 영역: 마우스가 위에 있을 때 휠로 내용을 위아래로 민다 (내용이 보이는 칸보다 클 때만)
            var scroll = go.GetComponent<ScrollRect>();
            if (scroll != null && scroll.enabled && scroll.vertical && scroll.content != null && wheel != 0f && abs.HasPoint(mouse))
            {
                var view = scroll.viewport != null ? scroll.viewport : rt;
                float max = Mathf.Max(0f, scroll.content.screenRect.height - view.screenRect.height);
                var p = scroll.content.anchoredPosition;
                scroll.content.anchoredPosition = new CVec2(p.x, Mathf.Clamp(p.y - wheel * 60f * Mathf.Max(0.1f, scroll.scrollSensitivity) / 10f, 0f, max));
            }

            // 자기 회전(내정 지도의 길처럼 기울인 띠): 피벗을 중심으로. 화면은 y 가 아래라 방향이 반대다
            float turn = -rt.localEulerAngles.z * Mathf.Deg2Rad;
            var pivot = new GVec2(rt.pivot.x, 1f - rt.pivot.y);
            foreach (var c in go.components)
            {
                if (c is Image image) { if (image.enabled) { SyncImage(image, rect, scale, alpha, interactive); Turn(image, turn, pivot); } }
                else if (c is Text text) { if (text.enabled) { SyncText(text, rect, scale, alpha, interactive); Turn(text, turn, pivot); } }
                else if (c is InputField field) { if (field.enabled) SyncField(field, rect, scale); }
            }
            // 가리는 상자(RectMask2D, Mask): 자식을 이 사각형 안에서만 보이게
            var mask = (Compat.Object)go.GetComponent<RectMask2D>() ?? go.GetComponent<Mask>();
            var saved = (container, origin, clip, clipped);
            if (mask != null)
            {
                var box = Acquire<Control>(mask).node;
                box.ClipContents = true;
                box.Position = rect.Position;
                box.Size = rect.Size;
                clip = clipped ? clip.Intersection(abs) : abs;
                clipped = true;
                container = box;
                origin = abs.Position;
            }
            for (int i = 0; i < rt.childCount; i++)
            {
                var child = (RectTransform)rt.GetChild(i);
                if (child.gameObject.activeSelf) Walk(child, scale, alpha, interactive);
            }
            (container, origin, clip, clipped) = saved;
        }

        // 눌림 판정에 쓸 화면 사각형 (가려진 부분은 뺀다)
        void AddHit(Graphic graphic, Rect2 local)
        {
            var area = new Rect2(local.Position + origin, local.Size);
            if (clipped) area = area.Intersection(clip);
            if (area.Size.X > 0f && area.Size.Y > 0f) hits.Add(new Hit { rect = area, graphic = graphic });
        }

        void Turn(Compat.Object key, float radians, GVec2 pivot)
        {
            if (!views.TryGetValue(key, out var view)) return;
            var node = view.node;
            if (radians == 0f && node.Rotation == 0f) return;
            node.PivotOffset = new GVec2(node.Size.X * pivot.X, node.Size.Y * pivot.Y);
            node.Rotation = radians;
        }

        Rect2 ToScreen(CRect r, float scale) => new Rect2(r.x * scale, screenHeight - (r.y + r.height) * scale, r.width * scale, r.height * scale);

        View Acquire<T>(Compat.Object key) where T : Control, new()
        {
            if (!views.TryGetValue(key, out var view) || !(view.node is T))
            {
                view?.node.QueueFree();
                view = new View { node = new T { MouseFilter = Control.MouseFilterEnum.Ignore } };
                container.AddChild(view.node);
                views[key] = view;
            }
            view.seen = frame;
            view.node.Visible = true;
            Place(view.node);
            return view;
        }

        // 지금 부모 상자 안에, 이번 프레임에 만난 순서대로 (그리는 순서)
        void Place(Control node)
        {
            if (node.GetParent() != container) node.Reparent(container, false);
            order.TryGetValue(container, out int index);
            if (node.GetIndex() != index) container.MoveChild(node, Mathf.Min(index, container.GetChildCount() - 1));
            order[container] = index + 1;
            drawIndex++;
        }

        // 버튼 상태에 따른 색 배율 (보통/올림/누름/못 누름)
        GColor Tint(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            var button = graphic.GetComponentInParent<Selectable>();
            if (button != null && button.enabled && button.targetGraphic == graphic && button.transition == Selectable.Transition.ColorTint)
            {
                var b = button.colors;
                var state = !button.interactable ? b.disabledColor : button == pressed ? b.pressedColor : button == hovered ? b.highlightedColor : b.normalColor;
                c = c * state;
            }
            return new GColor(c.r, c.g, c.b, c.a * alpha);
        }

        void SyncImage(Image image, Rect2 rect, float scale, float alpha, bool interactive)
        {
            if (interactive && image.raycastTarget) AddHit(image, rect);
            var color = Tint(image, alpha);
            var sprite = image.sprite;
            var texture = sprite != null ? Textures.Of(sprite) : null;
            if (texture == null)
            {
                var view = Acquire<ColorRect>(image);
                var node = (ColorRect)view.node;
                node.Position = rect.Position; node.Size = rect.Size; node.Scale = GVec2.One;
                node.Color = color;
                return;
            }

            var v = Acquire<NinePatchRect>(image);
            var patch = (NinePatchRect)v.node;
            if (v.spriteCache != sprite)
            {
                v.spriteCache = sprite;
                patch.Texture = texture;
                patch.RegionRect = Textures.Region(sprite);
                patch.TextureFilter = sprite.texture != null && sprite.texture.filterMode == FilterMode.Point ? CanvasItem.TextureFilterEnum.Nearest : CanvasItem.TextureFilterEnum.Linear;
            }
            bool sliced = image.type == Image.Type.Sliced && sprite.border != null;
            // 테두리(왼쪽, 아래, 오른쪽, 위)는 그림 픽셀 그대로 두고 노드를 캔버스 배율로 키운다
            patch.PatchMarginLeft = sliced ? (int)sprite.border[0] : 0;
            patch.PatchMarginBottom = sliced ? (int)sprite.border[1] : 0;
            patch.PatchMarginRight = sliced ? (int)sprite.border[2] : 0;
            patch.PatchMarginTop = sliced ? (int)sprite.border[3] : 0;
            var r = rect;
            if (image.preserveAspect && sprite.rect.height > 0f && r.Size.Y > 0f)
            {
                // 비율을 지켜 안에 맞춘다 (가운데)
                float want = sprite.rect.width / sprite.rect.height, have = r.Size.X / r.Size.Y;
                if (have > want) { float w = r.Size.Y * want; r = new Rect2(r.Position.X + (r.Size.X - w) * 0.5f, r.Position.Y, w, r.Size.Y); }
                else { float h = r.Size.X / want; r = new Rect2(r.Position.X, r.Position.Y + (r.Size.Y - h) * 0.5f, r.Size.X, h); }
            }
            patch.Position = r.Position;
            patch.Size = scale > 0f ? r.Size / scale : r.Size;
            patch.Scale = new GVec2(scale, scale);
            patch.Modulate = color;
        }

        void SyncText(Text text, Rect2 rect, float scale, float alpha, bool interactive)
        {
            if (interactive && text.raycastTarget) AddHit(text, rect);
            if (text.supportRichText && text.text != null && (text.text.Contains("<size=") || text.text.Contains("<color=")))
            {
                SyncRichText(text, rect, scale, alpha);
                return;
            }
            var view = Acquire<Label>(text);
            var label = (Label)view.node;
            if (view.label == null)
            {
                view.label = new LabelSettings { Font = Fonts.Default };
                label.LabelSettings = view.label;
                label.ClipText = false;
            }
            string s = text.text ?? "";
            if (view.textCache != s) { view.textCache = s; label.Text = Plain(s); }
            label.Position = rect.Position;
            label.Size = rect.Size;
            var settings = view.label;
            // 글 전체를 감싼 크기 꼬리표(<size=22>)가 있으면 그 크기로 (상점 설명)
            int baseSize = text.fontSize;
            if (text.supportRichText && s.Length > 0 && s[0] == '<')
            {
                var m = SizeTag.Match(s);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int tagged)) baseSize = tagged;
            }
            int size = Mathf.Max(1, Mathf.RoundToInt(baseSize * scale));
            if (settings.FontSize != size) settings.FontSize = size;
            settings.FontColor = Tint(text, alpha);
            settings.LineSpacing = (text.lineSpacing - 1f) * size;
            bool bold = text.fontStyle == FontStyle.Bold || text.fontStyle == FontStyle.BoldAndItalic;
            settings.OutlineSize = bold ? Mathf.Max(1, size / 14) : 0;   // 굵은 글꼴 파일이 따로 없어 외곽선으로 굵게
            settings.OutlineColor = settings.FontColor;

            var shadow = text.GetComponent<Shadow>();
            if (shadow != null && shadow.enabled)
            {
                var sc = shadow.effectColor;
                settings.ShadowColor = new GColor(sc.r, sc.g, sc.b, sc.a * alpha);
                settings.ShadowOffset = new GVec2(shadow.effectDistance.x * scale, -shadow.effectDistance.y * scale);
                settings.ShadowSize = shadow is Outline ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(shadow.effectDistance.x) * scale * 2f)) : 1;
            }
            else settings.ShadowColor = new GColor(0f, 0f, 0f, 0f);

            int a = (int)text.alignment;
            label.HorizontalAlignment = (a % 3) == 0 ? HorizontalAlignment.Left : (a % 3) == 1 ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            label.VerticalAlignment = (a / 3) == 0 ? VerticalAlignment.Top : (a / 3) == 1 ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            label.AutowrapMode = text.horizontalOverflow == HorizontalWrapMode.Wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            label.ClipContents = text.verticalOverflow == VerticalWrapMode.Truncate;   // 칸을 넘는 줄은 자른다 (유니티의 Truncate)
        }

        // 크기·색 꼬리표가 섞인 글(상점 설명): 꼬리표를 BBCode 로 바꿔 RichTextLabel 로 그린다
        void SyncRichText(Text text, Rect2 rect, float scale, float alpha)
        {
            var view = Acquire<RichTextLabel>(text);
            var label = (RichTextLabel)view.node;
            int size = Mathf.Max(1, Mathf.RoundToInt(text.fontSize * scale));
            string key = text.text + "|" + size;
            if (view.textCache != key)
            {
                view.textCache = key;
                string bb = SizeAny.Replace(text.text, m => "[font_size=" + Mathf.Max(1, Mathf.RoundToInt(int.Parse(m.Groups[1].Value) * scale)) + "]");
                bb = bb.Replace("</size>", "[/font_size]").Replace("</color>", "[/color]").Replace("<b>", "[b]").Replace("</b>", "[/b]");
                bb = ColorAny.Replace(bb, "[color=$1]");
                label.BbcodeEnabled = true;
                label.ScrollActive = false;
                label.AddThemeFontOverride("normal_font", Fonts.Default);
                label.AddThemeFontSizeOverride("normal_font_size", size);
                label.Text = bb;
            }
            label.Position = rect.Position;
            label.Size = rect.Size;
            label.Modulate = Tint(text, alpha);
            int a = (int)text.alignment;
            label.VerticalAlignment = (a / 3) == 0 ? VerticalAlignment.Top : (a / 3) == 1 ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            label.HorizontalAlignment = (a % 3) == 0 ? HorizontalAlignment.Left : (a % 3) == 1 ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            label.AutowrapMode = text.horizontalOverflow == HorizontalWrapMode.Wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        }

        void SyncField(InputField field, Rect2 rect, float scale)
        {
            if (!views.TryGetValue(field, out var view))
            {
                var edit = new LineEdit { Flat = true, MouseFilter = Control.MouseFilterEnum.Stop, CaretBlink = true };
                edit.AddThemeFontOverride("font", Fonts.Default);
                edit.TextChanged += t => field.text = t;
                edit.TextSubmitted += t => { field.onSubmit.Invoke(t); field.onEndEdit.Invoke(t); };
                view = new View { node = edit };
                container.AddChild(edit);
                views[field] = view;
            }
            view.seen = frame;
            var node = (LineEdit)view.node;
            node.Visible = true;
            Place(node);
            node.Position = rect.Position;
            node.Size = rect.Size;
            node.Editable = field.interactable;
            node.Secret = field.contentType == InputField.ContentType.Password;
            node.MaxLength = field.characterLimit;
            int size = Mathf.Max(1, Mathf.RoundToInt((field.textComponent != null ? field.textComponent.fontSize : 24) * scale));
            node.AddThemeFontSizeOverride("font_size", size);
            if (node.Text != field.text) node.Text = field.text;
            // 입력 칸이 직접 글자를 그리므로 모형의 글자·안내 글자는 감춘다
            if (field.textComponent != null && views.TryGetValue(field.textComponent, out var tv)) tv.node.Visible = false;
            if (field.placeholder != null && views.TryGetValue(field.placeholder, out var pv)) pv.node.Visible = string.IsNullOrEmpty(field.text) && !node.HasFocus();
            if (field.isFocused && !node.HasFocus()) { node.GrabFocus(); field.DeactivateInputField(); }
        }

        /// <summary>화면 점 아래 맨 위의 그림과, 그것이 속한 누를 수 있는 UI(없으면 null).</summary>
        Selectable Pick(GVec2 point, out bool overUi)
        {
            overUi = false;
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                if (!hits[i].rect.HasPoint(point)) continue;
                overUi = true;
                var s = hits[i].graphic.GetComponentInParent<Selectable>();
                return s != null && s.IsInteractable() ? s : null;
            }
            return null;
        }

        void UpdateHover()
        {
            hovered = Pick(mouse, out bool over);
            EventSystem.PointerOverUi = over;
        }

        public override void _Input(InputEvent e)
        {
            if (e is InputEventMouseButton w && w.Pressed && (w.ButtonIndex == MouseButton.WheelUp || w.ButtonIndex == MouseButton.WheelDown))
            {
                wheel += w.ButtonIndex == MouseButton.WheelUp ? 1f : -1f;
                return;
            }
            if (!(e is InputEventMouseButton mb) || mb.ButtonIndex != MouseButton.Left) return;
            var target = Pick(mb.Position, out _);
            if (mb.Pressed) { pressed = target; return; }
            var was = pressed;
            pressed = null;
            if (was == null || was != target) return;
            switch (was)
            {
                case Button button: button.Press(); break;
                case Toggle toggle: toggle.isOn = !toggle.isOn; break;
            }
        }
    }
}
