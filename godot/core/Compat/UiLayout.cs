using System;
using System.Collections.Generic;

namespace Samkuk.Compat.UI
{
    /// <summary>
    /// UI 배치 계산 (G3): 캔버스 아래 모든 사각 트랜스폼의 화면상 사각형(<see cref="RectTransform.screenRect"/>)을 정한다.
    /// 유니티 uGUI 의 규칙을 이 게임이 쓰는 만큼만 흉내 낸다 — 앵커/오프셋, 가로·세로·격자 배치 그룹, 배치 요소의 최소/선호/유연 크기,
    /// 내용 크기 맞춤(세로), 비율 맞춤(부모 덮기). 화면 층은 계산된 사각형대로 Control 을 놓기만 한다.
    /// 좌표는 캔버스 기준 해상도의 픽셀, 원점은 왼쪽 아래, y 는 위가 +.
    /// </summary>
    public static class UiLayout
    {
        /// <summary>글자의 크기를 재는 곳 (화면 층이 실제 글꼴로 꽂는다). 없으면 글자 수로 어림한다.</summary>
        public static Func<Text, float, Vector2> MeasureText;

        static readonly List<RectTransform> kids = new List<RectTransform>();

        /// <summary>캔버스 하나를 배치한다. size 는 캔버스의 기준 크기(스케일러의 기준 해상도에 화면 비율을 맞춘 값).</summary>
        public static void Layout(Canvas canvas, Vector2 size)
        {
            var root = (RectTransform)canvas.transform;
            root.screenRect = new Rect(0f, 0f, size.x, size.y);
            LayoutChildren(root);
        }

        /// <summary>화면 크기에 맞춘 캔버스 기준 크기와 배율 (ScaleWithScreenSize 의 Expand/Match 규칙).</summary>
        public static Vector2 CanvasSize(Canvas canvas, Vector2 screen, out float scale)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scale = 1f;
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return screen;
            var r = scaler.referenceResolution;
            float sx = screen.x / Mathf.Max(1f, r.x), sy = screen.y / Mathf.Max(1f, r.y);
            switch (scaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand: scale = Mathf.Min(sx, sy); break;
                case CanvasScaler.ScreenMatchMode.Shrink: scale = Mathf.Max(sx, sy); break;
                default:
                    // 로그 공간에서 섞는다 (유니티와 같음)
                    scale = Mathf.Pow(2f, Mathf.Lerp(Log2(sx), Log2(sy), scaler.matchWidthOrHeight));
                    break;
            }
            return new Vector2(screen.x / scale, screen.y / scale);
        }

        static float Log2(float v) => Mathf.Log(Mathf.Max(1e-6f, v)) / Mathf.Log(2f);

        static void LayoutChildren(RectTransform parent)
        {
            var group = parent.GetComponent<LayoutGroup>();
            bool grouped = group != null && group.enabled;

            // 1) 그룹이 정하지 않는 자식: 앵커로
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = (RectTransform)parent.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (grouped && !Ignores(child)) continue;
                child.screenRect = Anchored(child, parent.screenRect);
                ApplyAspect(child, parent.screenRect);
            }

            // 2) 그룹이 정하는 자식
            if (grouped)
            {
                kids.Clear();
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = (RectTransform)parent.GetChild(i);
                    if (child.gameObject.activeSelf && !Ignores(child)) kids.Add(child);
                }
                var list = kids.ToArray();
                if (group is GridLayoutGroup grid) LayoutGrid(parent, grid, list);
                else if (group is HorizontalOrVerticalLayoutGroup hv) LayoutLine(parent, hv, list, group is HorizontalLayoutGroup);
            }

            // 3) 아래로
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = (RectTransform)parent.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                FitContent(child);
                LayoutChildren(child);
            }
        }

        static bool Ignores(RectTransform child)
        {
            var le = child.GetComponent<LayoutElement>();
            return le != null && le.enabled && le.ignoreLayout;
        }

        /// <summary>앵커와 오프셋으로 정해지는 사각형.</summary>
        public static Rect Anchored(RectTransform rt, Rect parent)
        {
            float x0 = parent.x + parent.width * rt.anchorMin.x + rt.offsetMin.x;
            float x1 = parent.x + parent.width * rt.anchorMax.x + rt.offsetMax.x;
            float y0 = parent.y + parent.height * rt.anchorMin.y + rt.offsetMin.y;
            float y1 = parent.y + parent.height * rt.anchorMax.y + rt.offsetMax.y;
            return new Rect(x0, y0, Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
        }

        static void ApplyAspect(RectTransform rt, Rect parent)
        {
            var fitter = rt.GetComponent<AspectRatioFitter>();
            if (fitter == null || !fitter.enabled || fitter.aspectRatio <= 0f) return;
            float a = fitter.aspectRatio;
            var r = rt.screenRect;
            switch (fitter.aspectMode)
            {
                case AspectRatioFitter.AspectMode.EnvelopeParent:
                case AspectRatioFitter.AspectMode.FitInParent:
                {
                    bool envelope = fitter.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent;
                    float w = parent.width, h = parent.width / a;
                    if ((h < parent.height) == envelope) { h = parent.height; w = h * a; }
                    r = new Rect(parent.x + (parent.width - w) * 0.5f, parent.y + (parent.height - h) * 0.5f, w, h);
                    break;
                }
                case AspectRatioFitter.AspectMode.WidthControlsHeight:
                {
                    float h = r.width / a;
                    r = new Rect(r.x, r.y + (r.height - h) * rt.pivot.y, r.width, h);
                    break;
                }
                case AspectRatioFitter.AspectMode.HeightControlsWidth:
                {
                    float w = r.height * a;
                    r = new Rect(r.x + (r.width - w) * rt.pivot.x, r.y, w, r.height);
                    break;
                }
            }
            rt.screenRect = r;
        }

        // 내용 크기 맞춤: 자기 배치 그룹(또는 글자)의 선호 크기로 줄이거나 늘린다 (피벗 자리는 그대로)
        static void FitContent(RectTransform rt)
        {
            var fitter = rt.GetComponent<ContentSizeFitter>();
            if (fitter == null || !fitter.enabled) return;
            var r = rt.screenRect;
            if (fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained)
            {
                float w = Preferred(rt, true, r.height);
                r = new Rect(r.x + (r.width - w) * rt.pivot.x, r.y, w, r.height);
            }
            if (fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained)
            {
                float h = Preferred(rt, false, r.width);
                r = new Rect(r.x, r.y + (r.height - h) * rt.pivot.y, r.width, h);
            }
            rt.screenRect = r;
        }

        /// <summary>한 축의 선호 크기: 배치 요소 값 → 배치 그룹의 내용 → 글자 → 자기 sizeDelta.</summary>
        public static float Preferred(RectTransform rt, bool horizontal, float crossSize)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le != null && le.enabled)
            {
                float p = horizontal ? le.preferredWidth : le.preferredHeight;
                if (p >= 0f) return p;
            }
            var group = rt.GetComponent<LayoutGroup>();
            if (group != null && group.enabled) return GroupPreferred(rt, group, horizontal, crossSize);
            var text = rt.GetComponent<Text>();
            if (text != null)
            {
                var size = Measure(text, horizontal ? 0f : crossSize);
                return horizontal ? size.x : size.y;
            }
            float own = horizontal ? rt.sizeDelta.x : rt.sizeDelta.y;
            return Mathf.Max(0f, own);
        }

        // 최소 크기: 배치 요소가 정했으면 그 값, 아니면 자기 배치 그룹의 최소(격자는 칸이 고정이라 선호 크기와 같다), 그 밖에는 0
        static float Min(RectTransform rt, bool horizontal)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le != null && le.enabled)
            {
                float m = horizontal ? le.minWidth : le.minHeight;
                if (m >= 0f) return m;
            }
            var group = rt.GetComponent<LayoutGroup>();
            if (group == null || !group.enabled) return 0f;
            if (group is GridLayoutGroup) return GroupPreferred(rt, group, horizontal, horizontal ? rt.screenRect.height : rt.screenRect.width);
            var hv = (HorizontalOrVerticalLayoutGroup)group;
            bool control = horizontal ? hv.childControlWidth : hv.childControlHeight;
            bool along = (group is HorizontalLayoutGroup) == horizontal;
            var children = ActiveChildren(rt);
            float total = 0f;
            foreach (var c in children)
            {
                float m = control ? Min(c, horizontal) : Mathf.Max(0f, horizontal ? c.sizeDelta.x : c.sizeDelta.y);
                total = along ? total + m : Mathf.Max(total, m);
            }
            if (along && children.Count > 1) total += hv.spacing * (children.Count - 1);
            return total + (horizontal ? group.padding.horizontal : group.padding.vertical);
        }

        static float Flexible(RectTransform rt, bool horizontal)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le == null || !le.enabled) return 0f;
            return Mathf.Max(0f, horizontal ? le.flexibleWidth : le.flexibleHeight);
        }

        static Vector2 Measure(Text text, float wrapWidth)
        {
            if (MeasureText != null) return MeasureText(text, wrapWidth);
            // 화면 층이 없을 때(테스트): 한글 한 자를 글자 크기만큼, 줄 높이는 1.2배로 어림
            string s = text.text ?? "";
            int lines = 1, longest = 0, current = 0;
            foreach (char c in s)
            {
                if (c == '\n') { lines++; longest = Mathf.Max(longest, current); current = 0; }
                else current++;
            }
            longest = Mathf.Max(longest, current);
            float w = longest * text.fontSize * 0.75f;
            if (wrapWidth > 0f && w > wrapWidth) { lines = Mathf.CeilToInt(lines * w / wrapWidth); w = wrapWidth; }
            return new Vector2(w, lines * text.fontSize * 1.2f);
        }

        static List<RectTransform> ActiveChildren(RectTransform parent)
        {
            var list = new List<RectTransform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = (RectTransform)parent.GetChild(i);
                if (child.gameObject.activeSelf && !Ignores(child)) list.Add(child);
            }
            return list;
        }

        static float GroupPreferred(RectTransform rt, LayoutGroup group, bool horizontal, float crossSize)
        {
            var children = ActiveChildren(rt);
            var pad = group.padding;
            float padding = horizontal ? pad.horizontal : pad.vertical;
            if (group is GridLayoutGroup grid)
            {
                if (children.Count == 0) return padding;
                float inner = crossSize - (horizontal ? pad.vertical : pad.horizontal);
                GridCounts(grid, children.Count, horizontal ? 0f : inner, horizontal ? inner : 0f, out int cols, out int rows);
                return horizontal ? padding + cols * grid.cellSize.x + (cols - 1) * grid.spacing.x : padding + rows * grid.cellSize.y + (rows - 1) * grid.spacing.y;
            }
            var hv = (HorizontalOrVerticalLayoutGroup)group;
            bool along = (group is HorizontalLayoutGroup) == horizontal;   // 재는 축이 늘어놓는 축인가
            float total = 0f;
            foreach (var c in children)
            {
                float p = ChildSize(c, hv, horizontal, crossSize);
                total = along ? total + p : Mathf.Max(total, p);
            }
            if (along && children.Count > 1) total += hv.spacing * (children.Count - 1);
            return total + padding;
        }

        // 그룹이 크기를 정하면(childControl) 선호 크기, 아니면 자식이 가진 크기
        static float ChildSize(RectTransform child, HorizontalOrVerticalLayoutGroup group, bool horizontal, float crossSize)
        {
            bool control = horizontal ? group.childControlWidth : group.childControlHeight;
            if (!control) return Mathf.Max(0f, horizontal ? child.sizeDelta.x : child.sizeDelta.y);
            return Mathf.Max(Min(child, horizontal), Preferred(child, horizontal, crossSize));
        }

        static void LayoutLine(RectTransform parent, HorizontalOrVerticalLayoutGroup group, RectTransform[] children, bool horizontalLine)
        {
            var area = parent.screenRect;
            var pad = group.padding;
            float innerW = Mathf.Max(0f, area.width - pad.horizontal), innerH = Mathf.Max(0f, area.height - pad.vertical);
            float innerMain = horizontalLine ? innerW : innerH, innerCross = horizontalLine ? innerH : innerW;
            bool controlMain = horizontalLine ? group.childControlWidth : group.childControlHeight;
            bool controlCross = horizontalLine ? group.childControlHeight : group.childControlWidth;
            bool expandMain = horizontalLine ? group.childForceExpandWidth : group.childForceExpandHeight;
            bool expandCross = horizontalLine ? group.childForceExpandHeight : group.childForceExpandWidth;

            int n = children.Length;
            if (n == 0) return;
            var main = new float[n];
            float total = group.spacing * (n - 1), flexSum = 0f;
            for (int i = 0; i < n; i++)
            {
                main[i] = ChildSize(children[i], group, horizontalLine, innerCross);
                total += main[i];
                flexSum += controlMain ? Flexible(children[i], horizontalLine) : 0f;
            }

            // 남는 자리: 유연 크기 비율로(없고 강제 확장이면 똑같이), 모자라면 그룹이 크기를 정하는 자식을 비례해 줄인다
            float extra = innerMain - total;
            float slack = 0f;   // 정렬에 쓸 남는 자리
            if (extra > 0f)
            {
                if (flexSum > 0f) for (int i = 0; i < n; i++) main[i] += extra * Flexible(children[i], horizontalLine) / flexSum;
                else if (expandMain && controlMain) for (int i = 0; i < n; i++) main[i] += extra / n;
                else if (expandMain) slack = extra;   // 크기는 그대로, 사이를 벌린다(유니티는 칸을 나누고 그 안에 정렬) — 여기서는 고르게 띄운다
                else slack = extra;
            }
            else if (extra < 0f && controlMain)
            {
                // 자리가 모자라면 유니티처럼 모든 자식을 "최소 ↔ 선호" 사이의 같은 비율로 줄인다
                float totalMin = 0f, totalPref = 0f;
                var mins = new float[n];
                for (int i = 0; i < n; i++) { mins[i] = Mathf.Min(Min(children[i], horizontalLine), main[i]); totalMin += mins[i]; totalPref += main[i]; }
                float room = innerMain - group.spacing * (n - 1);
                float k = totalPref > totalMin ? Mathf.Clamp01((room - totalMin) / (totalPref - totalMin)) : 0f;
                for (int i = 0; i < n; i++) main[i] = mins[i] + (main[i] - mins[i]) * k;
            }

            int align = (int)group.childAlignment;
            float alongFactor = horizontalLine ? (align % 3) * 0.5f : (align / 3) * 0.5f;   // 0 시작, 0.5 가운데, 1 끝
            float crossFactor = horizontalLine ? (align / 3) * 0.5f : (align % 3) * 0.5f;
            float gap = group.spacing;
            float offset = 0f;
            if (slack > 0f)
            {
                if (expandMain && !controlMain && n > 0) { gap += slack / n; offset = slack / n * alongFactor; }
                else offset = slack * alongFactor;
            }

            float cursor = offset;
            for (int i = 0; i < n; i++)
            {
                var child = children[i];
                float cross = controlCross
                    ? (expandCross ? innerCross : Mathf.Min(innerCross, Mathf.Max(Min(child, !horizontalLine), Preferred(child, !horizontalLine, main[i]))))
                    : Mathf.Max(0f, horizontalLine ? child.sizeDelta.y : child.sizeDelta.x);
                float crossOffset = (innerCross - cross) * crossFactor;
                if (horizontalLine)
                {
                    // 가로: 왼쪽에서 오른쪽, 세로 정렬은 위가 0
                    float x = area.x + pad.left + cursor;
                    float yTop = area.y + area.height - pad.top - crossOffset;
                    child.screenRect = new Rect(x, yTop - cross, main[i], cross);
                }
                else
                {
                    // 세로: 위에서 아래
                    float yTop = area.y + area.height - pad.top - cursor;
                    float x = area.x + pad.left + crossOffset;
                    child.screenRect = new Rect(x, yTop - main[i], cross, main[i]);
                }
                cursor += main[i] + gap;
            }
        }

        static void GridCounts(GridLayoutGroup grid, int count, float innerW, float innerH, out int cols, out int rows)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                cols = Mathf.Max(1, grid.constraintCount);
                rows = Mathf.CeilToInt(count / (float)cols);
            }
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                rows = Mathf.Max(1, grid.constraintCount);
                cols = Mathf.CeilToInt(count / (float)rows);
            }
            else
            {
                // 폭에 들어가는 만큼
                cols = innerW > 0f ? Mathf.Max(1, Mathf.FloorToInt((innerW + grid.spacing.x + 0.001f) / (grid.cellSize.x + grid.spacing.x))) : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
                rows = Mathf.CeilToInt(count / (float)cols);
            }
        }

        static void LayoutGrid(RectTransform parent, GridLayoutGroup grid, RectTransform[] children)
        {
            int n = children.Length;
            if (n == 0) return;
            var area = parent.screenRect;
            var pad = grid.padding;
            float innerW = Mathf.Max(0f, area.width - pad.horizontal), innerH = Mathf.Max(0f, area.height - pad.vertical);
            GridCounts(grid, n, innerW, innerH, out int cols, out int rows);

            float usedW = cols * grid.cellSize.x + (cols - 1) * grid.spacing.x, usedH = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y;
            int align = (int)grid.childAlignment;
            float offX = (innerW - usedW) * (align % 3) * 0.5f, offY = (innerH - usedH) * (align / 3) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                int col, row;
                if (grid.startAxis == GridLayoutGroup.Axis.Horizontal) { col = i % cols; row = i / cols; }
                else { row = i % rows; col = i / rows; }
                float x = area.x + pad.left + offX + col * (grid.cellSize.x + grid.spacing.x);
                float yTop = area.y + area.height - pad.top - offY - row * (grid.cellSize.y + grid.spacing.y);
                children[i].screenRect = new Rect(x, yTop - grid.cellSize.y, grid.cellSize.x, grid.cellSize.y);
            }
        }
    }

    /// <summary>유니티의 같은 이름: 화면 픽셀을 사각 트랜스폼의 로컬 좌표(피벗 기준, 캔버스 단위)로. 오버레이 캔버스만 다룬다(cam 은 쓰지 않는다).</summary>
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (rect == null) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            var screen = new Vector2(Screen.width, Screen.height);
            float scale = 1f;
            Vector2 size = canvas != null ? UiLayout.CanvasSize(canvas, screen, out scale) : screen;
            // 아직 배치 전이면(크기 0) 캔버스 전체로 본다
            Rect r = rect.screenRect.width > 0f && rect.screenRect.height > 0f ? rect.screenRect : new Rect(0f, 0f, size.x, size.y);
            float x = screenPoint.x / Mathf.Max(1e-6f, scale), y = screenPoint.y / Mathf.Max(1e-6f, scale);
            localPoint = new Vector2(x - (r.x + r.width * rect.pivot.x), y - (r.y + r.height * rect.pivot.y));
            return true;
        }
    }
}
