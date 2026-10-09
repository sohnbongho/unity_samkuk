using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 전투 맵의 바닥 타일(이어 붙여도 이음새가 없음)과 지형 소품(나무, 바위 등, 투명 배경)을 GDI+ 로 그린다.
// 평면 색 + 잉크 외곽선(성 그림, 장수 그림과 같은 화풍). 소품은 "바닥에 닿는 점"이 아래쪽 가운데가 되게 그린다.
// 그리는 좌표계는 예전 크기(바닥 256, 나무 144x176 등)지만 결과는 도트 규격(PPU 32)으로 절반 크기로 축소해
// 반투명 없이·색 단계를 줄여·1픽셀 외곽선을 입힌다(PixelTools, HD-2D Step 14-3). 유닛 크기는 그대로다(바닥 128 = 4유닛).
// 기준표는 terrain.json (파일 이름, 그리는 방식 kind, 색). Windows PowerShell 5.1 = C# 5 문법만 사용.
public static class TerrainArt
{
    static readonly Color Ink = Color.FromArgb(255, 34, 26, 22);
    const int GroundSize = 256;        // 그리는 좌표계
    const int PixelScale = 2;          // 결과는 1/PixelScale (PPU 64 -> 32)
    const int PropLevels = 7;          // 소품 채널당 색 단계
    const int GroundLevels = 12;       // 바닥은 알갱이 질감이 남게 단계를 더 둔다

    // ───────────────────────── 도구 ─────────────────────────

    static Color Hex(string h) { return ColorTranslator.FromHtml(h); }
    static Color A(Color c, int a) { return Color.FromArgb(a, c.R, c.G, c.B); }
    static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
    static Color Shade(Color c, float f)
    {
        return Color.FromArgb(c.A, (int)Math.Min(255, c.R * f), (int)Math.Min(255, c.G * f), (int)Math.Min(255, c.B * f));
    }
    static float R(Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    static int Seed(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return (int)(h & 0x7fffffff);
    }

    static Color[] Colors(string[] hex)
    {
        var c = new Color[hex.Length];
        for (int i = 0; i < hex.Length; i++) c[i] = Hex(hex[i]);
        return c;
    }

    static void Shadow(Graphics g, float cx, float cy, float rx, float ry)
    {
        using (var b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    // 잉크 테두리를 먼저 모두 그린 뒤 색을 채워서, 겹친 도형이 하나의 덩어리 외곽선을 갖게 한다
    static void Ells(Graphics g, Color fill, float line, params float[] cxyrxry)
    {
        for (int i = 0; i + 3 < cxyrxry.Length; i += 4)
            using (var b = new SolidBrush(Ink)) g.FillEllipse(b, cxyrxry[i] - cxyrxry[i + 2] - line, cxyrxry[i + 1] - cxyrxry[i + 3] - line, (cxyrxry[i + 2] + line) * 2, (cxyrxry[i + 3] + line) * 2);
        for (int i = 0; i + 3 < cxyrxry.Length; i += 4)
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, cxyrxry[i] - cxyrxry[i + 2], cxyrxry[i + 1] - cxyrxry[i + 3], cxyrxry[i + 2] * 2, cxyrxry[i + 3] * 2);
    }

    static void Ell(Graphics g, Color fill, float cx, float cy, float rx, float ry)
    {
        using (var b = new SolidBrush(fill)) g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    static void Poly(Graphics g, Color fill, float line, PointF[] pts)
    {
        using (var b = new SolidBrush(fill)) g.FillPolygon(b, pts);
        if (line > 0) using (var p = new Pen(Ink, line)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, pts); }
    }

    static void Stroke(Graphics g, Color c, float w, bool outline, params PointF[] pts)
    {
        if (outline)
            using (var p = new Pen(Ink, w + 3.5f)) { p.StartCap = p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round; Draw(g, p, pts); }
        using (var p = new Pen(c, w)) { p.StartCap = p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round; Draw(g, p, pts); }
    }

    static void Draw(Graphics g, Pen p, PointF[] pts)
    {
        if (pts.Length == 2) g.DrawLine(p, pts[0], pts[1]);
        else g.DrawCurve(p, pts, 0.5f);
    }

    static PointF P(float x, float y) { return new PointF(x, y); }

    // 들쭉날쭉한 바위 모양 (바닥은 납작하게)
    static PointF[] RockShape(Random rng, float cx, float cy, float rx, float ry, int n)
    {
        var pts = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * 2 * i / n + R(rng, -0.12f, 0.12f);
            float k = R(rng, 0.78f, 1.0f);
            float y = cy + (float)Math.Sin(a) * ry * k;
            if (y > cy) y = cy + (y - cy) * 0.55f;   // 아래쪽은 눌러서 땅에 앉은 느낌
            pts[i] = P(cx + (float)Math.Cos(a) * rx * k, y);
        }
        return pts;
    }

    // ───────────────────────── 소품 ─────────────────────────

    static void Size(string kind, out int w, out int h)
    {
        switch (kind)
        {
            case "tree": w = 144; h = 176; break;
            case "pine": w = 112; h = 176; break;
            case "palm": w = 160; h = 192; break;
            case "willow": w = 176; h = 176; break;
            case "deadtree": w = 128; h = 160; break;
            case "bush": w = 96; h = 72; break;
            case "tuft": w = 56; h = 40; break;
            case "flowers": w = 64; h = 48; break;
            case "rock": w = 72; h = 56; break;
            case "boulder": w = 112; h = 88; break;
            case "bigrock": w = 176; h = 144; break;
            case "mossrock": w = 96; h = 72; break;
            case "mound": w = 160; h = 88; break;
            case "terrace": w = 192; h = 112; break;
            case "yurt": w = 144; h = 128; break;
            case "reed": w = 72; h = 112; break;
            case "boat": w = 144; h = 88; break;
            case "fern": w = 112; h = 88; break;
            case "stump": w = 64; h = 56; break;
            case "pond": w = 288; h = 192; break;
            case "banner": w = 56; h = 144; break;
            case "riverwater": w = 256; h = 128; break;
            case "riverbank": w = 256; h = 128; break;
            default: w = 96; h = 96; break;
        }
    }

    static void Tree(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 8;
        Shadow(g, cx, gy, w * 0.30f, h * 0.045f);
        Color bark = Color.FromArgb(255, 112, 78, 48);
        using (var b = new SolidBrush(Ink)) g.FillRectangle(b, cx - w * 0.07f - 2, h * 0.5f, w * 0.14f + 4, gy - h * 0.5f + 2);
        using (var b = new SolidBrush(bark)) g.FillRectangle(b, cx - w * 0.07f, h * 0.5f, w * 0.14f, gy - h * 0.5f);
        Ells(g, c[1], 3, cx, h * 0.38f, w * 0.40f, h * 0.30f, cx - w * 0.18f, h * 0.46f, w * 0.24f, h * 0.20f, cx + w * 0.2f, h * 0.45f, w * 0.24f, h * 0.20f);
        Ell(g, c[0], cx - w * 0.04f, h * 0.34f, w * 0.34f, h * 0.24f);
        Ell(g, c[0], cx - w * 0.2f, h * 0.44f, w * 0.2f, h * 0.15f);
        Ell(g, c[0], cx + w * 0.2f, h * 0.43f, w * 0.2f, h * 0.15f);
        Ell(g, A(Mix(c[0], Color.White, 0.35f), 150), cx - w * 0.12f, h * 0.25f, w * 0.14f, h * 0.08f);
    }

    static void Pine(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 8;
        Shadow(g, cx, gy, w * 0.28f, h * 0.04f);
        using (var b = new SolidBrush(Ink)) g.FillRectangle(b, cx - 8, gy - h * 0.2f, 16, h * 0.2f + 2);
        using (var b = new SolidBrush(Color.FromArgb(255, 100, 70, 44))) g.FillRectangle(b, cx - 5, gy - h * 0.2f, 10, h * 0.2f);
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float bottom = gy - h * 0.12f - i * h * 0.19f, top = bottom - h * 0.34f;
            float half = w * (0.46f - i * 0.085f);
            var pts = new[] { P(cx - half, bottom), P(cx - half * 0.55f, bottom - h * 0.03f), P(cx - half * 0.7f, bottom - h * 0.07f), P(cx, top),
                              P(cx + half * 0.7f, bottom - h * 0.07f), P(cx + half * 0.55f, bottom - h * 0.03f), P(cx + half, bottom) };
            Poly(g, Mix(c[1], c[0], t), 3, pts);
        }
    }

    static void Palm(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 8, lean = R(rng, -w * 0.12f, w * 0.12f);
        Shadow(g, cx, gy, w * 0.22f, h * 0.035f);
        var trunk = new[] { P(cx, gy), P(cx + lean * 0.3f, h * 0.65f), P(cx + lean * 0.8f, h * 0.42f), P(cx + lean, h * 0.3f) };
        using (var p = new Pen(Ink, 17)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawCurve(p, trunk, 0.5f); }
        using (var p = new Pen(c[1], 11)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawCurve(p, trunk, 0.5f); }
        float tx = cx + lean, ty = h * 0.3f;
        for (int i = 0; i < 8; i++)
        {
            double a = Math.PI * i / 7.0;
            float ex = tx + (float)Math.Cos(a) * w * 0.45f, ey = ty - (float)Math.Sin(a) * h * 0.1f + h * 0.2f * (float)Math.Abs(Math.Cos(a));
            var fr = new[] { P(tx, ty), P(tx + (ex - tx) * 0.5f, ty - h * 0.11f), P(ex, ey) };
            Stroke(g, i % 2 == 0 ? c[0] : Shade(c[0], 0.8f), 8, true, fr);
        }
        Ell(g, Color.FromArgb(255, 110, 70, 40), tx - 6, ty + 8, 7, 7);
        Ell(g, Color.FromArgb(255, 110, 70, 40), tx + 7, ty + 9, 7, 7);
    }

    static void Willow(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 8;
        Shadow(g, cx, gy, w * 0.3f, h * 0.045f);
        using (var b = new SolidBrush(Ink)) g.FillRectangle(b, cx - 11, h * 0.4f, 22, gy - h * 0.4f + 2);
        using (var b = new SolidBrush(c[1])) g.FillRectangle(b, cx - 8, h * 0.4f, 16, gy - h * 0.4f);
        Ells(g, c[0], 3, cx, h * 0.3f, w * 0.38f, h * 0.2f);
        Ell(g, c[0], cx, h * 0.3f, w * 0.38f, h * 0.2f);
        for (int i = 0; i < 17; i++)   // 늘어진 가지
        {
            float x = cx - w * 0.36f + i * w * 0.045f;
            float top = h * 0.3f + (float)Math.Sin(i * 0.5) * 6;
            float len = R(rng, h * 0.28f, h * 0.45f) * (1f - Math.Abs(i - 8f) / 14f);
            Stroke(g, i % 2 == 0 ? c[0] : Shade(c[0], 0.85f), 4, true, P(x, top), P(x + R(rng, -5, 5), top + len * 0.6f), P(x + R(rng, -7, 7), top + len));
        }
    }

    static void DeadTree(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 8;
        Shadow(g, cx, gy, w * 0.24f, h * 0.04f);
        var trunk = new[] { P(cx - 12, gy), P(cx - 8, h * 0.5f), P(cx - 5, h * 0.2f), P(cx + 4, h * 0.2f), P(cx + 9, h * 0.5f), P(cx + 13, gy) };
        Poly(g, c[0], 3.5f, trunk);
        Stroke(g, c[0], 6, true, P(cx - 4, h * 0.45f), P(cx - w * 0.28f, h * 0.3f), P(cx - w * 0.36f, h * 0.15f));
        Stroke(g, c[0], 5, true, P(cx + 4, h * 0.35f), P(cx + w * 0.26f, h * 0.22f), P(cx + w * 0.3f, h * 0.08f));
        Stroke(g, c[0], 4, true, P(cx, h * 0.22f), P(cx - 6, h * 0.1f), P(cx - 2, h * 0.02f + 4));
        Stroke(g, c[0], 3.5f, true, P(cx - w * 0.2f, h * 0.36f), P(cx - w * 0.3f, h * 0.4f));
    }

    static void Bush(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 6;
        Shadow(g, cx, gy, w * 0.4f, h * 0.07f);
        Ells(g, c[1], 3, cx, gy - h * 0.38f, w * 0.34f, h * 0.36f, cx - w * 0.24f, gy - h * 0.26f, w * 0.22f, h * 0.26f, cx + w * 0.24f, gy - h * 0.26f, w * 0.22f, h * 0.26f);
        Ell(g, c[0], cx - w * 0.02f, gy - h * 0.42f, w * 0.26f, h * 0.26f);
        Ell(g, c[0], cx - w * 0.24f, gy - h * 0.28f, w * 0.15f, h * 0.17f);
        Ell(g, c[0], cx + w * 0.24f, gy - h * 0.28f, w * 0.15f, h * 0.17f);
        Ell(g, A(Mix(c[0], Color.White, 0.3f), 140), cx - w * 0.1f, gy - h * 0.55f, w * 0.1f, h * 0.07f);
    }

    static void Tuft(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 4;
        for (int i = 0; i < 7; i++)
        {
            float x = cx - w * 0.24f + i * w * 0.08f, lean = (i - 3) * w * 0.035f, top = gy - R(rng, h * 0.55f, h * 0.9f);
            Stroke(g, i % 2 == 0 ? c[0] : c[1], 4, true, P(x, gy), P(x + lean * 0.4f, (gy + top) / 2), P(x + lean, top));
        }
    }

    static void Flowers(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 4;
        for (int i = 0; i < 5; i++)
        {
            float x = cx - w * 0.3f + i * w * 0.15f, top = gy - R(rng, h * 0.45f, h * 0.8f);
            Stroke(g, c[0], 3, true, P(x, gy), P(x + R(rng, -3, 3), top));
            Ell(g, Ink, x, top, 6.5f, 6.5f);
            Ell(g, c[1 + i % 2], x, top, 4.5f, 4.5f);
        }
    }

    static void Rock(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 6;
        Shadow(g, cx, gy - h * 0.06f, w * 0.42f, h * 0.08f);
        float cy = gy - h * 0.26f;   // 바위 아랫면이 그림자 위에 닿도록
        var shape = RockShape(rng, cx, cy, w * 0.42f, h * 0.34f, 9);
        Poly(g, c[0], 3.5f, shape);
        var hi = new[] { shape[5], shape[6], shape[7], P(cx - w * 0.05f, cy - h * 0.2f), P(cx - w * 0.2f, cy - h * 0.1f) };
        Poly(g, A(c[1], 170), 0, hi);
    }

    static void Boulder(Graphics g, int w, int h, Color[] c, Random rng)
    {
        Rock(g, w, h, c, rng);
        float cx = w / 2f, gy = h - 6;
        Stroke(g, A(Ink, 200), 2.5f, false, P(cx + w * 0.05f, gy - h * 0.62f), P(cx + w * 0.02f, gy - h * 0.42f), P(cx + w * 0.1f, gy - h * 0.25f));
        Stroke(g, A(Ink, 160), 2f, false, P(cx - w * 0.2f, gy - h * 0.3f), P(cx - w * 0.12f, gy - h * 0.2f));
    }

    static void BigRock(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float gy = h - 6;
        Shadow(g, w / 2f, gy - h * 0.05f, w * 0.46f, h * 0.07f);
        var big = RockShape(rng, w * 0.45f, gy - h * 0.28f, w * 0.38f, h * 0.34f, 10);
        Poly(g, c[0], 3.5f, big);
        var small = RockShape(rng, w * 0.78f, gy - h * 0.16f, w * 0.2f, h * 0.2f, 8);
        Poly(g, Shade(c[0], 1.1f), 3.5f, small);
        Poly(g, A(c[1], 170), 0, new[] { big[6], big[7], big[8], P(w * 0.42f, gy - h * 0.46f), P(w * 0.25f, gy - h * 0.32f) });
        Stroke(g, A(Ink, 200), 2.5f, false, P(w * 0.5f, gy - h * 0.7f), P(w * 0.46f, gy - h * 0.45f), P(w * 0.55f, gy - h * 0.2f));
        Stroke(g, A(Ink, 170), 2f, false, P(w * 0.3f, gy - h * 0.35f), P(w * 0.35f, gy - h * 0.15f));
    }

    static void MossRock(Graphics g, int w, int h, Color[] c, Random rng)
    {
        Rock(g, w, h, new[] { c[0], Mix(c[0], Color.White, 0.3f) }, rng);
        float cx = w / 2f, gy = h - 6;
        var moss = new[] { P(cx - w * 0.3f, gy - h * 0.55f), P(cx - w * 0.1f, gy - h * 0.8f), P(cx + w * 0.2f, gy - h * 0.75f), P(cx + w * 0.34f, gy - h * 0.5f),
                           P(cx + w * 0.1f, gy - h * 0.55f), P(cx - w * 0.05f, gy - h * 0.45f) };
        Poly(g, c[1], 2.5f, moss);
    }

    static void Mound(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 6;
        Shadow(g, cx, gy, w * 0.46f, h * 0.07f);
        using (var path = new GraphicsPath())
        {
            path.AddBezier(w * 0.04f, gy, w * 0.2f, h * 0.1f, w * 0.7f, h * 0.0f, w * 0.96f, gy);
            path.CloseFigure();
            using (var b = new SolidBrush(c[0])) g.FillPath(b, path);
            var state = g.Save();
            g.SetClip(path);
            using (var b = new SolidBrush(A(c[1], 150))) g.FillEllipse(b, w * 0.05f, h * 0.1f, w * 0.5f, h * 0.55f);
            for (int i = 0; i < 4; i++)
                using (var p = new Pen(A(Ink, 70), 2.5f)) g.DrawBezier(p, 0, gy - i * h * 0.17f, w * 0.3f, gy - i * h * 0.17f - 12, w * 0.7f, gy - i * h * 0.17f - 12, w, gy - i * h * 0.17f);
            g.Restore(state);
            using (var p = new Pen(Ink, 3.5f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
        }
    }

    static void Terrace(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 6;
        Shadow(g, cx, gy, w * 0.46f, h * 0.06f);
        float[] widths = { 0.92f, 0.66f, 0.4f };
        for (int i = 0; i < 3; i++)
        {
            float half = w * widths[i] / 2f, bottom = gy - i * h * 0.3f, top = bottom - h * 0.3f;
            var pts = new[] { P(cx - half, bottom), P(cx - half + 8, top), P(cx + half - 8, top), P(cx + half, bottom) };
            Poly(g, i % 2 == 0 ? c[0] : c[1], 3.5f, pts);
            using (var p = new Pen(A(Ink, 90), 2f)) { g.DrawLine(p, cx - half + 4, bottom - h * 0.1f, cx + half - 4, bottom - h * 0.1f); g.DrawLine(p, cx - half + 6, bottom - h * 0.2f, cx + half - 6, bottom - h * 0.2f); }
        }
    }

    static void Yurt(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 6;
        Shadow(g, cx, gy, w * 0.44f, h * 0.06f);
        using (var b = new SolidBrush(Ink)) g.FillRectangle(b, w * 0.14f - 2, h * 0.5f - 2, w * 0.72f + 4, gy - h * 0.5f + 4);
        using (var b = new SolidBrush(c[0])) g.FillRectangle(b, w * 0.14f, h * 0.5f, w * 0.72f, gy - h * 0.5f);
        using (var p = new Pen(A(Ink, 70), 1.8f))
            for (int i = 0; i < 9; i++) { g.DrawLine(p, w * 0.14f + i * w * 0.09f, h * 0.5f, w * 0.14f + i * w * 0.09f + 10, gy); }
        Poly(g, c[1], 3.5f, new[] { P(w * 0.06f, h * 0.54f), P(cx, h * 0.08f), P(w * 0.94f, h * 0.54f), P(cx, h * 0.6f) });
        Ell(g, Ink, cx, h * 0.08f, 4, 4);
        using (var b = new SolidBrush(Color.FromArgb(255, 70, 46, 34))) g.FillRectangle(b, cx - 11, gy - h * 0.3f, 22, h * 0.3f);
    }

    static void Reed(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 4;
        for (int i = 0; i < 8; i++)
        {
            float x = cx - w * 0.32f + i * w * 0.09f, lean = (i - 3.5f) * 3.5f, top = gy - R(rng, h * 0.6f, h * 0.95f);
            Stroke(g, c[0], 3, true, P(x, gy), P(x + lean * 0.5f, (gy + top) / 2), P(x + lean, top));
            if (i % 2 == 0) { Ell(g, Ink, x + lean, top + 2, 5.5f, 11); Ell(g, c[1], x + lean, top + 2, 3.5f, 9); }
        }
    }

    static void Boat(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f;
        using (var b = new SolidBrush(Color.FromArgb(90, 120, 190, 220))) g.FillEllipse(b, w * 0.04f, h * 0.62f, w * 0.92f, h * 0.3f);
        Poly(g, c[0], 3.5f, new[] { P(w * 0.06f, h * 0.55f), P(w * 0.94f, h * 0.55f), P(w * 0.8f, h * 0.82f), P(w * 0.2f, h * 0.82f) });
        using (var p = new Pen(A(Ink, 110), 2f)) g.DrawLine(p, w * 0.12f, h * 0.64f, w * 0.88f, h * 0.64f);
        Stroke(g, Ink, 3.5f, false, P(cx, h * 0.55f), P(cx, h * 0.06f));
        Poly(g, c[1], 3, new[] { P(cx + 3, h * 0.1f), P(cx + w * 0.3f, h * 0.45f), P(cx + 3, h * 0.45f) });
    }

    static void Fern(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 4;
        Shadow(g, cx, gy, w * 0.3f, h * 0.05f);
        for (int i = 0; i < 9; i++)
        {
            double a = Math.PI * (0.08 + i * 0.105);
            float ex = cx + (float)Math.Cos(a) * w * 0.46f, ey = gy - (float)Math.Sin(a) * h * 0.88f;
            var frond = new[] { P(cx, gy), P(cx + (ex - cx) * 0.55f, gy - (gy - ey) * 0.75f), P(ex, ey) };
            Stroke(g, i % 2 == 0 ? c[0] : c[1], 6, true, frond);
        }
    }

    static void Stump(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, gy = h - 6;
        Shadow(g, cx, gy, w * 0.4f, h * 0.08f);
        using (var b = new SolidBrush(Ink)) g.FillRectangle(b, cx - w * 0.3f - 2, h * 0.4f, w * 0.6f + 4, gy - h * 0.4f);
        using (var b = new SolidBrush(c[0])) g.FillRectangle(b, cx - w * 0.3f, h * 0.4f, w * 0.6f, gy - h * 0.4f - 2);
        Ells(g, c[0], 3, cx, gy - 2, w * 0.3f, h * 0.1f);
        Ells(g, c[1], 3, cx, h * 0.4f, w * 0.3f, h * 0.14f);
        using (var p = new Pen(A(c[0], 200), 2f)) { g.DrawEllipse(p, cx - w * 0.18f, h * 0.4f - h * 0.08f, w * 0.36f, h * 0.16f); g.DrawEllipse(p, cx - w * 0.08f, h * 0.4f - h * 0.035f, w * 0.16f, h * 0.07f); }
    }

    static void Pond(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w / 2f, cy = h / 2f;
        int n = 16;
        var outer = new PointF[n];
        var inner = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * 2 * i / n;
            float k = R(rng, 0.82f, 1.0f);
            outer[i] = P(cx + (float)Math.Cos(a) * (w * 0.47f) * k, cy + (float)Math.Sin(a) * (h * 0.44f) * k);
            inner[i] = P(cx + (float)Math.Cos(a) * (w * 0.4f) * k, cy + (float)Math.Sin(a) * (h * 0.36f) * k);
        }
        using (var path = new GraphicsPath()) { path.AddClosedCurve(outer, 0.5f); using (var b = new SolidBrush(A(Mix(c[1], Color.Black, 0.35f), 200))) g.FillPath(b, path); }
        using (var path = new GraphicsPath()) { path.AddClosedCurve(inner, 0.5f); using (var b = new SolidBrush(c[0])) g.FillPath(b, path); }
        Ell(g, A(Mix(c[0], Color.White, 0.3f), 110), cx - w * 0.08f, cy - h * 0.06f, w * 0.22f, h * 0.15f);
        for (int i = 0; i < 6; i++)
        {
            float x = cx + R(rng, -w * 0.26f, w * 0.22f), y = cy + R(rng, -h * 0.2f, h * 0.2f), l = R(rng, 10, 26);
            using (var p = new Pen(A(Color.White, 130), 2f)) g.DrawLine(p, x, y, x + l, y);
        }
    }

    // 강 한 토막: 양 끝과 가장자리가 부드럽게 사라지는 길쭉한 타원. 여러 개를 겹쳐 놓으면 이어진 강이 된다.
    // 물(riverwater)은 푸른 중심 + 하얀 물결, 강둑(riverbank)은 더 넓고 흙색이라 물 밑에 깔려 물가가 된다.
    static void RiverSegment(Graphics g, int w, int h, Color[] c, Random rng, bool bank)
    {
        Color core = bank ? c[0] : c[0];
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(2, bank ? 2 : 14, w - 4, bank ? h - 4 : h - 28);
            using (var b = new PathGradientBrush(path))
            {
                b.CenterColor = A(core, bank ? 235 : 255);
                b.SurroundColors = new[] { A(core, 0) };
                b.FocusScales = bank ? new PointF(0.62f, 0.5f) : new PointF(0.7f, 0.5f);
                g.FillPath(b, path);
            }
        }
        if (bank) return;
        // 물 위의 빛나는 물결
        for (int i = 0; i < 3; i++)
        {
            float x = R(rng, w * 0.2f, w * 0.65f), y = R(rng, h * 0.34f, h * 0.66f), l = R(rng, 22, 56);
            using (var p = new Pen(Color.FromArgb(80, 255, 255, 255), 2.5f)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, x, y, x + l, y + R(rng, -3, 3)); }
        }
    }

    static void Banner(Graphics g, int w, int h, Color[] c, Random rng)
    {
        float cx = w * 0.3f, gy = h - 6;
        Shadow(g, cx, gy, w * 0.26f, h * 0.025f);
        using (var b = new SolidBrush(Ink)) g.FillRectangle(b, cx - 4, h * 0.06f, 8, gy - h * 0.06f + 2);
        using (var b = new SolidBrush(Color.FromArgb(255, 120, 86, 56))) g.FillRectangle(b, cx - 2.5f, h * 0.06f, 5, gy - h * 0.06f);
        Poly(g, c[0], 3, new[] { P(cx + 4, h * 0.1f), P(w * 0.92f, h * 0.14f), P(w * 0.8f, h * 0.24f), P(w * 0.94f, h * 0.34f), P(cx + 4, h * 0.38f) });
        Ell(g, Color.FromArgb(255, 232, 190, 84), cx, h * 0.05f, 5, 5);
        Poly(g, Color.FromArgb(255, 120, 120, 124), 2.5f, new[] { P(cx - 14, gy), P(cx - 10, gy - 12), P(cx + 10, gy - 12), P(cx + 14, gy) });
    }

    public static void DrawProp(string outPath, string kind, string[] colorHex, int seedOffset)
    {
        int w, h;
        Size(kind, out w, out h);
        var c = Colors(colorHex);
        var rng = new Random(Seed(Path.GetFileNameWithoutExtension(outPath)) + seedOffset);
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            switch (kind)
            {
                case "tree": Tree(g, w, h, c, rng); break;
                case "pine": Pine(g, w, h, c, rng); break;
                case "palm": Palm(g, w, h, c, rng); break;
                case "willow": Willow(g, w, h, c, rng); break;
                case "deadtree": DeadTree(g, w, h, c, rng); break;
                case "bush": Bush(g, w, h, c, rng); break;
                case "tuft": Tuft(g, w, h, c, rng); break;
                case "flowers": Flowers(g, w, h, c, rng); break;
                case "rock": Rock(g, w, h, c, rng); break;
                case "boulder": Boulder(g, w, h, c, rng); break;
                case "bigrock": BigRock(g, w, h, c, rng); break;
                case "mossrock": MossRock(g, w, h, c, rng); break;
                case "mound": Mound(g, w, h, c, rng); break;
                case "terrace": Terrace(g, w, h, c, rng); break;
                case "yurt": Yurt(g, w, h, c, rng); break;
                case "reed": Reed(g, w, h, c, rng); break;
                case "boat": Boat(g, w, h, c, rng); break;
                case "fern": Fern(g, w, h, c, rng); break;
                case "stump": Stump(g, w, h, c, rng); break;
                case "pond": Pond(g, w, h, c, rng); break;
                case "banner": Banner(g, w, h, c, rng); break;
                case "riverwater": RiverSegment(g, w, h, c, rng, false); break;
                case "riverbank": RiverSegment(g, w, h, c, rng, true); break;
                default: throw new ArgumentException("알 수 없는 kind: " + kind);
            }
            // 강물/강둑은 토막을 겹쳐 이어 붙이므로 가장자리가 흐린 채로(알파 단계만 줄임), 나머지는 반투명 없이 + 잉크 외곽선
            bool soft = kind == "riverwater" || kind == "riverbank";
            using (var small = PixelTools.Downscale(bmp, w / PixelScale, h / PixelScale))
            {
                PixelTools.Pixelize(small, PropLevels, !soft, 6, soft ? Color.FromArgb(0, 0, 0, 0) : Ink);
                small.Save(outPath, ImageFormat.Png);
            }
        }
    }

    // ───────────────────────── 바닥 타일 ─────────────────────────

    // 가장자리에 걸치는 도형이 반대편 가장자리에도 그려지게 9번 그려서 타일을 이어 붙여도 이음새가 없게 한다
    static void Wrap(Graphics g, Action draw)
    {
        for (int dx = -GroundSize; dx <= GroundSize; dx += GroundSize)
            for (int dy = -GroundSize; dy <= GroundSize; dy += GroundSize)
            {
                g.TranslateTransform(dx, dy);
                draw();
                g.ResetTransform();
            }
    }

    public static void DrawGround(string outPath, string baseHex, string[] accentHex, string detail)
    {
        var rng = new Random(Seed(Path.GetFileNameWithoutExtension(outPath)));
        Color bas = Hex(baseHex);
        var acc = Colors(accentHex);
        using (var bmp = new Bitmap(GroundSize, GroundSize, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(bas);

            // 큰 얼룩: 땅 색이 고르지 않은 느낌
            for (int i = 0; i < 18; i++)
            {
                float x = R(rng, 0, GroundSize), y = R(rng, 0, GroundSize), rx = R(rng, 26, 70), ry = R(rng, 20, 56);
                Color col = A(acc[rng.Next(acc.Length)], 80);
                Wrap(g, delegate { using (var b = new SolidBrush(col)) g.FillEllipse(b, x - rx, y - ry, rx * 2, ry * 2); });
            }

            int count = detail == "stone" ? 70 : 130;
            for (int i = 0; i < count; i++)
            {
                float x = R(rng, 0, GroundSize), y = R(rng, 0, GroundSize), s = R(rng, 0.7f, 1.3f);
                int variant = rng.Next();   // 9번 그려도 같은 모양이어야 이어 붙였을 때 이음새가 없다
                Color lite = A(Mix(acc[rng.Next(acc.Length)], Color.White, 0.18f), 190);
                Color dark = A(Shade(bas, 0.7f), 200);
                Wrap(g, delegate { GroundDetail(g, detail, x, y, s, lite, dark, variant); });
            }

            // 곱게 섞인 알갱이
            for (int i = 0; i < 700; i++)
            {
                int px = rng.Next(GroundSize), py = rng.Next(GroundSize);
                bmp.SetPixel(px, py, Color.FromArgb(255, Clamp(bas.R + rng.Next(-9, 10)), Clamp(bas.G + rng.Next(-9, 10)), Clamp(bas.B + rng.Next(-9, 10))));
            }
            // 도트 규격: 절반으로 줄이고 색 단계만 줄인다 (타일은 가장자리가 없어 외곽선/알파 처리 없음, 이음새는 그대로 유지된다)
            using (var small = PixelTools.Downscale(bmp, GroundSize / PixelScale, GroundSize / PixelScale))
            {
                PixelTools.Pixelize(small, GroundLevels, false, 1, Color.FromArgb(0, 0, 0, 0));
                small.Save(outPath, ImageFormat.Png);
            }
        }
    }

    static int Clamp(int v) { return Math.Max(0, Math.Min(255, v)); }

    static void GroundDetail(Graphics g, string detail, float x, float y, float s, Color lite, Color dark, int variant)
    {
        var r = new Random(variant);
        switch (detail)
        {
            case "dry":
                // 마른 풀 + 잔돌
                for (int k = 0; k < 3; k++)
                    using (var p = new Pen(k == 1 ? dark : lite, 1.6f)) g.DrawLine(p, x + k * 3, y, x + k * 3 + (k - 1) * 2, y - 7 * s);
                if (r.Next(5) == 0) using (var b = new SolidBrush(dark)) g.FillEllipse(b, x + 4, y + 2, 4 * s, 3 * s);
                break;
            case "stone":
                var pts = new[] { P(x, y), P(x + 6 * s, y - 3 * s), P(x + 12 * s, y), P(x + 9 * s, y + 4 * s), P(x + 2 * s, y + 4 * s) };
                using (var b = new SolidBrush(lite)) g.FillPolygon(b, pts);
                using (var p = new Pen(dark, 1.2f)) g.DrawPolygon(p, pts);
                break;
            case "wet":
                using (var p = new Pen(lite, 1.6f)) g.DrawArc(p, x, y, 16 * s, 6 * s, 200, 140);
                if (r.Next(4) == 0) using (var b = new SolidBrush(dark)) g.FillEllipse(b, x, y, 7 * s, 4 * s);
                break;
            case "jungle":
                using (var b = new SolidBrush(r.Next(2) == 0 ? lite : dark)) { g.TranslateTransform(x, y); g.RotateTransform(r.Next(180)); g.FillEllipse(b, 0, 0, 12 * s, 5 * s); g.ResetTransform(); }
                break;
            case "cracks":
                using (var p = new Pen(dark, 1.4f)) g.DrawCurve(p, new[] { P(x, y), P(x + 8 * s, y + 3 * s), P(x + 12 * s, y + 10 * s), P(x + 20 * s, y + 12 * s) }, 0.5f);
                if (r.Next(4) == 0) using (var b = new SolidBrush(lite)) g.FillEllipse(b, x + 6, y - 4, 4 * s, 3 * s);
                break;
            default:
                // 풀밭: 세 갈래 풀잎
                for (int k = 0; k < 3; k++)
                    using (var p = new Pen(k == 1 ? lite : dark, 1.8f)) g.DrawLine(p, x + k * 3, y, x + k * 3 + (k - 1) * 3, y - 8 * s);
                break;
        }
    }

    // 확인용 모아보기: 바닥은 3x3 으로 이어 붙여 이음새를 보고, 소품은 바닥색 위에 나열한다
    public static void Sheet(string dir, string[] groundFiles, string[][] propFiles, string outPath)
    {
        int cell = 200, pad = 10, cols = 8;
        int rows = groundFiles.Length;
        int sheetW = cols * (cell + pad) + pad;
        using (var sheet = new Bitmap(sheetW, rows * (cell + pad) + pad, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 26, 30, 40));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;   // 도트가 또렷하게 보이도록
            g.PixelOffsetMode = PixelOffsetMode.Half;
            for (int r = 0; r < rows; r++)
            {
                int y = pad + r * (cell + pad);
                using (var ground = Image.FromFile(Path.Combine(dir, groundFiles[r])))
                {
                    // 첫 칸: 바닥 타일 2x2 (이음새 확인), 나머지 칸의 배경도 같은 바닥
                    var tile = new Rectangle(pad, y, cell, cell);
                    for (int c = 0; c < cols; c++)
                    {
                        var rect = new Rectangle(pad + c * (cell + pad), y, cell, cell);
                        if (c == 0) { g.DrawImage(ground, new Rectangle(rect.X, rect.Y, cell / 2, cell / 2)); g.DrawImage(ground, new Rectangle(rect.X + cell / 2, rect.Y, cell / 2, cell / 2));
                                      g.DrawImage(ground, new Rectangle(rect.X, rect.Y + cell / 2, cell / 2, cell / 2)); g.DrawImage(ground, new Rectangle(rect.X + cell / 2, rect.Y + cell / 2, cell / 2, cell / 2)); }
                        else g.DrawImage(ground, rect);
                    }
                }
                for (int i = 0; i < propFiles[r].Length && i < cols - 1; i++)
                    using (var img = Image.FromFile(Path.Combine(dir, propFiles[r][i])))
                    {
                        float k = Math.Min((cell - 20f) / img.Width, (cell - 20f) / img.Height);
                        float dw = img.Width * k, dh = img.Height * k;
                        g.DrawImage(img, pad + (i + 1) * (cell + pad) + (cell - dw) / 2, y + (cell - dh) / 2, dw, dh);
                    }
            }
            sheet.Save(outPath, ImageFormat.Png);
        }
    }
}
