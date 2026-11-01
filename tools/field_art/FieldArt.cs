using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 야전(Step 15-2) 임시 그림을 GDI+ 로 그린다. 규격은 Assets/Scripts/Field/FieldArtSpec.cs 와 docs/FIELD_BATTLE.md "그림 규격".
//  - 바닥 타일 Tile_<지형>.png: 48x24 의 고전 2:1 마름모를 픽셀 단위로 직접 찍는다(이어 붙여도 틈이 없게). 안쪽은 지형 색 + 알갱이, 가장자리 2픽셀은 어둡게.
//  - 부대 시트 Unit_<병과|General>_<Player|Enemy>.png: 48x48 칸 4개 [앞모습 0, 앞모습 1, 뒷모습 0, 뒷모습 1].
//    한 칸에 병사 3명(보병 창·방패, 궁병 활), 기병은 말 2마리, 장수 부대는 큰 말 1마리 + 깃발. 칸 아래에서 12픽셀 위가 마름모 가운데(발 자리).
//    4배로 크게 그린 뒤 축소·색 단계·1픽셀 외곽선(PixelTools)을 입힌다. 앞모습은 오른쪽 아래로 오는 모습(얼굴 보임), 뒷모습은 오른쪽 위로 가는 모습.
// Windows PowerShell 5.1 = C# 5 문법만 사용.
public static class FieldArt
{
    const int SS = 4;                 // 부대 그림 확대 배율
    const int Cell = 48;              // 칸 (최종 픽셀)
    const int Foot = 12;              // 칸 아래에서 마름모 가운데까지
    const int TileW = 48, TileH = 24;
    const int UnitLevels = 7;
    const int TileLevels = 12;
    static readonly Color Ink = Color.FromArgb(255, 34, 26, 22);

    static readonly Color Skin = Color.FromArgb(255, 232, 195, 158);
    static readonly Color Steel = Color.FromArgb(255, 150, 158, 172);
    static readonly Color SteelDark = Color.FromArgb(255, 92, 100, 116);
    static readonly Color Wood = Color.FromArgb(255, 110, 76, 42);
    static readonly Color Gold = Color.FromArgb(255, 228, 184, 70);
    static readonly Color Pants = Color.FromArgb(255, 70, 58, 50);
    static readonly Color HorseBrown = Color.FromArgb(255, 138, 90, 44);
    static readonly Color HorseDark = Color.FromArgb(255, 74, 56, 46);
    static readonly Color HorseWhite = Color.FromArgb(255, 226, 220, 210);
    static readonly Color PlayerBlue = Color.FromArgb(255, 58, 98, 200);
    static readonly Color EnemyRed = Color.FromArgb(255, 194, 58, 58);

    static Color Hex(string h) { return ColorTranslator.FromHtml(h); }
    static Color Shade(Color c, float f)
    {
        return Color.FromArgb(c.A, (int)Math.Min(255, c.R * f), (int)Math.Min(255, c.G * f), (int)Math.Min(255, c.B * f));
    }
    static int Seed(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return (int)(h & 0x7fffffff);
    }

    // ───────────────────────── 바닥 타일 ─────────────────────────

    /// <summary>48x24 마름모 타일. baseHex 바탕에 accentHex 알갱이, 가장자리는 어둡게. 알파는 0/255 만.</summary>
    public static void DrawTile(string path, string baseHex, string[] accentHex)
    {
        Color baseC = Hex(baseHex);
        var accents = new Color[accentHex.Length];
        for (int i = 0; i < accentHex.Length; i++) accents[i] = Hex(accentHex[i]);
        Color edge = Shade(baseC, 0.72f);
        var rnd = new Random(Seed(Path.GetFileName(path)));

        var b = new Bitmap(TileW, TileH, PixelFormat.Format32bppArgb);
        for (int y = 0; y < TileH; y++)
        {
            int dy = y < TileH / 2 ? y : TileH - 1 - y;      // 0..11
            int half = 2 * (dy + 1);                           // 그 줄의 반폭 (2:1 마름모)
            int x0 = TileW / 2 - half, x1 = TileW / 2 + half; // [x0, x1)
            for (int x = x0; x < x1; x++)
            {
                bool edgePx = x < x0 + 2 || x >= x1 - 2 || dy == 0;
                Color c = baseC;
                if (!edgePx)
                {
                    double r = rnd.NextDouble();
                    if (r < 0.22) c = accents[rnd.Next(accents.Length)];
                }
                else c = edge;
                b.SetPixel(x, y, c);
            }
        }
        PixelTools.Pixelize(b, TileLevels, true, 1, Color.FromArgb(0, 0, 0, 0));
        b.Save(path, ImageFormat.Png);
        b.Dispose();
    }

    // ───────────────────────── 부대 시트 ─────────────────────────

    /// <summary>cls: Infantry/Archer/Cavalry/General, side: Player/Enemy. 192x48 시트를 저장한다.</summary>
    public static void DrawUnitSheet(string path, string cls, string side)
    {
        Color armor = side == "Player" ? PlayerBlue : EnemyRed;
        var sheet = new Bitmap(Cell * 4, Cell, PixelFormat.Format32bppArgb);
        using (var sg = Graphics.FromImage(sheet))
        {
            sg.Clear(Color.Transparent);
            for (int i = 0; i < 4; i++)
            {
                bool back = i >= 2;
                int frame = i % 2;
                using (var big = new Bitmap(Cell * SS, Cell * SS, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(big))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        DrawUnit(g, cls, side, armor, back, frame);
                    }
                    using (var small = PixelTools.Downscale(big, Cell, Cell))
                    {
                        PixelTools.Pixelize(small, UnitLevels, true, 1, Ink);
                        sg.DrawImage(small, new Rectangle(i * Cell, 0, Cell, Cell), new Rectangle(0, 0, Cell, Cell), GraphicsUnit.Pixel);
                    }
                }
            }
        }
        sheet.Save(path, ImageFormat.Png);
        sheet.Dispose();
    }

    // 마름모 가운데(발 자리)의 큰 그림 좌표. GDI 는 y 가 아래로 늘므로 바닥은 Cell - Foot.
    static float CX { get { return Cell * SS * 0.5f; } }
    static float CY { get { return (Cell - Foot) * SS; } }

    static void DrawUnit(Graphics g, string cls, string side, Color armor, bool back, int frame)
    {
        // 마름모 위에 흩어 선 자리 (최종 픽셀 단위 오프셋). 뒤(위)에 선 것부터 그려 겹침이 자연스럽다.
        switch (cls)
        {
            case "Infantry":
                Soldier(g, CX + 0 * SS, CY - 4 * SS, armor, back, frame, "spear");
                Soldier(g, CX - 11 * SS, CY + 0 * SS, armor, back, frame + 1, "spear");
                Soldier(g, CX + 10 * SS, CY + 1 * SS, armor, back, frame, "spear");
                break;
            case "Archer":
                Soldier(g, CX + 0 * SS, CY - 4 * SS, armor, back, frame, "bow");
                Soldier(g, CX - 11 * SS, CY + 0 * SS, armor, back, frame + 1, "bow");
                Soldier(g, CX + 10 * SS, CY + 1 * SS, armor, back, frame, "bow");
                break;
            case "Cavalry":
                Horse(g, CX - 8 * SS, CY - 4 * SS, armor, side == "Player" ? HorseBrown : HorseDark, back, frame, 1f, false);
                Horse(g, CX + 7 * SS, CY + 2 * SS, armor, side == "Player" ? HorseBrown : HorseDark, back, frame + 1, 1f, false);
                break;
            default: // General
                Horse(g, CX + 0 * SS, CY + 0 * SS, armor, HorseWhite, back, frame, 1.3f, true);
                break;
        }
    }

    // 병사 한 명: 발 가운데 (fx, fy). 키 약 15픽셀. kind = spear | bow.
    static void Soldier(Graphics g, float fx, float fy, Color armor, bool back, int frame, string kind)
    {
        float s = SS;
        int step = frame % 2;                 // 걷기: 다리 벌림과 몸 흔들림
        float bob = step == 1 ? -0.5f * s : 0f;

        // 다리
        Fill(g, Pants, fx - 2.2f * s, fy - 4 * s + bob, 1.8f * s, 4 * s - bob + (step == 1 ? 0 : 0));
        Fill(g, Pants, fx + 0.4f * s, fy - 4 * s + bob + (step == 1 ? 0.8f * s : 0), 1.8f * s, 4 * s - bob - (step == 1 ? 0.8f * s : 0));
        // 몸(갑옷)
        Fill(g, armor, fx - 3 * s, fy - 10 * s + bob, 6 * s, 6.5f * s);
        Fill(g, Shade(armor, 0.75f), fx - 3 * s, fy - 5.5f * s + bob, 6 * s, 1.2f * s); // 허리띠
        // 머리: 앞모습이면 얼굴, 뒷모습이면 투구 뒤통수
        float hy = fy - 13 * s + bob;
        if (back)
        {
            Ell(g, SteelDark, fx, hy, 2.8f * s, 2.8f * s);
        }
        else
        {
            Ell(g, Skin, fx, hy, 2.6f * s, 2.6f * s);
            // 투구 (위 절반)
            Fill(g, SteelDark, fx - 2.9f * s, hy - 2.9f * s, 5.8f * s, 2.4f * s);
            // 눈
            Fill(g, Ink, fx + 0.6f * s, hy + 0.2f * s, 0.8f * s, 0.8f * s);
        }
        // 투구 꼭지(편 색)
        Fill(g, armor, fx - 0.6f * s, hy - 4.2f * s, 1.2f * s, 1.6f * s);

        if (kind == "spear")
        {
            // 창: 오른손 쪽에 수직, 끝은 강철
            float sx = fx + 4.2f * s;
            Fill(g, Wood, sx - 0.5f * s, fy - 19 * s + bob, 1f * s, 19 * s - bob);
            Tri(g, Steel, sx, fy - 22 * s + bob, 1.4f * s, 3.5f * s);
            // 방패: 앞모습이면 몸 앞(왼쪽), 뒷모습이면 등 뒤에 살짝 보임
            if (back) Ell(g, Shade(armor, 0.85f), fx - 1.5f * s, fy - 8 * s + bob, 3.4f * s, 3.8f * s);
            else Ell(g, Shade(armor, 0.9f), fx - 3.8f * s, fy - 7.5f * s + bob, 3.0f * s, 3.4f * s);
            if (!back) Ell(g, Gold, fx - 3.8f * s, fy - 7.5f * s + bob, 1.0f * s, 1.0f * s);
        }
        else
        {
            // 활: 오른손 쪽에 세운 호. 뒷모습이면 화살통이 등에 보임
            float bx = fx + (back ? -4.0f : 4.4f) * s;
            using (var p = new Pen(Wood, 1.2f * s))
                g.DrawArc(p, bx - 3.5f * s, fy - 16 * s + bob, 7 * s, 10 * s, back ? 90 : -90, 180);
            using (var p = new Pen(Shade(Skin, 0.8f), 0.5f * s))
                g.DrawLine(p, bx, fy - 16 * s + bob, bx, fy - 6 * s + bob);
            if (back) Fill(g, Wood, fx + 1.5f * s, fy - 12 * s + bob, 1.8f * s, 6 * s);
        }
    }

    // 말 + 기수: 발(앞발) 가운데 (fx, fy). scale 로 장수 말을 키운다. banner = 깃발.
    static void Horse(Graphics g, float fx, float fy, Color armor, Color coat, bool back, int frame, float scale, bool banner)
    {
        float s = SS * scale;
        int step = frame % 2;
        float bob = step == 1 ? -0.5f * s : 0f;
        // 다리 4개 (걷기: 앞뒤 번갈아)
        for (int i = 0; i < 4; i++)
        {
            float lx = fx + (-6.5f + i * 4.2f) * s;
            float lift = ((i + step) % 2 == 0) ? 0f : -1.2f * s;
            Fill(g, Shade(coat, 0.8f), lx, fy - 5 * s + lift, 1.6f * s, 5 * s - lift);
        }
        // 몸통
        Ell(g, coat, fx, fy - 8.5f * s + bob, 8.5f * s, 3.8f * s);
        // 목·머리 (오른쪽을 본다)
        Fill(g, coat, fx + 6.5f * s, fy - 15 * s + bob, 3.2f * s, 7 * s);
        Fill(g, coat, fx + 7.0f * s, fy - 16 * s + bob, 5.5f * s, 3.0f * s);
        Fill(g, Shade(coat, 0.6f), fx + 7.2f * s, fy - 16.5f * s + bob, 1.2f * s, 1.6f * s); // 귀
        if (!back) Fill(g, Ink, fx + 11.2f * s, fy - 15.2f * s + bob, 0.8f * s, 0.8f * s);     // 눈
        // 꼬리
        Fill(g, Shade(coat, 0.6f), fx - 9.5f * s, fy - 10 * s + bob, 1.4f * s, 5 * s);
        // 안장깔개(편 색)
        Fill(g, armor, fx - 3.5f * s, fy - 11 * s + bob, 7 * s, 2.5f * s);
        // 기수
        Fill(g, Pants, fx - 1.5f * s, fy - 11.5f * s + bob, 3 * s, 3 * s);
        Fill(g, armor, fx - 2.5f * s, fy - 17 * s + bob, 5 * s, 6 * s);
        float hy = fy - 19.5f * s + bob;
        if (back) Ell(g, SteelDark, fx, hy, 2.4f * s, 2.4f * s);
        else
        {
            Ell(g, Skin, fx, hy, 2.3f * s, 2.3f * s);
            Fill(g, SteelDark, fx - 2.6f * s, hy - 2.6f * s, 5.2f * s, 2.1f * s);
            Fill(g, Ink, fx + 0.6f * s, hy + 0.1f * s, 0.7f * s, 0.7f * s);
        }
        Fill(g, armor, fx - 0.5f * s, hy - 3.8f * s, 1.0f * s, 1.4f * s);
        // 창
        float px = fx + 3.8f * s;
        Fill(g, Wood, px - 0.45f * s, fy - 26 * s + bob, 0.9f * s, 16 * s);
        Tri(g, Steel, px, fy - 28.5f * s + bob, 1.3f * s, 3 * s);
        if (banner)
        {
            // 깃대 + 깃발 (편 색, 금색 테)
            float bx = fx - 4.5f * s;
            Fill(g, Wood, bx - 0.45f * s, fy - 34 * s + bob, 0.9f * s, 24 * s);
            Fill(g, Gold, bx - 0.9f * s, fy - 35.5f * s + bob, 1.8f * s, 1.8f * s);
            var pts = new PointF[] { new PointF(bx, fy - 34 * s + bob), new PointF(bx + 9 * s, fy - 32.5f * s + bob), new PointF(bx + 7.5f * s, fy - 29 * s + bob), new PointF(bx + 9 * s, fy - 25.5f * s + bob), new PointF(bx, fy - 24 * s + bob) };
            using (var br = new SolidBrush(armor)) g.FillPolygon(br, pts);
            using (var p = new Pen(Gold, 0.8f * s)) g.DrawPolygon(p, pts);
        }
    }

    // ───────────────────────── 도형 ─────────────────────────

    static void Fill(Graphics g, Color c, float x, float y, float w, float h)
    {
        using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
    }
    static void Ell(Graphics g, Color c, float cx, float cy, float rx, float ry)
    {
        using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }
    // 위를 향한 삼각형: 밑변 가운데 (cx, cy), 반폭 hw, 높이 h
    static void Tri(Graphics g, Color c, float cx, float cy, float hw, float h)
    {
        using (var b = new SolidBrush(c)) g.FillPolygon(b, new PointF[] { new PointF(cx - hw, cy + h), new PointF(cx + hw, cy + h), new PointF(cx, cy) });
    }

    // ───────────────────────── 모아보기 ─────────────────────────

    public static void Preview(string outDir, string[] tileFiles, string[] unitFiles, string previewPath)
    {
        int zoom = 3;
        int w = Math.Max(tileFiles.Length * (TileW + 4) * zoom, (Cell * 4 + 8) * zoom) + 20;
        int h = 20 + (TileH + 8) * zoom + unitFiles.Length * (Cell + 4) * zoom;
        var prev = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(prev))
        {
            g.Clear(Color.FromArgb(255, 60, 70, 80));
            int x = 10, y = 10;
            foreach (var f in tileFiles)
            {
                string p = Path.Combine(outDir, f);
                if (!File.Exists(p)) continue;
                using (var b = new Bitmap(p)) PixelTools.DrawCrisp(g, b, new Rectangle(0, 0, b.Width, b.Height), new Rectangle(x, y, b.Width * zoom, b.Height * zoom));
                x += (TileW + 4) * zoom;
            }
            y += (TileH + 8) * zoom;
            foreach (var f in unitFiles)
            {
                string p = Path.Combine(outDir, f);
                if (!File.Exists(p)) continue;
                using (var b = new Bitmap(p)) PixelTools.DrawCrisp(g, b, new Rectangle(0, 0, b.Width, b.Height), new Rectangle(10, y, b.Width * zoom, b.Height * zoom));
                y += (Cell + 4) * zoom;
            }
        }
        prev.Save(previewPath, ImageFormat.Png);
        prev.Dispose();
    }
}
