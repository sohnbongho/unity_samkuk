using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// 코드로 그린 벡터 그림을 도트(픽셀 아트) 규격으로 바꾸는 공통 도구 (HD-2D, Step 14-3).
// 그리는 코드는 그대로 두고 마지막에 ① 축소 ② 알파 자르기(반투명 없음) ③ 색 단계 줄이기(포스터라이즈) ④ 1픽셀 잉크 외곽선을 입힌다.
// 장수/적 시트(tools/hero_art)와 지형(tools/terrain_art) 생성기가 같이 쓴다. Windows PowerShell 5.1 = C# 5 문법만 사용.
public static class PixelTools
{
    /// <summary>src 의 srcRect 영역을 dst 사각형(소수 좌표 가능)에 부드럽게 축소해 그린다.</summary>
    public static void DrawScaled(Graphics g, Bitmap src, Rectangle srcRect, RectangleF dst)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.DrawImage(src, dst, srcRect, GraphicsUnit.Pixel);
    }

    /// <summary>그림 전체를 w x h 로 축소한 새 비트맵.</summary>
    public static Bitmap Downscale(Bitmap src, int w, int h)
    {
        var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.Clear(Color.Transparent);
            DrawScaled(g, src, new Rectangle(0, 0, src.Width, src.Height), new RectangleF(0, 0, w, h));
        }
        return dst;
    }

    /// <summary>
    /// 도트 규격으로 다듬는다. levels = 채널당 색 단계 수(작을수록 색이 적음). hardAlpha = 알파를 0/255 로 자른다(반투명 없음),
    /// false 면 알파를 alphaLevels 단계로만 줄인다(강물처럼 가장자리가 흐려야 하는 것). ink 가 비어 있지 않으면(알파 > 0)
    /// 불투명 픽셀에 맞닿은 투명 픽셀을 잉크색으로 칠해 1픽셀 외곽선을 만든다(hardAlpha 일 때만 의미 있음).
    /// </summary>
    public static void Pixelize(Bitmap b, int levels, bool hardAlpha, int alphaLevels, Color ink)
    {
        int w = b.Width, h = b.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = b.GetPixel(x, y);
                int a = hardAlpha ? (c.A >= 128 ? 255 : 0) : Quantize(c.A, alphaLevels);
                if (a == 0) { b.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0)); continue; }
                b.SetPixel(x, y, Color.FromArgb(a, Quantize(c.R, levels), Quantize(c.G, levels), Quantize(c.B, levels)));
            }

        if (hardAlpha && ink.A > 0) Outline(b, ink);
    }

    /// <summary>0~255 값을 levels 단계 중 가장 가까운 값으로.</summary>
    public static int Quantize(int v, int levels)
    {
        if (levels <= 1) return v;
        float step = 255f / (levels - 1);
        return (int)Math.Round(Math.Round(v / step) * step);
    }

    /// <summary>불투명 픽셀과 상하좌우로 맞닿은 투명 픽셀을 잉크색으로 (그림이 사방 1픽셀씩 자란다).</summary>
    public static void Outline(Bitmap b, Color ink)
    {
        int w = b.Width, h = b.Height;
        var opaque = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                opaque[x, y] = b.GetPixel(x, y).A > 0;
        Color inkC = Color.FromArgb(255, ink.R, ink.G, ink.B);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (opaque[x, y]) continue;
                bool edge = (x > 0 && opaque[x - 1, y]) || (x < w - 1 && opaque[x + 1, y]) || (y > 0 && opaque[x, y - 1]) || (y < h - 1 && opaque[x, y + 1]);
                if (edge) b.SetPixel(x, y, inkC);
            }
    }

    /// <summary>미리보기용: 도트 그림을 정수 배로 또렷하게 확대해 그린다.</summary>
    public static void DrawCrisp(Graphics g, Bitmap src, Rectangle srcRect, Rectangle dst)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(src, dst, srcRect, GraphicsUnit.Pixel);
    }
}
