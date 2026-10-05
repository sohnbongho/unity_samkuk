using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

// 성 배경 그림(1920x1080, 불투명)을 GDI+ 로 그린다. 그리는 좌표는 1280x720 기준이고 Scale(1.5)배로 확대해 그려 선명하다. 평면 색 + 잉크 외곽선, 삼국지3 도시 화면처럼
// "하늘 - 먼 산 - 들판 - 성벽과 누각 - 길 - 앞쪽 나무" 순서로 겹쳐 그린다.
// 같은 성(id)은 항상 같은 그림이 나오도록 id 로 난수 씨앗을 정한다. (Windows PowerShell 5.1 = C# 5 문법만 사용)
public static class CastleArt
{
    public const int W = 1280, H = 720, Horizon = 392;   // 그리는 좌표계 (논리 크기)
    public const float Scale = 1.5f;                      // 실제 그림 = 논리 크기 x Scale (1920x1080)
    static readonly Color Ink = Color.FromArgb(255, 38, 28, 26);

    // ───────────────────────── 도구 ─────────────────────────

    static Color C(int r, int g, int b) { return Color.FromArgb(255, r, g, b); }
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

    // string.GetHashCode 는 실행마다 달라질 수 있으므로 직접 만든 FNV 해시를 쓴다
    static int Seed(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return (int)(h & 0x7fffffff);
    }

    static void Poly(Graphics g, Color c, float line, params float[] xy)
    {
        var pts = new PointF[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
        using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        if (line > 0) using (var p = new Pen(Ink, line)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, pts); }
    }

    static void Rect(Graphics g, Color c, float line, float x, float y, float w, float h)
    {
        using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
        if (line > 0) using (var p = new Pen(Ink, line)) g.DrawRectangle(p, x, y, w, h);
    }

    static void Ell(Graphics g, Color c, float line, float cx, float cy, float rx, float ry)
    {
        using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
        if (line > 0) using (var p = new Pen(Ink, line)) g.DrawEllipse(p, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    static void Line(Graphics g, Color c, float w, float x0, float y0, float x1, float y1)
    {
        using (var p = new Pen(c, w)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, x0, y0, x1, y1); }
    }

    // ───────────────────────── 지형 팔레트 ─────────────────────────

    class Pal
    {
        public Color Ground1, Ground2, Hill, Road;
        public float HillAmp;
        public bool Peaked;
    }

    static Pal PalFor(string terrain)
    {
        var p = new Pal();
        p.Road = C(214, 190, 140);
        switch (terrain)
        {
            case "Steppe":   p.Ground1 = C(196, 182, 112); p.Ground2 = C(150, 140, 82);  p.Hill = C(160, 150, 124); p.HillAmp = 24;  break;
            case "Loess":    p.Ground1 = C(218, 178, 108); p.Ground2 = C(172, 128, 72);  p.Hill = C(196, 154, 104); p.HillAmp = 58;  break;
            case "Mountain": p.Ground1 = C(122, 152, 98);  p.Ground2 = C(80, 110, 70);   p.Hill = C(96, 116, 138);  p.HillAmp = 175; p.Peaked = true; break;
            case "River":    p.Ground1 = C(144, 178, 112); p.Ground2 = C(94, 146, 84);   p.Hill = C(116, 154, 154); p.HillAmp = 44;  break;
            case "Jungle":   p.Ground1 = C(78, 144, 86);   p.Ground2 = C(42, 102, 62);   p.Hill = C(64, 116, 98);   p.HillAmp = 96;  p.Road = C(190, 160, 110); break;
            default:         p.Ground1 = C(152, 178, 94);  p.Ground2 = C(110, 148, 70);  p.Hill = C(112, 144, 122); p.HillAmp = 30;  break;
        }
        return p;
    }

    // ───────────────────────── 하늘 / 산 ─────────────────────────

    static void Sky(Graphics g, Random rng, int mood, string terrain, out Color skyBottom)
    {
        Color top, bot;
        if (mood == 1) { top = C(78, 72, 134); bot = C(246, 164, 104); }        // 해질녘
        else if (mood == 2) { top = C(112, 146, 200); bot = C(250, 218, 176); } // 새벽
        else { top = C(84, 150, 216); bot = C(212, 234, 246); }                 // 낮
        if (terrain == "Loess" || terrain == "Steppe") bot = Mix(bot, C(236, 208, 150), 0.35f); // 흙먼지
        if (terrain == "Jungle") { top = Mix(top, C(120, 150, 160), 0.35f); bot = Mix(bot, C(214, 226, 222), 0.4f); }
        skyBottom = bot;

        using (var b = new LinearGradientBrush(new Rectangle(0, 0, W, Horizon + 30), top, bot, LinearGradientMode.Vertical))
            g.FillRectangle(b, 0, 0, W, Horizon + 30);

        // 해(달)와 번지는 빛
        float sx = R(rng, 180, W - 180);
        float sy = mood == 0 ? R(rng, 70, 130) : Horizon - R(rng, 40, 90);
        Color sun = mood == 1 ? C(255, 214, 140) : C(255, 250, 220);
        for (int i = 4; i >= 1; i--) Ell(g, A(sun, 22), 0, sx, sy, 34 + i * 26, 34 + i * 26);
        Ell(g, sun, 0, sx, sy, mood == 1 ? 40 : 26, mood == 1 ? 40 : 26);

        // 구름
        int clouds = terrain == "Jungle" ? 9 : 5;
        Color cc = mood == 1 ? C(255, 190, 150) : C(255, 255, 255);
        for (int i = 0; i < clouds; i++)
        {
            float cx = R(rng, 0, W), cy = R(rng, 40, Horizon - 90), s = R(rng, 0.7f, 1.6f);
            for (int k = 0; k < 5; k++)
                Ell(g, A(cc, 150), 0, cx + k * 34 * s - 70 * s, cy + (k % 2 == 0 ? 0 : -10) * s, 46 * s, 17 * s);
        }
    }

    static float Tri(double t) { return 1f - (float)Math.Abs(Math.Sin(t)); }

    static void Ridge(Graphics g, Random rng, float baseY, float amp, bool peaked, Color col)
    {
        float p1 = R(rng, 0, 6.28f), p2 = R(rng, 0, 6.28f), p3 = R(rng, 0, 6.28f);
        float f1 = R(rng, 0.0035f, 0.007f), f2 = R(rng, 0.010f, 0.016f), f3 = R(rng, 0.025f, 0.04f);
        var pts = new List<PointF>();
        pts.Add(new PointF(-20, H));
        for (float x = -20; x <= W + 20; x += 14)
        {
            float n;
            if (peaked) n = 0.55f * Tri(x * f1 + p1) + 0.30f * Tri(x * f2 + p2) + 0.15f * Tri(x * f3 + p3);
            else n = 0.5f + 0.5f * (0.55f * (float)Math.Sin(x * f1 + p1) + 0.3f * (float)Math.Sin(x * f2 + p2) + 0.15f * (float)Math.Sin(x * f3 + p3));
            pts.Add(new PointF(x, baseY - amp * n));
        }
        pts.Add(new PointF(W + 20, H));
        using (var b = new SolidBrush(col)) g.FillPolygon(b, pts.ToArray());
        // 능선 쪽에 밝은 윗면을 얹어 덩어리감을 준다
        var lit = new List<PointF>();
        for (int i = 1; i < pts.Count - 1; i++) lit.Add(pts[i]);
        for (int i = pts.Count - 2; i >= 1; i--) lit.Add(new PointF(pts[i].X + 10, pts[i].Y + Math.Min(26, amp * 0.22f)));
        using (var b = new SolidBrush(A(Color.White, 34))) g.FillPolygon(b, lit.ToArray());
    }

    // ───────────────────────── 바닥(들판 / 강 / 길) ─────────────────────────

    static void Ground(Graphics g, Random rng, Pal pal, string terrain)
    {
        using (var b = new LinearGradientBrush(new Rectangle(0, Horizon - 4, W, H - Horizon + 4), pal.Ground1, pal.Ground2, LinearGradientMode.Vertical))
            g.FillRectangle(b, 0, Horizon - 4, W, H - Horizon + 4);

        // 원근감 있는 밭/풀밭 조각: 먼 곳은 작고 가까운 곳은 크게
        Color[] fields;
        if (terrain == "Loess") fields = new[] { C(214, 170, 96), C(190, 146, 82), C(226, 190, 118), C(168, 124, 70) };
        else if (terrain == "Steppe") fields = new[] { C(190, 176, 104), C(168, 158, 90), C(206, 192, 124), C(150, 142, 80) };
        else if (terrain == "Jungle") fields = new[] { C(66, 130, 78), C(52, 112, 70), C(84, 150, 90), C(40, 98, 60) };
        else if (terrain == "Mountain") fields = new[] { C(112, 144, 90), C(96, 128, 80), C(128, 156, 98), C(86, 116, 74) };
        else fields = new[] { C(150, 182, 90), C(126, 164, 78), C(206, 190, 98), C(104, 150, 74) };

        float y = Horizon;
        while (y < H)
        {
            float t = (y - Horizon) / (H - Horizon);
            float bh = 5 + t * 34;
            float x = -R(rng, 0, 60);
            while (x < W)
            {
                float bw = (28 + t * 150) * R(rng, 0.7f, 1.5f);
                Color fc = fields[rng.Next(fields.Length)];
                Rect(g, A(fc, 150), 0, x, y, bw, bh);
                x += bw;
            }
            using (var p = new Pen(A(Shade(pal.Ground2, 0.7f), 70), 1f)) g.DrawLine(p, 0, y, W, y);
            y += bh;
        }
    }

    static void Water(Graphics g, Random rng, bool wide, int mood, Color skyBottom)
    {
        float cy = wide ? 566 : 574, th = wide ? 104 : 58;
        float p1 = R(rng, 0, 6.28f), p2 = R(rng, 0, 6.28f);
        var top = new List<PointF>();
        var bot = new List<PointF>();
        for (float x = -20; x <= W + 20; x += 20)
        {
            top.Add(new PointF(x, cy - th / 2 + 12 * (float)Math.Sin(x * 0.007 + p1)));
            bot.Add(new PointF(x, cy + th / 2 + 14 * (float)Math.Sin(x * 0.0055 + p2)));
        }
        var pts = new List<PointF>(top);
        for (int i = bot.Count - 1; i >= 0; i--) pts.Add(bot[i]);
        Color deep = Mix(C(46, 108, 158), skyBottom, 0.18f), shallow = Mix(C(98, 164, 200), skyBottom, 0.3f);
        using (var b = new LinearGradientBrush(new Rectangle(0, (int)(cy - th), W, (int)(th * 2)), shallow, deep, LinearGradientMode.Vertical))
            g.FillPolygon(b, pts.ToArray());
        using (var p = new Pen(A(Ink, 160), 3f)) { g.DrawLines(p, top.ToArray()); g.DrawLines(p, bot.ToArray()); }

        // 반짝이는 물결
        for (int i = 0; i < 60; i++)
        {
            float x = R(rng, 0, W), yy = cy + R(rng, -th * 0.4f, th * 0.4f), len = R(rng, 14, 46);
            Line(g, A(Color.White, 120), 2f, x, yy, x + len, yy);
        }
        // 돛단배 (넓은 강일수록 많이)
        int boats = wide ? 3 : 1;
        for (int i = 0; i < boats; i++)
        {
            float bx = R(rng, 90, W - 90), by = cy + R(rng, -th * 0.15f, th * 0.3f), s = R(rng, 0.8f, 1.25f);
            Poly(g, C(104, 70, 48), 2.5f, bx - 30 * s, by - 6 * s, bx + 30 * s, by - 6 * s, bx + 20 * s, by + 8 * s, bx - 20 * s, by + 8 * s);
            Line(g, Ink, 3f, bx, by - 6 * s, bx, by - 52 * s);
            Poly(g, C(240, 228, 196), 2.5f, bx + 3 * s, by - 50 * s, bx + 26 * s, by - 14 * s, bx + 3 * s, by - 14 * s);
        }
    }

    // 길의 중심선과 반폭 (t = 0: 성문 앞, 1: 화면 아래)
    static float RoadX(float cx, float bend, float t) { return cx + bend * (float)Math.Sin(t * 3.0) * t; }
    static float RoadHalf(float gateW, float t) { return gateW * 0.28f + t * t * 200f; }

    static void Road(Graphics g, Pal pal, float cx, float by, float gateW, float bend, bool bridge, bool wide)
    {
        var l = new List<PointF>();
        var r = new List<PointF>();
        for (int i = 0; i <= 24; i++)
        {
            float t = i / 24f, y = by + t * (H + 8 - by);
            float x = RoadX(cx, bend, t), hw = RoadHalf(gateW, t);
            l.Add(new PointF(x - hw, y));
            r.Add(new PointF(x + hw, y));
        }
        var pts = new List<PointF>(l);
        for (int i = r.Count - 1; i >= 0; i--) pts.Add(r[i]);
        using (var b = new SolidBrush(pal.Road)) g.FillPolygon(b, pts.ToArray());
        using (var p = new Pen(A(Shade(pal.Road, 0.6f), 190), 3f)) { g.DrawLines(p, l.ToArray()); g.DrawLines(p, r.ToArray()); }
        for (int i = 2; i < 24; i += 2)   // 바퀴 자국/자갈
        {
            float t = i / 24f, y = by + t * (H - by), x = RoadX(cx, bend, t);
            Line(g, A(Shade(pal.Road, 0.82f), 200), 2f + t * 3, x - 10 - t * 40, y, x - 2 - t * 40, y + 2 + t * 6);
            Line(g, A(Shade(pal.Road, 0.82f), 200), 2f + t * 3, x + 10 + t * 40, y, x + 2 + t * 40, y + 2 + t * 6);
        }
        if (bridge)   // 물 위를 지나는 구간에 난간을 세운다
        {
            float cy = wide ? 566 : 574, th = wide ? 104 : 58;
            float t0 = (cy - th / 2 - 4 - by) / (H - by), t1 = (cy + th / 2 + 8 - by) / (H - by);
            for (int side = -1; side <= 1; side += 2)
            {
                float xa = RoadX(cx, bend, t0) + side * RoadHalf(gateW, t0), ya = by + t0 * (H - by);
                float xb = RoadX(cx, bend, t1) + side * RoadHalf(gateW, t1), yb = by + t1 * (H - by);
                Line(g, Ink, 5f, xa, ya - 12, xb, yb - 12);
                Line(g, C(184, 60, 48), 3f, xa, ya - 12, xb, yb - 12);
                for (int k = 0; k <= 6; k++)
                {
                    float f = k / 6f;
                    Line(g, Ink, 4f, xa + (xb - xa) * f, ya + (yb - ya) * f - 12, xa + (xb - xa) * f, ya + (yb - ya) * f + 2);
                }
            }
        }
    }

    // ───────────────────────── 나무 / 바위 ─────────────────────────

    static void RoundTree(Graphics g, float x, float y, float s, Color leaf)
    {
        Rect(g, C(96, 66, 44), 2.5f, x - 5 * s, y - 30 * s, 10 * s, 32 * s);
        Ell(g, Shade(leaf, 0.85f), 3f, x, y - 52 * s, 34 * s, 28 * s);
        Ell(g, leaf, 0, x - 8 * s, y - 60 * s, 22 * s, 17 * s);
        Ell(g, Shade(leaf, 1.15f), 0, x - 12 * s, y - 64 * s, 10 * s, 7 * s);
    }

    static void Pine(Graphics g, float x, float y, float s, Color leaf)
    {
        Rect(g, C(88, 60, 40), 2.5f, x - 4 * s, y - 14 * s, 8 * s, 16 * s);
        for (int i = 0; i < 3; i++)
        {
            float w = (34 - i * 8) * s, top = y - (30 + i * 26) * s, bot = y - (6 + i * 22) * s;
            Poly(g, Shade(leaf, 1f - i * 0.08f), 3f, x - w, bot, x + w, bot, x, top);
        }
    }

    static void Palm(Graphics g, float x, float y, float s, Color leaf)
    {
        float lean = (x < W / 2 ? 1 : -1) * 16 * s;
        using (var p = new Pen(Ink, 11 * s)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawBezier(p, x, y, x + lean * 0.2f, y - 40 * s, x + lean * 0.8f, y - 80 * s, x + lean, y - 108 * s); }
        using (var p = new Pen(C(150, 112, 74), 6 * s)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawBezier(p, x, y, x + lean * 0.2f, y - 40 * s, x + lean * 0.8f, y - 80 * s, x + lean, y - 108 * s); }
        float tx = x + lean, ty = y - 108 * s;
        for (int i = 0; i < 7; i++)
        {
            double a = Math.PI * i / 6.0;   // 부채꼴로 펼친 뒤 끝이 처지게
            float ex = tx + (float)Math.Cos(a) * 58 * s, ey = ty - (float)Math.Sin(a) * 22 * s + 26 * s * (float)Math.Abs(Math.Cos(a));
            using (var p = new Pen(Ink, 9 * s)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawBezier(p, tx, ty, tx + (ex - tx) * 0.4f, ty - 30 * s, tx + (ex - tx) * 0.8f, ty - 14 * s, ex, ey); }
            using (var p = new Pen(leaf, 5 * s)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawBezier(p, tx, ty, tx + (ex - tx) * 0.4f, ty - 30 * s, tx + (ex - tx) * 0.8f, ty - 14 * s, ex, ey); }
        }
    }

    static void Rock(Graphics g, float x, float y, float s, Color c)
    {
        Poly(g, c, 3f, x - 36 * s, y, x - 28 * s, y - 20 * s, x - 6 * s, y - 32 * s, x + 20 * s, y - 24 * s, x + 38 * s, y - 4 * s, x + 30 * s, y + 4 * s);
        Poly(g, A(Color.White, 50), 0, x - 28 * s, y - 20 * s, x - 6 * s, y - 32 * s, x + 4 * s, y - 20 * s, x - 14 * s, y - 8 * s);
    }

    static void Tuft(Graphics g, float x, float y, float s, Color c)
    {
        for (int i = -2; i <= 2; i++)
            using (var p = new Pen(c, 2f * s + 1)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, x + i * 4 * s, y, x + i * 7 * s, y - (12 + (i * i % 3) * 4) * s); }
    }

    static void Props(Graphics g, Random rng, string terrain, Pal pal, float cx, float bend, float gateW, float waterTop, float waterBot)
    {
        int count = terrain == "Jungle" ? 34 : (terrain == "Mountain" ? 22 : (terrain == "Steppe" ? 12 : 20));
        var list = new List<float[]>();
        for (int i = 0; i < count; i++)
        {
            float y = R(rng, Horizon + 90, H + 10);
            float t = (y - 454) / (H - 454);
            float x = R(rng, -30, W + 30);
            if (t > 0 && Math.Abs(x - RoadX(cx, bend, Math.Max(0, t))) < RoadHalf(gateW, Math.Max(0, t)) + 70) continue; // 길을 비워 둔다
            if (y > waterTop && y < waterBot) continue; // 물 위에는 나무를 세우지 않는다
            list.Add(new float[] { x, y, rng.Next(100) });
        }
        list.Sort(delegate (float[] a, float[] b) { return a[1].CompareTo(b[1]); });
        Color leaf = terrain == "Jungle" ? C(52, 130, 74) : (terrain == "Loess" || terrain == "Steppe" ? C(150, 150, 70) : C(86, 150, 70));
        foreach (var p in list)
        {
            float s = 0.45f + (p[1] - Horizon) / (H - Horizon) * 1.25f;
            int k = (int)p[2];
            switch (terrain)
            {
                case "Mountain": if (k < 70) Pine(g, p[0], p[1], s, C(46, 100, 70)); else Rock(g, p[0], p[1], s, C(150, 144, 140)); break;
                case "Jungle": if (k < 45) Palm(g, p[0], p[1], s, C(54, 150, 76)); else if (k < 85) RoundTree(g, p[0], p[1], s, leaf); else Tuft(g, p[0], p[1], s, C(36, 96, 54)); break;
                case "Steppe": if (k < 55) Tuft(g, p[0], p[1], s * 1.4f, C(122, 116, 60)); else Rock(g, p[0], p[1], s * 0.9f, C(176, 164, 140)); break;
                case "Loess": if (k < 40) RoundTree(g, p[0], p[1], s * 0.8f, leaf); else if (k < 70) Rock(g, p[0], p[1], s, C(196, 160, 112)); else Tuft(g, p[0], p[1], s, C(140, 108, 60)); break;
                default: if (k < 55) RoundTree(g, p[0], p[1], s, leaf); else if (k < 75) Pine(g, p[0], p[1], s * 0.9f, C(60, 118, 72)); else Tuft(g, p[0], p[1], s, C(72, 120, 56)); break;
            }
        }
    }

    // 산악 지형: 성 양옆을 깎아지른 절벽이 감싼다
    static void Cliffs(Graphics g, Random rng)
    {
        for (int side = 0; side < 2; side++)
        {
            float edge = side == 0 ? -10 : W + 10, dir = side == 0 ? 1 : -1;
            float wTop = R(rng, 230, 330), wBot = R(rng, 150, 230);
            Color rock = C(104, 94, 92), lit = C(146, 132, 122);
            // 안쪽 가장자리: 아래는 넓고 위로 갈수록 좁아지는 울퉁불퉁한 절벽선
            var pts = new List<float>();
            pts.Add(edge); pts.Add(H + 10);
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                pts.Add(edge + dir * (wBot * (1 - t) + wTop * 0.35f * t + R(rng, -16, 16) + (float)Math.Sin(t * 3.1) * (wTop - wBot * 0.6f) * 0.5f));
                pts.Add(H + 10 - t * (H - 130));
            }
            pts.Add(edge); pts.Add(120);
            Poly(g, rock, 4f, pts.ToArray());
            // 밝은 면 / 균열
            for (int i = 0; i < 7; i++)
            {
                float yy = R(rng, 200, H - 20), xx = edge + dir * R(rng, 20, wBot * 0.8f);
                Poly(g, A(lit, 150), 0, xx, yy, xx + dir * R(rng, 30, 70), yy + R(rng, 20, 60), xx + dir * 10, yy + R(rng, 60, 110));
            }
        }
    }

    // ───────────────────────── 성 ─────────────────────────

    static void Roof(Graphics g, float cx, float y, float w, float h, Color col)
    {
        float e = w * 0.09f + 4, rw = w * 0.15f;
        float lx = cx - w / 2 - e, rx = cx + w / 2 + e, ty = y - e * 0.9f;
        using (var p = new GraphicsPath())
        {
            p.AddBezier(lx, ty, cx - w * 0.25f, y + e * 0.55f, cx + w * 0.25f, y + e * 0.55f, rx, ty);                   // 처마 아랫선
            p.AddBezier(rx, ty, cx + w * 0.36f, y - h * 0.12f, cx + w * 0.22f, y - h * 0.72f, cx + rw, y - h);           // 오른쪽 내림마루
            p.AddLine(cx + rw, y - h, cx - rw, y - h);
            p.AddBezier(cx - rw, y - h, cx - w * 0.22f, y - h * 0.72f, cx - w * 0.36f, y - h * 0.12f, lx, ty);           // 왼쪽 내림마루
            p.CloseFigure();
            using (var b = new SolidBrush(col)) g.FillPath(b, p);
            using (var pen = new Pen(Ink, 2.6f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
        }
        for (int i = -3; i <= 3; i++)   // 기와 골
            Line(g, A(Shade(col, 0.7f), 190), 1.4f, cx + i * rw * 0.62f, y - h + 3, cx + i * (w / 2) * 0.34f, y - 2);
        Rect(g, Shade(col, 0.6f), 2f, cx - rw - 3, y - h - 5, rw * 2 + 6, 7);      // 용마루
    }

    static float Hall(Graphics g, float cx, float baseY, float w, int floors, Color roof, Color wall)
    {
        float y = baseY;
        for (int f = 0; f < floors; f++)
        {
            float bh = Math.Max(14, w * 0.2f), bw = w * 0.84f;
            Rect(g, wall, 2.2f, cx - bw / 2, y - bh, bw, bh);
            int cols = 4;
            for (int i = 0; i <= cols; i++)
                Rect(g, C(176, 52, 44), 0, cx - bw / 2 + i * (bw - 5) / cols, y - bh, 5, bh);
            for (int i = 0; i < cols; i++)
                Rect(g, C(54, 40, 38), 0, cx - bw / 2 + 9 + i * (bw - 5) / cols, y - bh * 0.78f, (bw - 5) / cols - 13, bh * 0.5f);
            float rh = w * 0.30f;
            Roof(g, cx, y - bh, w, rh, roof);
            y = y - bh - rh * 0.88f;
            w *= 0.74f;
        }
        // 꼭대기 장식
        Line(g, Ink, 5f, cx, y + 2, cx, y - 14);
        Ell(g, C(232, 190, 84), 2f, cx, y - 16, 4, 4);
        return y;
    }

    static void Banner(Graphics g, float x, float y, float h, Color col, string text, Font font)
    {
        Line(g, Ink, 4f, x, y, x, y - h);
        float fw = h * 0.46f, fh = h * 0.56f, top = y - h + 2;
        Poly(g, col, 2.5f, x + 2, top, x + 2 + fw, top + 4, x + 2 + fw * 0.9f, top + fh * 0.5f, x + 2 + fw, top + fh - 4, x + 2, top + fh);
        if (font != null && text.Length > 0)
        {
            var fmt = new StringFormat(); fmt.Alignment = StringAlignment.Center; fmt.LineAlignment = StringAlignment.Center;
            using (var b = new SolidBrush(C(250, 236, 190)))
                g.DrawString(text.Substring(0, 1), font, b, new RectangleF(x + 2, top, fw * 0.9f, fh), fmt);
        }
    }

    static void Wall(Graphics g, Random rng, float x0, float x1, float yTop, float yBase, Color stone)
    {
        Poly(g, stone, 3.5f, x0 + 4, yTop, x1 - 4, yTop, x1 + 10, yBase, x0 - 10, yBase);
        for (float y = yTop + 10; y < yBase; y += 10)   // 벽돌 줄눈
        {
            Line(g, A(Shade(stone, 0.7f), 150), 1.2f, x0, y, x1, y);
            for (float x = x0 + ((int)(y / 10) % 2) * 9; x < x1; x += 18)
                Line(g, A(Shade(stone, 0.7f), 110), 1.2f, x, y, x, y + 10);
        }
        using (var b = new LinearGradientBrush(new RectangleF(x0, yBase - 22, x1 - x0, 22), A(Color.Black, 0), A(Color.Black, 70), LinearGradientMode.Vertical))
            g.FillRectangle(b, x0 - 10, yBase - 22, x1 - x0 + 20, 22);
        Rect(g, Shade(stone, 0.85f), 2.5f, x0 + 2, yTop - 5, x1 - x0 - 4, 8);   // 성가퀴 받침
        for (float x = x0 + 4; x < x1 - 12; x += 18)                              // 성가퀴
            Rect(g, Shade(stone, 1.05f), 2.2f, x, yTop - 16, 11, 12);
    }

    static void Tower(Graphics g, float cx, float yBase, float w, float h, Color stone, Color roof, Color wall)
    {
        Poly(g, stone, 3.5f, cx - w / 2 + 3, yBase - h, cx + w / 2 - 3, yBase - h, cx + w / 2 + 5, yBase, cx - w / 2 - 5, yBase);
        for (float y = yBase - h + 10; y < yBase; y += 10) Line(g, A(Shade(stone, 0.7f), 150), 1.2f, cx - w / 2, y, cx + w / 2, y);
        Rect(g, Shade(stone, 0.85f), 2.5f, cx - w / 2 - 2, yBase - h - 4, w + 4, 8);
        Hall(g, cx, yBase - h - 2, w * 1.05f, 1, roof, wall);
    }

    static void Castle(Graphics g, Random rng, int size, string name, string hanja, Font flagFont, Font plaqueFont,
        float cx, float by, Pal pal, Color skyBottom, out float gateW)
    {
        float[] wallW = { 300, 420, 560, 720 }, wallH = { 62, 76, 90, 106 }, keepW = { 92, 120, 148, 190 }, towerW = { 46, 54, 62, 72 };
        int[] keepFloors = { 2, 3, 3, 4 };
        float ww = wallW[size], wh = wallH[size];
        bool imperial = size == 3;
        Color roof = imperial ? C(214, 164, 56) : C(70, 94, 98);
        Color wall = C(238, 224, 196), stone = C(176, 166, 150);
        gateW = 78 + size * 16;

        // 땅에 드리우는 그림자
        Ell(g, A(Color.Black, 56), 0, cx, by + 6, ww * 0.62f, 22);

        // 성 안쪽 건물 (성벽에 아랫부분이 가려진다)
        // 본성은 문루보다 한 뼘 뒤(위)에 세워 문루와 겹쳐 보이지 않게 한다
        float inner = by - wh + 16;
        Hall(g, cx - ww * 0.29f, inner, keepW[size] * 0.62f, size >= 2 ? 2 : 1, roof, wall);
        Hall(g, cx + ww * 0.29f, inner, keepW[size] * 0.62f, size >= 2 ? 2 : 1, roof, wall);
        float keepTop = Hall(g, cx, inner - 30, keepW[size], keepFloors[size], roof, wall);
        Color flagCol = imperial ? C(196, 40, 40) : C(46, 96, 168);
        Banner(g, cx, keepTop - 14, 70 + size * 8, flagCol, hanja, flagFont);

        // 성벽 / 모서리 망루 / 성문
        Wall(g, rng, cx - ww / 2, cx + ww / 2, by - wh, by, stone);
        Tower(g, cx - ww / 2, by, towerW[size], wh + 24, Shade(stone, 1.04f), roof, wall);
        Tower(g, cx + ww / 2, by, towerW[size], wh + 24, Shade(stone, 1.04f), roof, wall);
        Banner(g, cx - ww / 2, by - wh - 24 - towerW[size] * 0.9f, 52, flagCol, "", null);
        Banner(g, cx + ww / 2, by - wh - 24 - towerW[size] * 0.9f, 52, flagCol, "", null);

        float gw = gateW;
        Rect(g, Shade(stone, 0.92f), 3.5f, cx - gw / 2, by - wh - 8, gw, wh + 8);               // 문루 기단
        using (var p = new GraphicsPath())                                                        // 아치형 문
        {
            float aw = gw * 0.52f, ah = wh * 0.82f;
            p.AddLine(cx - aw / 2, by, cx - aw / 2, by - ah + aw / 2);
            p.AddArc(cx - aw / 2, by - ah, aw, aw, 180, 180);
            p.AddLine(cx + aw / 2, by - ah + aw / 2, cx + aw / 2, by);
            p.CloseFigure();
            using (var b = new SolidBrush(C(120, 36, 32))) g.FillPath(b, p);
            using (var pen = new Pen(Ink, 3f)) g.DrawPath(pen, p);
            Line(g, Ink, 2.5f, cx, by, cx, by - ah + 4);                                          // 문짝 사이
            for (int i = 0; i < 3; i++)
                for (int s = -1; s <= 1; s += 2)
                    Ell(g, C(232, 190, 84), 0, cx + s * aw * 0.22f, by - 10 - i * 12, 2.2f, 2.2f);
        }
        float hallBase = by - wh - 8;
        Hall(g, cx, hallBase, gw * 1.12f, 1, roof, wall);

        // 문루 현판(성 이름)
        float px = cx, py = by - wh * 0.9f - 6;
        Rect(g, C(58, 40, 34), 2.5f, px - 22, py - 14, 44, 22);
        var fmt = new StringFormat(); fmt.Alignment = StringAlignment.Center; fmt.LineAlignment = StringAlignment.Center;
        using (var b = new SolidBrush(C(236, 196, 92)))
            g.DrawString(hanja, plaqueFont, b, new RectangleF(px - 22, py - 14, 44, 22), fmt);
    }

    // ───────────────────────── 한 장 그리기 ─────────────────────────

    static string PickFont(params string[] names)
    {
        var have = new HashSet<string>();
        foreach (var f in FontFamily.Families) have.Add(f.Name);
        foreach (var n in names) if (have.Contains(n)) return n;
        return "Arial";
    }

    public static Bitmap Render(string id, string name, string hanja, string terrain, int size, bool water)
    {
        var rng = new Random(Seed(id));
        var pal = PalFor(terrain);
        int mood = rng.Next(3);
        var bmp = new Bitmap((int)(W * Scale), (int)(H * Scale), PixelFormat.Format32bppArgb);
        string fontName = PickFont("KaiTi", "SimSun", "Microsoft JhengHei", "Microsoft YaHei", "Malgun Gothic");

        using (var g = Graphics.FromImage(bmp))
        using (var flagFont = new Font(fontName, 22f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var plaqueFont = new Font(fontName, 17f, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(Scale, Scale);   // 이후 모든 그리기는 1280x720 좌표로 하면 된다
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            Color skyBottom;
            Sky(g, rng, mood, terrain, out skyBottom);

            // 먼 산 3겹: 멀수록 하늘색에 가깝게(대기 원근)
            Color hazy = Mix(pal.Hill, skyBottom, 0.55f);
            Ridge(g, rng, Horizon - 30, pal.HillAmp * 1.15f, pal.Peaked, Mix(pal.Hill, skyBottom, 0.62f));
            Ridge(g, rng, Horizon - 8, pal.HillAmp * 0.8f, pal.Peaked, hazy);
            Ridge(g, rng, Horizon + 12, pal.HillAmp * 0.5f, pal.Peaked, Mix(pal.Hill, pal.Ground1, 0.35f));

            Ground(g, rng, pal, terrain);
            // 지평선 안개
            using (var b = new LinearGradientBrush(new Rectangle(0, Horizon - 20, W, 130), A(skyBottom, 170), A(skyBottom, 0), LinearGradientMode.Vertical))
                g.FillRectangle(b, 0, Horizon - 20, W, 130);

            if (water || terrain == "River") Water(g, rng, terrain == "River", mood, skyBottom);

            float cx = W / 2f + R(rng, -60, 60), by = Horizon + 66, bend = R(rng, -120, 120);
            if (terrain == "Mountain") Cliffs(g, rng);

            // 길을 먼저 깔고 성이 길 끝을 덮도록 성을 나중에 그린다
            float gateW = 78 + size * 16;
            Road(g, pal, cx, by, gateW, bend, water || terrain == "River", terrain == "River");
            Castle(g, rng, size, name, hanja, flagFont, plaqueFont, cx, by, pal, skyBottom, out gateW);
            bool river = water || terrain == "River", bigRiver = terrain == "River";
            Props(g, rng, terrain, pal, cx, bend, gateW, river ? (bigRiver ? 500 : 530) : -1, river ? (bigRiver ? 640 : 616) : -1);

            // 분위기 보정과 가장자리 어둡게
            if (mood == 1) using (var b = new SolidBrush(A(C(255, 120, 60), 40))) g.FillRectangle(b, 0, 0, W, H);
            if (mood == 2) using (var b = new SolidBrush(A(C(255, 190, 200), 26))) g.FillRectangle(b, 0, 0, W, H);
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new RectangleF(0, 0, W, H));
                using (var pb = new PathGradientBrush(path))
                {
                    pb.CenterColor = A(Color.Black, 0);
                    pb.SurroundColors = new[] { A(Color.Black, 110) };
                    pb.FocusScales = new PointF(0.62f, 0.55f);
                    g.FillRectangle(pb, 0, 0, W, H);
                }
            }
        }
        return bmp;
    }

    public static void Draw(string outPath, string id, string name, string hanja, string terrain, int size, bool water)
    {
        using (var bmp = Render(id, name, hanja, terrain, size, water))
            bmp.Save(outPath, ImageFormat.Png);
    }

    // 확인용 모아보기 시트
    public static void Sheet(string dir, string[] files, string outPath, int cols)
    {
        int cw = 320, ch = 180, pad = 8;
        int rows = (files.Length + cols - 1) / cols;
        using (var sheet = new Bitmap(cols * (cw + pad) + pad, rows * (ch + pad) + pad, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(C(30, 34, 48));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                for (int i = 0; i < files.Length; i++)
                    using (var img = Image.FromFile(Path.Combine(dir, files[i])))
                        g.DrawImage(img, pad + (i % cols) * (cw + pad), pad + (i / cols) * (ch + pad), cw, ch);
            }
            sheet.Save(outPath, ImageFormat.Png);
        }
    }
}
