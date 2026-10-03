using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 장수 초상화(512x640, 투명 배경)를 GDI+ 로 그린다. 평면 색 + 잉크 외곽선 + 간단한 명암.
public static partial class HeroArt
{
    public const int W = 512, H = 640;
    static readonly Color Ink = Color.FromArgb(255, 36, 22, 20);
    static readonly Color Hair = Color.FromArgb(255, 43, 36, 32);
    static readonly Color Gold = Color.FromArgb(255, 232, 190, 84);
    const float BodyUp = -30f;
    static readonly Color Steel = Color.FromArgb(255, 196, 206, 218);

    // ───────────────────────── 도구 ─────────────────────────

    static Color Hex(string h) { return ColorTranslator.FromHtml(h); }

    static Color Shade(Color c, float f, int a)
    {
        return Color.FromArgb(a, (int)Math.Min(255, c.R * f), (int)Math.Min(255, c.G * f), (int)Math.Min(255, c.B * f));
    }
    static Color Shade(Color c, float f) { return Shade(c, f, c.A); }

    static PointF[] Pts(float[] xy)
    {
        var r = new PointF[xy.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
        return r;
    }

    static GraphicsPath Smooth(params float[] xy) { var p = new GraphicsPath(); p.AddClosedCurve(Pts(xy), 0.5f); return p; }
    static GraphicsPath Poly(params float[] xy) { var p = new GraphicsPath(); p.AddPolygon(Pts(xy)); return p; }
    static GraphicsPath Ell(float cx, float cy, float rx, float ry) { var p = new GraphicsPath(); p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2); return p; }

    static void Fill(Graphics g, GraphicsPath p, Color c, float line)
    {
        using (var b = new SolidBrush(c)) g.FillPath(b, p);
        if (line > 0)
            using (var pen = new Pen(Ink, line)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
    }
    static void Fill(Graphics g, GraphicsPath p, Color c) { Fill(g, p, c, 4.5f); }

    static void DrawOpen(Graphics g, Pen pen, PointF[] pts)
    {
        if (pts.Length == 2) g.DrawLine(pen, pts[0], pts[1]);
        else g.DrawCurve(pen, pts, 0.5f);
    }

    // 열린 부드러운 선 (외곽선 포함 여부 선택)
    static void Stroke(Graphics g, Color c, float w, bool outline, params float[] xy)
    {
        var pts = Pts(xy);
        if (outline)
            using (var pen = new Pen(Ink, w + 7)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; DrawOpen(g, pen, pts); }
        using (var pen = new Pen(c, w)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; DrawOpen(g, pen, pts); }
    }

    // 굵기가 변하는 곡선(눈썹, 수염, 깃털). 2차 베지어 p0 - c - p1.
    // leafW > 0 이면 양 끝이 뾰족하고 가운데가 가장 굵은 잎 모양.
    static void Taper(Graphics g, Color c, float w0, float w1, float leafW, float line,
        float x0, float y0, float cx, float cy, float x1, float y1)
    {
        const int N = 16;
        var left = new PointF[N + 1];
        var right = new PointF[N + 1];
        for (int i = 0; i <= N; i++)
        {
            float t = (float)i / N, u = 1 - t;
            float x = u * u * x0 + 2 * u * t * cx + t * t * x1;
            float y = u * u * y0 + 2 * u * t * cy + t * t * y1;
            float dx = 2 * u * (cx - x0) + 2 * t * (x1 - cx);
            float dy = 2 * u * (cy - y0) + 2 * t * (y1 - cy);
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) len = 1f;
            float nx = -dy / len, ny = dx / len;
            float w = leafW > 0 ? leafW * (float)Math.Sin(Math.PI * t) : w0 + (w1 - w0) * t;
            w *= 0.5f;
            left[i] = new PointF(x + nx * w, y + ny * w);
            right[i] = new PointF(x - nx * w, y - ny * w);
        }
        var all = new List<PointF>(left);
        for (int i = N; i >= 0; i--) all.Add(right[i]);
        var path = new GraphicsPath();
        path.AddPolygon(all.ToArray());
        Fill(g, path, c, line);
    }
    static void Taper(Graphics g, Color c, float w0, float w1, float x0, float y0, float cx, float cy, float x1, float y1)
    {
        Taper(g, c, w0, w1, 0, 2.5f, x0, y0, cx, cy, x1, y1);
    }

    // 꿩 깃: 잎 모양 + 가운데 줄기 + 가는 결
    static void Feather(Graphics g, Color c, Color vein, float maxW, float x0, float y0, float cx, float cy, float x1, float y1)
    {
        Taper(g, c, 0, 0, maxW, 4f, x0, y0, cx, cy, x1, y1);
        for (float t = 0.22f; t < 0.85f; t += 0.12f)
        {
            float u = 1 - t;
            float x = u * u * x0 + 2 * u * t * cx + t * t * x1, y = u * u * y0 + 2 * u * t * cy + t * t * y1;
            float dx = 2 * u * (cx - x0) + 2 * t * (x1 - cx), dy = 2 * u * (cy - y0) + 2 * t * (y1 - cy);
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
            float half = maxW * (float)Math.Sin(Math.PI * t) * 0.42f;
            Stroke(g, vein, 2.5f, false, x, y, x + nx * half + ux * 8, y + ny * half + uy * 8);
            Stroke(g, vein, 2.5f, false, x, y, x - nx * half + ux * 8, y - ny * half + uy * 8);
        }
        Taper(g, vein, 4, 1, 0, 0, x0, y0, cx, cy, x1, y1);
    }

    static void ClipFill(Graphics g, GraphicsPath clip, GraphicsPath shape, Color c)
    {
        g.SetClip(clip);
        using (var b = new SolidBrush(c)) g.FillPath(b, shape);
        g.ResetClip();
    }

    // ───────────────────────── 얼굴 부품 ─────────────────────────

    static GraphicsPath FacePath(float fw, float jw, float top, float chin)
    {
        return Smooth(256, top,
            256 + fw * 0.80f, top + 34,
            256 + fw, 222,
            256 + jw, 292,
            256, chin,
            256 - jw, 292,
            256 - fw, 222,
            256 - fw * 0.80f, top + 34);
    }

    static void Face(Graphics g, GraphicsPath p, Color skin, float fw)
    {
        Fill(g, p, skin, 0);
        ClipFill(g, p, Ell(256 + fw * 1.2f, 245, fw * 0.6f, 160), Shade(skin, 0.74f, 80));
        ClipFill(g, p, Ell(256 - fw * 0.58f, 266, 20, 11), Color.FromArgb(55, 230, 90, 80));
        ClipFill(g, p, Ell(256 + fw * 0.58f, 266, 20, 11), Color.FromArgb(55, 230, 90, 80));
        using (var pen = new Pen(Ink, 5f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
    }

    static void Ears(Graphics g, Color skin, float fw, float ry)
    {
        // 얼굴보다 먼저 그려 얼굴이 위에 덮이게 한다 (호출 순서로 보장)
    }

    static void EarPair(Graphics g, Color skin, float fw, float ry, float cy)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var ear = Ell(256 + s * (fw + 2), cy, 15, ry);
            Fill(g, ear, skin, 4.5f);
            ClipFill(g, ear, Ell(256 + s * (fw + 2), cy + 2, 7, ry * 0.6f), Shade(skin, 0.8f, 160));
        }
    }

    static void Eye(Graphics g, float cx, float cy, float w, float h, float tilt, bool left, Color iris)
    {
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(left ? tilt : -tilt);

        var eye = new GraphicsPath();
        eye.AddBezier(-w / 2, 0, -w / 4, -h, w / 4, -h, w / 2, 0);
        eye.AddBezier(w / 2, 0, w / 4, h * 0.75f, -w / 4, h * 0.75f, -w / 2, 0);
        using (var b = new SolidBrush(Hex("#FBF7EE"))) g.FillPath(b, eye);

        g.SetClip(eye);
        using (var b = new SolidBrush(iris)) g.FillEllipse(b, -h * 0.62f, -h * 0.66f, h * 1.24f, h * 1.24f);
        using (var b = new SolidBrush(Ink)) g.FillEllipse(b, -h * 0.32f, -h * 0.36f, h * 0.64f, h * 0.64f);
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, -h * 0.36f, -h * 0.46f, h * 0.28f, h * 0.28f);
        g.ResetClip();

        var top = new GraphicsPath();
        top.AddBezier(-w / 2, 0, -w / 4, -h, w / 4, -h, w / 2, 0);
        using (var pen = new Pen(Ink, 5.5f)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawPath(pen, top); }
        var bot = new GraphicsPath();
        bot.AddBezier(w / 2, 0, w / 4, h * 0.75f, -w / 4, h * 0.75f, -w / 2, 0);
        using (var pen = new Pen(Color.FromArgb(160, Ink), 2f)) g.DrawPath(pen, bot);
        g.Restore(st);
    }

    static void Eyes(Graphics g, float spacing, float y, float w, float h, float tilt, Color iris)
    {
        Eye(g, 256 - spacing, y, w, h, tilt, true, iris);
        Eye(g, 256 + spacing, y, w, h, tilt, false, iris);
    }

    // slant > 0 이면 안쪽이 낮아 화난 인상, < 0 이면 온화한 인상
    static void Brows(Graphics g, Color c, float gap, float y, float len, float slant, float arch, float thick)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            float xi = 256 + s * gap, yi = y + slant;
            float xo = 256 + s * (gap + len), yo = y - slant;
            Taper(g, c, thick, thick * 0.4f, xi, yi, (xi + xo) / 2, (yi + yo) / 2 - arch, xo, yo);
        }
    }

    static void Nose(Graphics g, Color skin, float y0, float size)
    {
        var c = Shade(skin, 0.62f);
        Stroke(g, c, 3.5f, false, 252, y0, 250, y0 + 22 * size, 257, y0 + 27 * size);
        Stroke(g, c, 3f, false, 262, y0 + 26 * size, 268, y0 + 22 * size);
    }

    static void Mouth(Graphics g, string type, float cy, float w)
    {
        var c = Hex("#7A2E2E");
        if (type == "smile") Stroke(g, c, 4f, false, 256 - w / 2, cy - 4, 256, cy + 6, 256 + w / 2, cy - 4);
        else if (type == "flat") Stroke(g, c, 4.5f, false, 256 - w / 2, cy, 256, cy + 2, 256 + w / 2, cy);
        else if (type == "smirk") Stroke(g, c, 4f, false, 256 - w / 2, cy + 2, 256, cy + 4, 256 + w / 2, cy - 6);
        else if (type == "roar")
        {
            var mouth = Smooth(256 - w / 2, cy, 256, cy - 8, 256 + w / 2, cy,
                256 + w * 0.38f, cy + 34, 256, cy + 44, 256 - w * 0.38f, cy + 34);
            Fill(g, mouth, Hex("#5A1414"), 4f);
            ClipFill(g, mouth, Poly(256 - w / 2, cy - 10, 256 + w / 2, cy - 10, 256 + w / 2, cy + 12, 256 - w / 2, cy + 12), Color.FromArgb(255, 245, 240, 230));
            ClipFill(g, mouth, Ell(256, cy + 36, w * 0.28f, 12), Hex("#C0525A"));
            using (var pen = new Pen(Ink, 4f)) g.DrawPath(pen, mouth);
        }
    }

    // ───────────────────────── 몸 ─────────────────────────

    static GraphicsPath BodyPath()
    {
        return Smooth(10, 700, 26, 556, 82, 462, 170, 414, 256, 404, 342, 414, 430, 462, 486, 556, 502, 700);
    }

    static void Torso(Graphics g, Color main, Color dark)
    {
        var body = BodyPath();
        Fill(g, body, main, 0);
        ClipFill(g, body, Ell(470, 580, 110, 220), Shade(dark, 1f, 120));
        ClipFill(g, body, Ell(256, 700, 150, 120), Shade(dark, 1f, 90));
        using (var pen = new Pen(Ink, 5f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, body); }
    }

    static void Neck(Graphics g, Color skin)
    {
        var neck = Smooth(224, 280, 288, 280, 298, 330, 296, 420, 216, 420, 214, 330);
        Fill(g, neck, Shade(skin, 0.86f), 4.5f);
        ClipFill(g, neck, Ell(256, 318, 70, 34), Color.FromArgb(90, 60, 30, 20));
    }

    static void Collar(Graphics g, Color inner, Color trim)
    {
        Fill(g, Poly(212, 404, 256, 482, 300, 404), inner, 4.5f);
        Stroke(g, trim, 15, true, 204, 402, 232, 450, 256, 488);
        Stroke(g, trim, 15, true, 308, 402, 280, 450, 256, 488);
    }

    static void Pauldron(Graphics g, bool left, Color main, Color ring, Color dark)
    {
        float sx = left ? -1 : 1;
        var st = g.Save();
        g.TranslateTransform(256 + sx * 170, 486);
        g.RotateTransform(sx * 22);
        Fill(g, Ell(0, 0, 70, 46), main, 5f);
        ClipFill(g, Ell(0, 0, 70, 46), Ell(10 * sx, 24, 80, 30), Shade(dark, 1f, 110));
        using (var pen = new Pen(ring, 6f)) g.DrawEllipse(pen, -50, -30, 100, 60);
        using (var pen = new Pen(Ink, 2.5f)) g.DrawEllipse(pen, -56, -36, 112, 72);
        g.Restore(st);
    }

    static void Medallion(Graphics g, float cx, float cy, float r, Color gold, Color core)
    {
        Fill(g, Ell(cx, cy, r, r), gold, 4.5f);
        Fill(g, Ell(cx, cy, r * 0.6f, r * 0.6f), core, 3f);
    }

    static void ScaleRows(Graphics g, Color c, float y0, float y1, float step)
    {
        var body = BodyPath();
        g.SetClip(body);
        using (var pen = new Pen(Color.FromArgb(150, c), 3f))
        {
            int row = 0;
            for (float y = y0; y < y1; y += step * 0.8f, row++)
            {
                float off = (row % 2) * step / 2;
                for (float x = 40 - step + off; x < 480; x += step)
                    g.DrawArc(pen, x, y, step, step * 0.9f, 0, 180);
            }
        }
        g.ResetClip();
    }

    // ───────────────────────── 무기/소품 ─────────────────────────

    static void HiltBehind(Graphics g, float x, float y, float dx, float dy, Color handle, Color guard)
    {
        // (x,y) = 날밑 위치, (dx,dy) = 손잡이 방향(위쪽으로)
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        float ux = dx / len, uy = dy / len;
        Stroke(g, handle, 15, true, x, y, x + dx, y + dy);
        Fill(g, Ell(x + dx, y + dy, 11, 11), guard, 4f);
        float gx = -uy * 24, gy = ux * 24;
        Stroke(g, guard, 10, true, x - gx, y - gy, x + gx, y + gy);
    }

    static void Glow(Graphics g, Color c)
    {
        using (var path = Ell(256, 280, 270, 300))
        using (var br = new PathGradientBrush(path))
        {
            br.CenterColor = Color.FromArgb(120, c);
            br.SurroundColors = new Color[] { Color.FromArgb(0, c) };
            g.FillPath(br, path);
        }
    }

    // ───────────────────────── 장수 ─────────────────────────

    static void LiuBei(Graphics g)
    {
        var robe = Hex("#3FB37F"); var dark = Hex("#1F7A55"); var skin = Hex("#F2CFA8"); var gold = Gold;
        Glow(g, Hex("#6FE3A5"));

        g.TranslateTransform(0, BodyUp);
        // 등 뒤의 쌍고검 손잡이
        HiltBehind(g, 142, 438, -34, -92, Hex("#8A3B34"), gold);
        HiltBehind(g, 370, 438, 34, -92, Hex("#8A3B34"), gold);

        Torso(g, robe, dark);
        Neck(g, skin);
        Collar(g, Hex("#F3EBD8"), gold);
        Stroke(g, gold, 6, false, 60, 560, 150, 520, 200, 470);
        Stroke(g, gold, 6, false, 452, 560, 362, 520, 312, 470);
        Medallion(g, 256, 560, 20, gold, Hex("#E15B4A"));

        g.ResetTransform();
        EarPair(g, skin, 72, 36, 238);
        var face = FacePath(72, 50, 120, 336);
        Face(g, face, skin, 72);

        Nose(g, skin, 226, 1f);
        Mouth(g, "smile", 296, 34);
        Taper(g, Hair, 9, 2, 254, 281, 228, 276, 206, 296);
        Taper(g, Hair, 9, 2, 258, 281, 284, 276, 306, 296);
        Fill(g, Smooth(243, 322, 256, 317, 269, 322, 264, 356, 256, 370, 248, 356), Hair, 3f);

        Eyes(g, 36, 228, 42, 18, -5, Hex("#3B2A20"));
        Brows(g, Hair, 18, 202, 44, -3, 8, 8);

        // 머리: 올림머리 + 금관
        Fill(g, Ell(256, 90, 27, 30), Hair, 4.5f);
        Fill(g, Smooth(180, 206, 178, 150, 206, 112, 256, 102, 306, 112, 334, 150, 332, 206, 304, 168, 256, 152, 208, 168), Hair, 4.5f);
        Taper(g, Hair, 14, 6, 190, 180, 184, 215, 186, 258);
        Taper(g, Hair, 14, 6, 322, 180, 328, 215, 326, 258);
        Fill(g, Smooth(228, 98, 284, 98, 288, 122, 256, 128, 224, 122), gold, 4f);
        Fill(g, Ell(256, 112, 7, 7), Hex("#E15B4A"), 2.5f);
        Stroke(g, gold, 5, true, 214, 90, 298, 90);
        Taper(g, Color.FromArgb(70, 255, 255, 255), 10, 3, 0, 0, 226, 124, 214, 114, 226, 104);
    }

    static void GuanYu(Graphics g)
    {
        var robe = Hex("#1F6F46"); var dark = Hex("#124A2E"); var skin = Hex("#D2674E"); var gold = Gold;
        Glow(g, Hex("#3BC878"));

        // 청룡언월도
        Stroke(g, Hex("#6B4A2A"), 12, true, 432, 700, 408, 120);
        var blade = Smooth(398, 170, 392, 90, 416, 34, 470, 18, 466, 62, 452, 98, 436, 138, 420, 168);
        Fill(g, blade, Hex("#C9D3DA"), 5f);
        ClipFill(g, blade, Ell(444, 120, 40, 60), Color.FromArgb(120, 80, 100, 120));
        Stroke(g, Hex("#2EA060"), 5, false, 404, 150, 420, 70, 462, 30);
        Fill(g, Ell(410, 176, 9, 9), Hex("#C0392B"), 3f);

        g.TranslateTransform(0, BodyUp);
        Torso(g, robe, dark);
        Neck(g, skin);
        Collar(g, Hex("#E7D9B8"), gold);
        Pauldron(g, true, Hex("#1A5C3B"), gold, dark);
        Pauldron(g, false, Hex("#1A5C3B"), gold, dark);
        Medallion(g, 256, 568, 24, gold, Hex("#1F6F46"));

        g.ResetTransform();
        EarPair(g, skin, 70, 28, 238);
        var face = FacePath(70, 52, 120, 338);
        Face(g, face, skin, 70);

        // 긴 수염
        var beard = Smooth(184, 252, 172, 330, 196, 432, 256, 530, 316, 432, 340, 330, 328, 252, 300, 300, 256, 312, 212, 300);
        Fill(g, beard, Hair, 5f);
        ClipFill(g, beard, Ell(330, 400, 60, 150), Color.FromArgb(70, 0, 0, 0));
        for (int i = -2; i <= 2; i++)
            Taper(g, Hex("#5A4C44"), 5, 1, 0, 0, 256 + i * 22, 322, 256 + i * 34, 410, 256 + i * 8, 500 - Math.Abs(i) * 24);
        Mouth(g, "flat", 296, 30);
        Taper(g, Hair, 13, 3, 254, 280, 220, 276, 196, 310);
        Taper(g, Hair, 13, 3, 258, 280, 292, 276, 316, 310);

        Nose(g, skin, 228, 1.05f);
        Eyes(g, 36, 226, 40, 11, 9, Hex("#2A1A14"));
        Brows(g, Hair, 16, 202, 54, 9, 2, 13);

        // 녹색 두건 + 상투
        Fill(g, Ell(256, 90, 24, 26), Hex("#2A8A55"), 4.5f);
        Fill(g, Smooth(172, 208, 170, 146, 208, 106, 256, 98, 304, 106, 342, 146, 340, 208, 304, 176, 256, 164, 208, 176), Hex("#2A8A55"), 5f);
        ClipFill(g, Smooth(172, 208, 170, 146, 208, 106, 256, 98, 304, 106, 342, 146, 340, 208, 304, 176, 256, 164, 208, 176), Ell(320, 150, 60, 80), Color.FromArgb(90, 10, 60, 30));
        Stroke(g, gold, 7, true, 176, 176, 256, 160, 336, 176);
        Fill(g, Ell(256, 164, 10, 10), Hex("#C0392B"), 3f);
    }

    static void ZhangFei(Graphics g)
    {
        var armor = Hex("#566A9C"); var dark = Hex("#36457A"); var skin = Hex("#CC9366"); var steel = Steel;
        Glow(g, Hex("#7F8CFF"));

        // 장팔사모
        Stroke(g, Hex("#4A3426"), 12, true, 66, 700, 112, 130);
        Fill(g, Poly(112, 6, 130, 52, 119, 70, 134, 108, 112, 128, 92, 108, 106, 70, 96, 52), steel, 4.5f);
        Stroke(g, Hex("#8892A4"), 3, false, 112, 24, 112, 108);
        Fill(g, Smooth(104, 134, 120, 134, 126, 176, 112, 190, 98, 176), Hex("#C0392B"), 4f);

        g.TranslateTransform(0, BodyUp);
        Torso(g, armor, dark);
        ScaleRows(g, dark, 470, 640, 30);
        Neck(g, skin);
        Collar(g, Hex("#2E3860"), steel);
        for (int s = -1; s <= 1; s += 2)
        {
            Fill(g, Poly(256 + s * 168, 440, 256 + s * 146, 386, 256 + s * 196, 428), steel, 4.5f);
        }
        Pauldron(g, true, Hex("#4A5C8C"), steel, dark);
        Pauldron(g, false, Hex("#4A5C8C"), steel, dark);
        Medallion(g, 256, 560, 24, steel, Hex("#C0392B"));

        g.ResetTransform();
        EarPair(g, skin, 82, 26, 240);
        var face = FacePath(82, 64, 124, 332);
        Face(g, face, skin, 82);

        // 덥수룩한 수염 (뾰족한 가장자리)
        var beard = Poly(188, 248, 172, 284, 158, 304, 178, 316, 168, 350, 198, 342, 202, 376, 226, 358, 240, 388,
            256, 364, 272, 388, 286, 358, 310, 376, 314, 342, 344, 350, 334, 316, 354, 304, 340, 284, 324, 248,
            302, 278, 256, 286, 210, 278);
        Fill(g, beard, Hair, 5f);
        Mouth(g, "roar", 298, 66);
        Taper(g, Hair, 14, 3, 254, 284, 214, 276, 190, 306);
        Taper(g, Hair, 14, 3, 258, 284, 298, 276, 322, 306);

        Nose(g, skin, 226, 1.2f);
        Eyes(g, 40, 226, 42, 22, 6, Hex("#2A1A14"));
        Brows(g, Hair, 16, 198, 56, 10, 2, 16);

        // 산발한 머리 + 붉은 머리띠
        var hairMass = Poly(172, 214, 166, 168, 180, 144, 174, 116, 204, 126, 208, 88, 232, 112, 256, 74, 280, 112, 304, 88, 308, 126, 338, 116, 332, 144, 346, 168, 340, 214,
            312, 176, 256, 162, 200, 176);
        Fill(g, hairMass, Hair, 5f);
        Stroke(g, Hex("#C0392B"), 17, true, 176, 178, 256, 164, 338, 178);
        Taper(g, Hex("#C0392B"), 14, 6, 334, 180, 372, 190, 384, 236);
        Taper(g, Hex("#C0392B"), 14, 6, 330, 184, 362, 210, 366, 262);
    }

    static void CaoCao(Graphics g)
    {
        var armor = Hex("#3A62D0"); var dark = Hex("#223C8F"); var skin = Hex("#ECCBA8"); var gold = Gold;
        Glow(g, Hex("#5C8DFF"));

        // 의천검 손잡이
        HiltBehind(g, 360, 436, 36, -96, Hex("#2A2F4A"), gold);

        g.TranslateTransform(0, BodyUp);
        // 망토
        var cape = Smooth(-14, 700, -2, 540, 56, 428, 160, 378, 256, 370, 352, 378, 456, 428, 514, 540, 526, 700);
        Fill(g, cape, Hex("#1B2757"), 5f);
        ClipFill(g, cape, Ell(60, 560, 60, 200), Color.FromArgb(90, 0, 0, 0));

        Torso(g, armor, dark);
        ScaleRows(g, dark, 450, 640, 28);
        Neck(g, skin);
        // 세운 깃
        Fill(g, Smooth(186, 322, 224, 312, 236, 436, 166, 452, 164, 380), Hex("#1B2757"), 4.5f);
        Fill(g, Smooth(326, 322, 288, 312, 276, 436, 346, 452, 348, 380), Hex("#1B2757"), 4.5f);
        Collar(g, Hex("#141C3E"), gold);
        Pauldron(g, true, Hex("#2F4FB0"), gold, dark);
        Pauldron(g, false, Hex("#2F4FB0"), gold, dark);
        Medallion(g, 256, 566, 22, gold, Hex("#1B2757"));

        g.ResetTransform();
        EarPair(g, skin, 66, 26, 238);
        var face = FacePath(66, 46, 124, 336);
        Face(g, face, skin, 66);

        Nose(g, skin, 228, 0.95f);
        Mouth(g, "smirk", 298, 32);
        Taper(g, Hair, 8, 1.5f, 254, 282, 224, 276, 196, 304);
        Taper(g, Hair, 8, 1.5f, 258, 282, 288, 276, 316, 304);
        Fill(g, Poly(246, 322, 266, 322, 256, 366), Hair, 3f);

        Eyes(g, 34, 228, 38, 12, 7, Hex("#2A1A14"));
        Brows(g, Hair, 14, 205, 44, 7, 3, 9);

        // 관: 검은 모자 + 금 장식
        var cap = Smooth(188, 176, 192, 134, 256, 114, 320, 134, 324, 176, 300, 152, 256, 146, 212, 152);
        Fill(g, cap, Hair, 5f);
        Fill(g, Poly(212, 130, 300, 130, 292, 84, 220, 84), Hair, 5f);
        Fill(g, Poly(216, 84, 296, 84, 306, 70, 206, 70), gold, 4.5f);
        Stroke(g, gold, 9, true, 206, 124, 306, 124);
        Fill(g, Ell(256, 124, 8, 8), Hex("#E15B4A"), 2.5f);
        Taper(g, Hair, 12, 5, 190, 168, 184, 210, 188, 258);
        Taper(g, Hair, 12, 5, 322, 168, 328, 210, 324, 258);
    }

    static void LuBu(Graphics g)
    {
        var armor = Hex("#C8452A"); var dark = Hex("#7A2418"); var skin = Hex("#E4B690"); var gold = Hex("#F2C14E");
        Glow(g, Hex("#FF9A4D"));

        // 방천화극
        Stroke(g, Hex("#4A2A1E"), 12, true, 74, 700, 88, 60);
        Fill(g, Poly(88, 4, 102, 56, 88, 76, 74, 56), Steel, 4.5f);
        var crescent = new GraphicsPath();
        crescent.AddBezier(88, 78, 34, 66, 6, 128, 34, 184);
        crescent.AddBezier(34, 184, 46, 140, 66, 122, 88, 120);
        crescent.CloseFigure();
        Fill(g, crescent, Steel, 4.5f);
        Fill(g, Poly(94, 112, 128, 104, 98, 134), Steel, 4.5f);
        Fill(g, Ell(88, 92, 9, 9), Hex("#C0392B"), 3f);

        g.TranslateTransform(0, BodyUp);
        // 붉은 망토
        var cloak = Smooth(-30, 700, -10, 520, 50, 400, 160, 366, 256, 358, 352, 366, 462, 400, 522, 520, 542, 700);
        Fill(g, cloak, Hex("#8E1F1F"), 5f);
        ClipFill(g, cloak, Ell(30, 560, 70, 220), Color.FromArgb(100, 40, 0, 0));
        ClipFill(g, cloak, Ell(482, 500, 40, 160), Color.FromArgb(60, 255, 120, 80));

        Torso(g, armor, dark);
        ScaleRows(g, dark, 450, 640, 28);
        Neck(g, skin);
        Collar(g, Hex("#3A1410"), gold);
        Pauldron(g, true, gold, Hex("#C8452A"), Hex("#9A6A18"));
        Pauldron(g, false, gold, Hex("#C8452A"), Hex("#9A6A18"));
        Medallion(g, 256, 566, 26, gold, Hex("#C0392B"));

        g.ResetTransform();
        EarPair(g, skin, 70, 26, 238);
        var face = FacePath(70, 54, 126, 336);
        Face(g, face, skin, 70);
        ClipFill(g, face, Smooth(206, 290, 256, 296, 306, 290, 296, 330, 256, 340, 216, 330), Color.FromArgb(70, 60, 30, 20));
        using (var pen = new Pen(Ink, 5f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, face); }

        Nose(g, skin, 228, 1.05f);
        Mouth(g, "flat", 302, 34);
        Taper(g, Hair, 9, 2, 254, 285, 226, 281, 204, 300);
        Taper(g, Hair, 9, 2, 258, 285, 286, 281, 308, 300);
        Eyes(g, 36, 228, 38, 15, 8, Hex("#3A1F14"));
        Brows(g, Hair, 16, 203, 50, 9, 2, 12);

        // 꿩 깃 투구
        Feather(g, Hex("#2E8C7A"), Hex("#0F4A40"), 44, 236, 104, 176, 72, 150, 2);
        Feather(g, Hex("#2E8C7A"), Hex("#0F4A40"), 44, 276, 104, 336, 72, 362, 2);
        var helm = Smooth(176, 200, 172, 142, 206, 100, 256, 88, 306, 100, 340, 142, 336, 200, 300, 164, 256, 154, 212, 164);
        Fill(g, helm, gold, 5f);
        ClipFill(g, helm, Ell(320, 140, 60, 80), Color.FromArgb(80, 120, 70, 10));
        Stroke(g, Hex("#C8452A"), 9, true, 180, 176, 256, 160, 332, 176);
        Fill(g, Ell(256, 128, 11, 11), Hex("#C0392B"), 3f);
        Fill(g, Smooth(172, 196, 184, 196, 190, 252, 176, 262, 168, 236), gold, 4.5f);
        Fill(g, Smooth(340, 196, 328, 196, 322, 252, 336, 262, 344, 236), gold, 4.5f);
    }

    // ───────────────────────── 출력 ─────────────────────────

    public static Bitmap Render(string name)
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            if (name == "Hero_LiuBei") LiuBei(g);
            else if (name == "Hero_GuanYu") GuanYu(g);
            else if (name == "Hero_ZhangFei") ZhangFei(g);
            else if (name == "Hero_CaoCao") CaoCao(g);
            else if (name == "Hero_LvBu") LuBu(g);
        }
        return bmp;
    }

    public static void Generate(string outDir, string sheetPath)
    {
        string[] names = { "Hero_LiuBei", "Hero_GuanYu", "Hero_ZhangFei", "Hero_CaoCao", "Hero_LvBu" };
        Directory.CreateDirectory(outDir);
        float scale = 0.62f;
        int cw = (int)(W * scale), ch = (int)(H * scale), pad = 14;
        var sheet = new Bitmap(names.Length * (cw + pad) + pad, ch + pad * 2, PixelFormat.Format32bppArgb);
        using (var sg = Graphics.FromImage(sheet))
        {
            sg.Clear(Color.FromArgb(255, 43, 51, 77));
            sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < names.Length; i++)
            {
                using (var bmp = Render(names[i]))
                {
                    bmp.Save(Path.Combine(outDir, names[i] + ".png"), ImageFormat.Png);
                    sg.DrawImage(bmp, pad + i * (cw + pad), pad, cw, ch);
                }
            }
        }
        sheet.Save(sheetPath, ImageFormat.Png);
        sheet.Dispose();
    }
}
