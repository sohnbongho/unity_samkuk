using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 야전(Step 15-2) 임시 그림을 GDI+ 로 그린다. 규격은 Assets/Scripts/Field/FieldArtSpec.cs 와 docs/FIELD_BATTLE.md "그림 규격".
//  - 바닥 타일 Tile_<지형>.png: 48x24 의 고전 2:1 마름모를 픽셀 단위로 직접 찍는다(이어 붙여도 틈이 없게). 안쪽은 지형 색 + 알갱이, 가장자리 2픽셀은 어둡게.
//  - 부대 시트 Unit_*.png 는 15-8 부터 장수/적 걷기 시트와 같은 규격(4x4, 192x192)이라 tools/hero_art/FieldSprites.cs(HeroArt 의 partial)가 그린다. 여기는 바닥 타일과 모아보기만.
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

    // ───────────────────────── 모아보기 ─────────────────────────

    public static void Preview(string outDir, string[] tileFiles, string[] unitFiles, string previewPath)
    {
        int zoom = 3;
        int sheet = Cell * 4;   // 부대 시트 한 변 (4x4 칸)
        int w = Math.Max(tileFiles.Length * (TileW + 4) * zoom, (sheet + 8) * zoom) + 20;
        int h = 20 + (TileH + 8) * zoom + unitFiles.Length * (sheet + 4) * zoom;
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
                y += (sheet + 4) * zoom;
            }
        }
        prev.Save(previewPath, ImageFormat.Png);
        prev.Dispose();
    }
}
