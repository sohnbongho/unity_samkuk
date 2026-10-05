using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 전략 지도 그림(2560x1440)을 GDI+ 로 그린다. 삼국지3처럼 "주(州)마다 다른 색 영역 + 해안선 + 강 + 산".
// 각 영역은 가장 가까운 성의 주 색으로 칠한다(보로노이). 좌표(0~1)는 castles.json 의 x,y 와 같고,
// 유니티의 StrategyUI 가 이 그림을 지도 영역에 꽉 채워 늘려 쓰므로 성 마커가 그대로 맞는다.
// 주 색은 Assets/Scripts/Strategy/StrategyUI.cs 의 RegionColor 와 같은 계열로 유지한다.
public static class MapArt
{
    const int MW = 2560, MH = 1440;
    const float K = MW / 1600f;   // 처음 1600x900 기준으로 잡은 픽셀 값(선 굵기, 글자 크기 등)을 해상도에 맞춰 늘리는 배율
    static readonly Color Ink = Color.FromArgb(255, 58, 40, 30);

    static Color Rgb(int r, int g, int b) { return Color.FromArgb(255, r, g, b); }

    static Color RegionColor(string region)
    {
        switch (region)
        {
            case "Youzhou": return Rgb(176, 196, 140);
            case "Jizhou": return Rgb(214, 196, 120);
            case "Bingzhou": return Rgb(190, 170, 140);
            case "Qingzhou": return Rgb(150, 196, 160);
            case "Yanzhou": return Rgb(206, 170, 130);
            case "Yuzhou": return Rgb(200, 150, 150);
            case "Xuzhou": return Rgb(160, 180, 200);
            case "Sili": return Rgb(222, 184, 96);
            case "Liangzhou": return Rgb(176, 150, 120);
            case "Yizhou": return Rgb(120, 176, 130);
            case "Jingzhou": return Rgb(150, 170, 210);
            case "Yangzhou": return Rgb(150, 200, 200);
            case "Jiaozhou": return Rgb(190, 150, 190);
            default: return Rgb(180, 180, 180);
        }
    }

    static string RegionHanja(string region)
    {
        switch (region)
        {
            case "Youzhou": return "幽州";
            case "Jizhou": return "冀州";
            case "Bingzhou": return "幷州";
            case "Qingzhou": return "靑州";
            case "Yanzhou": return "兗州";
            case "Yuzhou": return "豫州";
            case "Xuzhou": return "徐州";
            case "Sili": return "司隷";
            case "Liangzhou": return "涼州";
            case "Yizhou": return "益州";
            case "Jingzhou": return "荊州";
            case "Yangzhou": return "揚州";
            case "Jiaozhou": return "交州";
            default: return "";
        }
    }

    // 이웃한 주끼리 이름이 겹치거나 산/해안에 가려지는 곳만 직접 정한 글자 위치(지도 좌표)
    static readonly Dictionary<string, PointF> LabelSpots = new Dictionary<string, PointF>
    {
        { "Youzhou", new PointF(0.80f, 0.035f) }, { "Jizhou", new PointF(0.595f, 0.205f) }, { "Qingzhou", new PointF(0.725f, 0.26f) },
        { "Bingzhou", new PointF(0.40f, 0.09f) }, { "Sili", new PointF(0.375f, 0.29f) }, { "Yanzhou", new PointF(0.655f, 0.31f) },
        { "Jiaozhou", new PointF(0.30f, 0.92f) },
    };

    static PointF P(float x, float y) { return new PointF(x * MW, y * MH); }

    static PointF[] Pts(params float[] xy)
    {
        var r = new PointF[xy.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = P(xy[2 * i], xy[2 * i + 1]);
        return r;
    }

    // 해안선: 화면 밖으로 넘어가는 서쪽/북쪽은 땅, 동쪽/남쪽 바다만 윤곽을 둔다
    static GraphicsPath Land()
    {
        var p = new GraphicsPath();
        p.AddClosedCurve(Pts(
            -0.05f, -0.05f, 1.05f, -0.05f, 1.05f, 0.07f, 0.97f, 0.11f, 0.92f, 0.14f, 0.85f, 0.12f, 0.78f, 0.15f,
            0.73f, 0.17f, 0.76f, 0.20f, 0.83f, 0.21f, 0.91f, 0.23f, 0.93f, 0.26f, 0.87f, 0.30f, 0.83f, 0.34f,
            0.86f, 0.38f, 0.91f, 0.43f, 0.97f, 0.49f, 0.96f, 0.55f, 0.92f, 0.61f, 0.86f, 0.68f, 0.80f, 0.74f,
            0.72f, 0.79f, 0.66f, 0.84f, 0.60f, 0.89f, 0.52f, 0.91f, 0.44f, 0.93f, 0.38f, 0.96f, 0.32f, 0.99f,
            0.26f, 1.02f, -0.05f, 1.05f), 0.25f);
        return p;
    }

    static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xffff) / 65535f;
    }

    // 주 색 영역 (절반 해상도로 계산해 부드럽게 늘린다)
    static Bitmap Regions(string[] regions, float[] xs, float[] ys)
    {
        int w = MW / 2, h = MH / 2;
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[data.Stride * h];
        int n = regions.Length;
        var colors = new Color[n];
        for (int i = 0; i < n; i++) colors[i] = RegionColor(regions[i]);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x / (float)w, py = y / (float)h;
                float d1 = float.MaxValue; int best = 0;
                for (int i = 0; i < n; i++)
                {
                    float dx = (px - xs[i]) * w, dy = (py - ys[i]) * h;
                    float d = dx * dx + dy * dy;
                    if (d < d1) { d1 = d; best = i; }
                }
                float d2 = float.MaxValue;   // 다른 주에 속한 가장 가까운 성까지의 거리
                for (int i = 0; i < n; i++)
                {
                    if (regions[i] == regions[best]) continue;
                    float dx = (px - xs[i]) * w, dy = (py - ys[i]) * h;
                    float d = dx * dx + dy * dy;
                    if (d < d2) d2 = d;
                }
                float gap = (float)(Math.Sqrt(d2) - Math.Sqrt(d1));   // 경계에서 0
                float f = 0.86f + 0.14f * Math.Min(1f, gap / (14f * K));    // 경계 쪽을 살짝 어둡게
                float noise = 0.96f + 0.08f * Hash(x / 2, y / 2);
                Color c = colors[best];
                float k = f * noise;
                if (gap < 1.4f * K) k *= 0.55f;                           // 주 경계선

                int o = y * data.Stride + x * 4;
                buf[o] = (byte)Math.Min(255, c.B * k);
                buf[o + 1] = (byte)Math.Min(255, c.G * k);
                buf[o + 2] = (byte)Math.Min(255, c.R * k);
                buf[o + 3] = 255;
            }
        System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    static void River(Graphics g, Color col, float w, params float[] xy)
    {
        var pts = Pts(xy);
        using (var pen = new Pen(Ink, (w + 3) * K)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; g.DrawCurve(pen, pts, 0.5f); }
        using (var pen = new Pen(col, w * K)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; g.DrawCurve(pen, pts, 0.5f); }
    }

    // 산줄기: 선 위에 작은 산 모양을 줄지어 세운다
    static void Mountains(Graphics g, Random rng, float scale, params float[] xy)
    {
        scale *= K;
        var pts = Pts(xy);
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            float len = (float)Math.Sqrt(Math.Pow(pts[i + 1].X - pts[i].X, 2) + Math.Pow(pts[i + 1].Y - pts[i].Y, 2));
            int count = (int)(len / (26f * scale));
            for (int k = 0; k <= count; k++)
            {
                float t = count == 0 ? 0 : k / (float)count;
                float x = pts[i].X + (pts[i + 1].X - pts[i].X) * t + (float)(rng.NextDouble() - 0.5) * 18 * scale;
                float y = pts[i].Y + (pts[i + 1].Y - pts[i].Y) * t + (float)(rng.NextDouble() - 0.5) * 18 * scale;
                float s = (0.8f + (float)rng.NextDouble() * 0.6f) * scale;
                var tri = new[] { new PointF(x - 16 * s, y + 10 * s), new PointF(x, y - 18 * s), new PointF(x + 16 * s, y + 10 * s) };
                using (var b = new SolidBrush(Color.FromArgb(235, 138, 112, 84))) g.FillPolygon(b, tri);
                var shade = new[] { new PointF(x, y - 18 * s), new PointF(x + 16 * s, y + 10 * s), new PointF(x + 2 * s, y + 10 * s) };
                using (var b = new SolidBrush(Color.FromArgb(120, 70, 50, 36))) g.FillPolygon(b, shade);
                using (var pen = new Pen(Color.FromArgb(220, Ink), 2f * K)) { pen.LineJoin = LineJoin.Round; g.DrawPolygon(pen, tri); }
            }
        }
    }

    static string PickFont(params string[] names)
    {
        var have = new HashSet<string>();
        foreach (var f in FontFamily.Families) have.Add(f.Name);
        foreach (var n in names) if (have.Contains(n)) return n;
        return "Arial";
    }

    public static void Draw(string outPath, string[] regions, float[] xs, float[] ys)
    {
        var rng = new Random(20240601);
        using (var bmp = new Bitmap(MW, MH, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 바다
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, MW, MH), Rgb(70, 124, 168), Rgb(44, 92, 142), LinearGradientMode.ForwardDiagonal))
                g.FillRectangle(b, 0, 0, MW, MH);
            for (int i = 0; i < 160; i++)   // 잔물결
            {
                float x = (float)rng.NextDouble() * MW, y = (float)rng.NextDouble() * MH, l = (14 + (float)rng.NextDouble() * 30) * K;
                using (var pen = new Pen(Color.FromArgb(46, 255, 255, 255), 2f * K)) g.DrawLine(pen, x, y, x + l, y);
            }

            using (var land = Land())
            {
                // 해안의 얕은 바다 띠
                using (var pen = new Pen(Color.FromArgb(70, 170, 214, 232), 34f * K)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, land); }
                using (var pen = new Pen(Color.FromArgb(70, 190, 226, 240), 18f * K)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, land); }

                // 땅: 주 색 영역을 해안선 안쪽만 보이게
                using (var regionsBmp = Regions(regions, xs, ys))
                {
                    var state = g.Save();
                    g.SetClip(land);
                    g.DrawImage(regionsBmp, new Rectangle(0, 0, MW, MH));

                    // 강: 황하(누런색), 장강/한수/회수(푸른색)
                    River(g, Rgb(226, 190, 96), 6f, 0.10f, 0.30f, 0.16f, 0.26f, 0.24f, 0.18f, 0.34f, 0.12f, 0.44f, 0.14f,
                        0.44f, 0.25f, 0.46f, 0.31f, 0.52f, 0.32f, 0.60f, 0.29f, 0.68f, 0.25f, 0.76f, 0.19f);
                    River(g, Rgb(96, 154, 206), 7f, 0.06f, 0.63f, 0.18f, 0.60f, 0.26f, 0.57f, 0.38f, 0.53f, 0.46f, 0.54f,
                        0.57f, 0.53f, 0.64f, 0.57f, 0.70f, 0.52f, 0.75f, 0.47f, 0.82f, 0.47f, 0.95f, 0.47f);
                    River(g, Rgb(96, 154, 206), 4f, 0.28f, 0.42f, 0.40f, 0.45f, 0.50f, 0.50f, 0.57f, 0.53f);
                    River(g, Rgb(96, 154, 206), 4f, 0.50f, 0.42f, 0.62f, 0.40f, 0.77f, 0.38f);

                    // 산맥: 태행산, 진령, 촉 지방 산, 남령
                    Mountains(g, rng, 1.0f, 0.48f, 0.13f, 0.50f, 0.20f, 0.52f, 0.30f);
                    Mountains(g, rng, 1.0f, 0.30f, 0.395f, 0.38f, 0.40f, 0.47f, 0.395f);
                    Mountains(g, rng, 1.1f, 0.09f, 0.45f, 0.17f, 0.43f, 0.27f, 0.44f);
                    Mountains(g, rng, 0.9f, 0.40f, 0.70f, 0.52f, 0.69f, 0.64f, 0.70f);
                    Mountains(g, rng, 0.9f, 0.04f, 0.70f, 0.12f, 0.84f);
                    Mountains(g, rng, 0.9f, 0.70f, 0.64f, 0.78f, 0.64f);

                    g.Restore(state);
                }

                // 주 이름(한자)을 크고 흐리게
                var centers = new Dictionary<string, PointF>();
                var counts = new Dictionary<string, int>();
                for (int i = 0; i < regions.Length; i++)
                {
                    PointF c;
                    if (!centers.TryGetValue(regions[i], out c)) { c = new PointF(0, 0); counts[regions[i]] = 0; }
                    centers[regions[i]] = new PointF(c.X + xs[i], c.Y + ys[i]);
                    counts[regions[i]]++;
                }
                string fontName = PickFont("KaiTi", "SimSun", "Microsoft JhengHei", "Microsoft YaHei", "Malgun Gothic");
                using (var font = new Font(fontName, 54f * K, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(Color.FromArgb(78, 60, 36, 24)))
                {
                    var fmt = new StringFormat(); fmt.Alignment = StringAlignment.Center; fmt.LineAlignment = StringAlignment.Center;
                    foreach (var kv in centers)
                    {
                        // 성 마커와 겹치지 않도록 평균 위치에서 살짝 아래로 내린다. 겹치는 주는 직접 정한 자리를 쓴다
                        float cx = kv.Value.X / counts[kv.Key] * MW, cy = kv.Value.Y / counts[kv.Key] * MH + 52 * K;
                        PointF fixedPos;
                        if (LabelSpots.TryGetValue(kv.Key, out fixedPos)) { cx = fixedPos.X * MW; cy = fixedPos.Y * MH; }
                        g.DrawString(RegionHanja(kv.Key), font, brush, new RectangleF(cx - 100 * K, cy - 40 * K, 200 * K, 80 * K), fmt);
                    }
                }

                // 해안선
                using (var pen = new Pen(Ink, 4f * K)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, land); }
            }

            // 낡은 종이 느낌: 가장자리를 어둡게
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new RectangleF(0, 0, MW, MH));
                using (var pb = new PathGradientBrush(path))
                {
                    pb.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pb.SurroundColors = new[] { Color.FromArgb(120, 20, 10, 0) };
                    pb.FocusScales = new PointF(0.8f, 0.8f);
                    g.FillRectangle(pb, 0, 0, MW, MH);
                }
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
    }
}
