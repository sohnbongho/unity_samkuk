using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 들고 휘두르는 무기 그림(Step 10-9, 베기 6종 + 창 3종 + 의천검 2종 + 극 2종). 손잡이가 아래, 날이 위(+y)를 향하며 피벗은 손잡이 끝(아래 가운데).
// 최종 픽셀 크기(w x h)의 좌표계에서 바로 그리되 WSS 배로 키워 그린 뒤 축소·색 단계·1픽셀 외곽선(PixelTools)을 입힌다.
// 크기 기준: PPU 32 → 검 26px ≈ 0.8유닛, 언월도 40px ≈ 1.25유닛. 결과: Assets/Sprites/Weapons/<무기 에셋 이름>_Held.png
public static partial class HeroArt
{
    const int WSS = 8;

    static readonly string[] HeldWeaponNames =
    {
        "Weapon_Sword", "Weapon_TwinSwords", "Weapon_GreenDragon",
        "Weapon_Evo_Zanmato", "Weapon_Evo_TwinDragons", "Weapon_Evo_MoonDragon",
        "Weapon_Thrust", "Weapon_SerpentSpear", "Weapon_Evo_DragonSpear",
        "Weapon_YitianSword", "Weapon_Evo_OverlordSword", "Weapon_SkyPiercer", "Weapon_Evo_PeerlessHalberd",
    };

    static readonly Color Wood = Color.FromArgb(255, 92, 60, 36);
    static readonly Color WoodDark = Color.FromArgb(255, 58, 36, 22);
    static readonly Color Leather = Color.FromArgb(255, 120, 44, 36);
    static readonly Color SteelLight = Color.FromArgb(255, 236, 240, 246);
    static readonly Color SteelDark = Color.FromArgb(255, 130, 142, 160);
    static readonly Color Jade = Color.FromArgb(255, 150, 220, 180);
    static readonly Color JadeDark = Color.FromArgb(255, 70, 150, 110);
    static readonly Color Cyan = Color.FromArgb(255, 150, 230, 240);
    static readonly Color CyanDark = Color.FromArgb(255, 50, 150, 175);
    static readonly Color Crimson = Color.FromArgb(255, 200, 60, 50);
    static readonly Color Azure = Color.FromArgb(255, 70, 110, 220);
    static readonly Color Violet = Color.FromArgb(255, 200, 170, 245);
    static readonly Color VioletDark = Color.FromArgb(255, 120, 80, 190);
    static readonly Color Navy = Color.FromArgb(255, 40, 50, 110);

    public static void GenerateHeldWeapons(string outDir, string previewPath)
    {
        Directory.CreateDirectory(outDir);
        int zoom = 4;
        int cell = 64 * zoom; // 가장 긴 그림(극 56px)이 들어가는 칸
        var prev = new Bitmap(cell * HeldWeaponNames.Length + 20, cell + 20, PixelFormat.Format32bppArgb);
        using (var pg = Graphics.FromImage(prev))
        {
            pg.Clear(Color.FromArgb(255, 70, 100, 70));
            for (int i = 0; i < HeldWeaponNames.Length; i++)
            {
                using (var b = RenderHeldWeapon(HeldWeaponNames[i]))
                {
                    b.Save(Path.Combine(outDir, HeldWeaponNames[i] + "_Held.png"), ImageFormat.Png);
                    // 미리보기: 칸 아래 가운데에 손잡이 끝을 맞춰 놓는다
                    int x = 10 + i * cell + (cell - b.Width * zoom) / 2;
                    int y = 10 + cell - b.Height * zoom;
                    PixelTools.DrawCrisp(pg, b, new Rectangle(0, 0, b.Width, b.Height), new Rectangle(x, y, b.Width * zoom, b.Height * zoom));
                }
            }
        }
        prev.Save(previewPath, ImageFormat.Png);
        prev.Dispose();
    }

    public static Bitmap RenderHeldWeapon(string name)
    {
        int w, h;
        Action<Graphics, int, int> draw;
        switch (name)
        {
            case "Weapon_TwinSwords": w = 12; h = 24; draw = DrawSword(Jade, JadeDark, Gold, Wood); break;
            case "Weapon_GreenDragon": w = 16; h = 40; draw = DrawGlaive(Jade, JadeDark, Gold, false); break;
            case "Weapon_Evo_Zanmato": w = 16; h = 44; draw = DrawBroadBlade(); break;
            case "Weapon_Evo_TwinDragons": w = 12; h = 26; draw = DrawTwoToneSword(); break;
            case "Weapon_Evo_MoonDragon": w = 18; h = 44; draw = DrawGlaive(Cyan, CyanDark, Gold, true); break;
            case "Weapon_Thrust": w = 10; h = 44; draw = DrawSpear(SteelLight, SteelDark, Steel, false, false); break;
            case "Weapon_SerpentSpear": w = 12; h = 48; draw = DrawSpear(SteelLight, SteelDark, Gold, true, false); break;
            case "Weapon_Evo_DragonSpear": w = 12; h = 48; draw = DrawSpear(Cyan, CyanDark, Gold, false, true); break;
            case "Weapon_YitianSword": w = 12; h = 30; draw = DrawOrnateSword(SteelLight, SteelDark, Azure, Navy); break;
            case "Weapon_Evo_OverlordSword": w = 14; h = 34; draw = DrawOrnateSword(Violet, VioletDark, Gold, Crimson); break;
            case "Weapon_SkyPiercer": w = 14; h = 52; draw = DrawHalberd(SteelLight, SteelDark, Gold, false); break;
            case "Weapon_Evo_PeerlessHalberd": w = 16; h = 56; draw = DrawHalberd(Gold, Shade(Gold, 0.7f), Crimson, true); break;
            default: w = 12; h = 26; draw = DrawSword(SteelLight, SteelDark, Gold, Wood); break; // Weapon_Sword
        }

        var big = new Bitmap(w * WSS, h * WSS, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.ScaleTransform(WSS, WSS);
            draw(g, w, h);
        }
        var small = PixelTools.Downscale(big, w, h);
        big.Dispose();
        PixelTools.Pixelize(small, PIXEL_LEVELS, true, 1, Ink);
        return small;
    }

    // ───────────────────────── 모양 (좌표: x 0..w, y 0..h, y 는 아래로) ─────────────────────────

    static void FillPoly(Graphics g, Color c, params float[] xy)
    {
        using (var br = new SolidBrush(c)) g.FillPolygon(br, Pts(xy));
    }

    static void FillRect(Graphics g, Color c, float x, float y, float w, float h)
    {
        using (var br = new SolidBrush(c)) g.FillRectangle(br, x, y, w, h);
    }

    /// <summary>곧은 검: 손잡이(아래) + 코등이 + 날. 날 왼쪽 절반은 밝게, 오른쪽은 어둡게 해 면을 나눈다.</summary>
    static Action<Graphics, int, int> DrawSword(Color blade, Color bladeDark, Color guard, Color grip)
    {
        return delegate(Graphics g, int w, int h)
        {
            float cx = w * 0.5f;
            float gripTop = h - 7f;
            FillRect(g, Shade(grip, 0.8f), cx - 1.2f, gripTop, 2.4f, 6f);            // 손잡이
            FillRect(g, guard, cx - 1.8f, h - 1.6f, 3.6f, 1.6f);                      // 칼자루 끝
            FillRect(g, guard, cx - 3.2f, gripTop - 1.6f, 6.4f, 1.6f);                // 코등이
            float bladeBottom = gripTop - 1.6f;
            float tip = 0.6f;
            FillPoly(g, blade, cx - 1.6f, bladeBottom, cx, bladeBottom, cx, tip + 2.2f, cx - 1.6f, tip + 3.2f);
            FillPoly(g, bladeDark, cx, bladeBottom, cx + 1.6f, bladeBottom, cx + 1.6f, tip + 3.2f, cx, tip + 2.2f);
            FillPoly(g, blade, cx - 1.6f, tip + 3.2f, cx + 1.6f, tip + 3.2f, cx, tip);       // 끝
        };
    }

    /// <summary>의천검/패왕의천검: 곧은 검에 가운데 홈 선과 칼자루 보석. line 은 홈 색, grip 은 손잡이 색.</summary>
    static Action<Graphics, int, int> DrawOrnateSword(Color blade, Color bladeDark, Color line, Color grip)
    {
        var baseSword = DrawSword(blade, bladeDark, Gold, grip);
        return delegate(Graphics g, int w, int h)
        {
            baseSword(g, w, h);
            float cx = w * 0.5f;
            float bladeBottom = h - 7f - 1.6f;
            FillRect(g, line, cx - 0.4f, 4.5f, 0.8f, bladeBottom - 6f);                 // 홈 선
            FillRect(g, line, cx - 0.9f, h - 1.4f, 1.8f, 1.2f);                           // 칼자루 보석
            FillRect(g, Shade(Gold, 1.15f), cx - 3.2f, h - 7f - 1.6f, 6.4f, 0.6f);        // 코등이 윗면 빛
        };
    }

    /// <summary>
    /// 극(방천화극/천하무쌍극): 긴 자루 + 위쪽 창 촉 + 좌우 양쪽의 초승달 날 + 붉은 술. ornate 면 날이 더 크고 자루에 금 테가 더 들어간다(진화형).
    /// </summary>
    static Action<Graphics, int, int> DrawHalberd(Color blade, Color bladeDark, Color trim, bool ornate)
    {
        return delegate(Graphics g, int w, int h)
        {
            float cx = w * 0.5f;
            float headH = h * 0.38f;                                                       // 촉과 초승달이 차지하는 높이
            FillRect(g, Wood, cx - 1.1f, headH - 0.5f, 2.2f, h - headH + 0.5f);          // 자루
            FillRect(g, WoodDark, cx - 1.1f, headH - 0.5f, 0.8f, h - headH + 0.5f);      // 자루 그늘
            FillRect(g, trim, cx - 1.5f, h - 1.6f, 3.0f, 1.6f);                            // 물미
            FillRect(g, trim, cx - 1.8f, headH - 1.0f, 3.6f, 1.6f);                        // 촉 받침
            FillRect(g, Crimson, cx - 1.7f, headH + 1.2f, 3.4f, 2.8f);                     // 붉은 술
            if (ornate)
            {
                FillRect(g, trim, cx - 1.3f, h * 0.62f, 2.6f, 1.2f);                       // 자루 금 테
                FillRect(g, trim, cx - 1.3f, h * 0.8f, 2.6f, 1.2f);
            }

            // 창 촉: 잎 모양, 자루 위로 곧게
            float tipBottom = headH * 0.72f, tipMid = headH * 0.32f;
            FillPoly(g, blade, cx - 0.6f, tipBottom, cx, tipBottom, cx, 0.5f, cx - 1.9f, tipMid);
            FillPoly(g, bladeDark, cx, tipBottom, cx + 0.6f, tipBottom, cx + 1.9f, tipMid, cx, 0.5f);

            // 초승달 날 둘: 자루 양옆에서 바깥으로 불룩하게. 바깥 타원에서 안쪽(자루 쪽) 타원을 뺀다
            float cy = headH * 0.55f;
            float ry = headH * 0.36f * (ornate ? 1.1f : 1f);
            float rx = (w * 0.5f - 1.2f) * 0.95f;
            FillCrescent(g, blade, cx - 1.6f, cy, rx, ry, -1);
            FillCrescent(g, bladeDark, cx + 1.6f, cy, rx, ry, +1);
            // 날과 자루를 잇는 가로 쇠
            FillRect(g, trim, cx - 2.6f, cy - 0.7f, 5.2f, 1.4f);
        };
    }

    /// <summary>초승달: (ex, cy) 를 가운데로 side 쪽으로 rx 만큼 뻗는 타원에서, 자루 쪽으로 치우친 같은 크기 타원을 뺀 모양.</summary>
    static void FillCrescent(Graphics g, Color c, float ex, float cy, float rx, float ry, int side)
    {
        float ox = ex + side * rx * 0.5f;                                      // 바깥 타원 중심
        float ix = ex - side * rx * 0.15f;                                     // 안쪽 타원 중심(자루 쪽으로)
        using (var outer = new GraphicsPath())
        using (var inner = new GraphicsPath())
        {
            outer.AddEllipse(ox - rx * 0.5f, cy - ry, rx, ry * 2f);
            inner.AddEllipse(ix - rx * 0.5f, cy - ry * 0.85f, rx, ry * 1.7f);
            using (var region = new Region(outer))
            using (var br = new SolidBrush(c))
            {
                region.Exclude(inner);
                g.FillRegion(br, region);
            }
        }
    }

    /// <summary>쌍룡자웅검: 곧은 검인데 날이 푸른 쪽/붉은 쪽으로 나뉜다.</summary>
    static Action<Graphics, int, int> DrawTwoToneSword()
    {
        var baseSword = DrawSword(SteelLight, SteelDark, Gold, Leather);
        return delegate(Graphics g, int w, int h)
        {
            baseSword(g, w, h);
            float cx = w * 0.5f;
            float bladeBottom = h - 7f - 1.6f;
            FillRect(g, Azure, cx - 1.0f, 4.5f, 0.9f, bladeBottom - 5.5f);
            FillRect(g, Crimson, cx + 0.1f, 4.5f, 0.9f, bladeBottom - 5.5f);
        };
    }

    /// <summary>언월도: 긴 자루 + 위쪽 오른편으로 휘어진 넓은 날. ornate 면 자루 장식과 날 끝 갈고리를 더한다(진화형).</summary>
    static Action<Graphics, int, int> DrawGlaive(Color blade, Color bladeDark, Color trim, bool ornate)
    {
        return delegate(Graphics g, int w, int h)
        {
            float cx = w * 0.42f;
            float bladeH = h * 0.46f;                     // 날이 차지하는 높이
            FillRect(g, Wood, cx - 1.1f, bladeH - 1f, 2.2f, h - bladeH + 1f);       // 자루
            FillRect(g, WoodDark, cx - 1.1f, bladeH - 1f, 0.8f, h - bladeH + 1f);  // 자루 그늘
            FillRect(g, trim, cx - 1.6f, h - 2f, 3.2f, 2f);                        // 물미
            FillRect(g, trim, cx - 1.8f, bladeH - 1.2f, 3.6f, 2.2f);               // 날 받침
            if (ornate) FillRect(g, Crimson, cx - 1.4f, bladeH + 3f, 2.8f, 2.4f);  // 붉은 술

            // 날: 자루 위에서 시작해 오른쪽으로 불룩하게 휘고 위쪽 끝이 뾰족
            float r = w - 1f;
            FillPoly(g, blade,
                cx - 0.6f, bladeH, cx + 1.0f, bladeH, cx + 2.6f, bladeH * 0.78f, r, bladeH * 0.5f, r - 1.2f, bladeH * 0.22f,
                cx + 2.4f, 1.2f, cx + 0.3f, 0.5f, cx - 0.6f, bladeH * 0.15f);
            // 날 안쪽(자루 쪽) 어두운 면
            FillPoly(g, bladeDark,
                cx - 0.6f, bladeH, cx + 1.0f, bladeH, cx + 1.8f, bladeH * 0.6f, cx + 1.6f, bladeH * 0.25f, cx + 0.3f, 0.5f, cx - 0.6f, bladeH * 0.15f);
            if (ornate) FillPoly(g, trim, cx - 0.6f, bladeH * 0.15f, cx - 2.4f, bladeH * 0.22f, cx - 0.6f, bladeH * 0.3f); // 뒤쪽 갈고리
        };
    }

    /// <summary>창: 긴 자루 + 위쪽 잎 모양 촉. serpent 면 장팔사모의 뱀처럼 구불거리는 촉, ornate 면 용담창의 장식(붉은 술, 금 테).</summary>
    static Action<Graphics, int, int> DrawSpear(Color blade, Color bladeDark, Color trim, bool serpent, bool ornate)
    {
        return delegate(Graphics g, int w, int h)
        {
            float cx = w * 0.5f;
            float headH = serpent ? h * 0.36f : h * 0.28f;           // 촉이 차지하는 높이
            FillRect(g, Wood, cx - 1.0f, headH - 0.5f, 2.0f, h - headH + 0.5f);         // 자루
            FillRect(g, WoodDark, cx - 1.0f, headH - 0.5f, 0.7f, h - headH + 0.5f);     // 자루 그늘
            FillRect(g, trim, cx - 1.4f, h - 1.6f, 2.8f, 1.6f);                           // 물미
            FillRect(g, trim, cx - 1.6f, headH - 0.8f, 3.2f, 1.6f);                       // 촉 받침
            if (ornate)
            {
                FillRect(g, Crimson, cx - 1.6f, headH + 1.5f, 3.2f, 2.6f);               // 붉은 술
                FillRect(g, trim, cx - 1.2f, h * 0.62f, 2.4f, 1.2f);                      // 자루 금 테
            }

            if (serpent)
            {
                // 뱀처럼 좌우로 구불거리는 촉: 가운데 선을 따라 폭이 변하는 다각형
                var pts = new List<float>();
                int steps = 7;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;                                           // 0 = 촉 끝, 1 = 촉 밑
                    float y = 0.5f + t * (headH - 1f);
                    float wave = (float)Math.Sin(t * Math.PI * 2.2) * 1.3f * (1f - t * 0.3f);
                    float half = 0.4f + 1.5f * (float)Math.Sin(t * Math.PI);            // 가운데가 넓고 양 끝이 좁다
                    pts.Add(cx + wave - half); pts.Add(y);
                }
                for (int i = steps; i >= 0; i--)
                {
                    float t = (float)i / steps;
                    float y = 0.5f + t * (headH - 1f);
                    float wave = (float)Math.Sin(t * Math.PI * 2.2) * 1.3f * (1f - t * 0.3f);
                    float half = 0.4f + 1.5f * (float)Math.Sin(t * Math.PI);
                    pts.Add(cx + wave + half); pts.Add(y);
                }
                FillPoly(g, blade, pts.ToArray());
                // 왼쪽 절반을 어둡게: 같은 다각형을 가운데 선 기준으로 반만
                var dark = new List<float>();
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float y = 0.5f + t * (headH - 1f);
                    float wave = (float)Math.Sin(t * Math.PI * 2.2) * 1.3f * (1f - t * 0.3f);
                    float half = 0.4f + 1.5f * (float)Math.Sin(t * Math.PI);
                    dark.Add(cx + wave - half); dark.Add(y);
                }
                for (int i = steps; i >= 0; i--)
                {
                    float t = (float)i / steps;
                    float y = 0.5f + t * (headH - 1f);
                    float wave = (float)Math.Sin(t * Math.PI * 2.2) * 1.3f * (1f - t * 0.3f);
                    dark.Add(cx + wave); dark.Add(y);
                }
                FillPoly(g, bladeDark, dark.ToArray());
            }
            else
            {
                // 잎 모양 촉: 밑에서 넓어졌다가 끝으로 뾰족
                float bottom = headH - 0.8f, mid = headH * 0.45f;
                FillPoly(g, blade, cx - 0.6f, bottom, cx, bottom, cx, 0.5f, cx - 2.2f, mid);
                FillPoly(g, bladeDark, cx, bottom, cx + 0.6f, bottom, cx + 2.2f, mid, cx, 0.5f);
                FillRect(g, Shade(blade, 1.1f), cx - 0.3f, 2f, 0.6f, bottom - 3f);     // 가운데 능선
            }
        };
    }

    /// <summary>참마도: 폭 넓은 긴 날과 붉은 가죽 손잡이. 날 끝은 비스듬히 잘려 있다.</summary>
    static Action<Graphics, int, int> DrawBroadBlade()
    {
        return delegate(Graphics g, int w, int h)
        {
            float cx = w * 0.5f;
            float gripTop = h - 11f;
            FillRect(g, Leather, cx - 1.4f, gripTop, 2.8f, 10f);                      // 손잡이
            for (int i = 0; i < 4; i++) FillRect(g, Shade(Leather, 0.6f), cx - 1.4f, gripTop + 1.5f + i * 2.4f, 2.8f, 0.7f); // 감은 자국
            FillRect(g, Gold, cx - 2.2f, h - 1.6f, 4.4f, 1.6f);
            FillRect(g, Gold, cx - 4.2f, gripTop - 1.8f, 8.4f, 1.8f);                 // 넓은 코등이
            float bb = gripTop - 1.8f;
            FillPoly(g, SteelDark, cx - 3.2f, bb, cx + 3.2f, bb, cx + 3.2f, 4.5f, cx - 0.5f, 0.6f, cx - 3.2f, 3.5f);
            FillPoly(g, SteelLight, cx - 3.2f, bb, cx - 0.4f, bb, cx - 0.4f, 1.3f, cx - 3.2f, 3.5f);
            FillRect(g, Shade(SteelDark, 0.75f), cx + 0.8f, 6f, 0.9f, bb - 8f);     // 홈
        };
    }
}
