using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 적 걷기 스프라이트 시트. 규격은 장수 시트와 같다 (4열 프레임 x 4행 방향, 칸 96x96).
// 보병/궁병은 장수와 같은 머리 큰 2등신 몸에 머리 장식과 무기만 바꾸고, 기병/보스는 말 + 기수.
public static partial class HeroArt
{
    // ───────────────────────── 적 스타일 ─────────────────────────

    public static readonly string[] EnemyNames =
    {
        "Enemy_Soldier", "Enemy_Scout", "Enemy_YellowArcher", "Enemy_YellowTurbanGeneral",
        "Enemy_DongzhuoInfantry", "Enemy_DongzhuoCrossbow",
        "Enemy_LvbuElite", "Enemy_LvbuArcher",
        "Enemy_XiliangCavalry", "Boss_Lvbu"
    };

    static St EnemyStyleOf(string name)
    {
        var s = new St();
        s.name = name;
        s.skin = Hex("#E9C49C");
        s.hair = Hex("#2B2420");
        s.boots = Hex("#3A2A22");
        s.trim = Hex("#8A6A3A");
        s.plume = Color.FromArgb(0, 0, 0, 0);
        s.maskC = Hex("#8E3B2B");
        switch (name)
        {
            case "Enemy_Soldier":
                s.robe = Hex("#B08A52"); s.dark = Hex("#7A5C33"); s.pants = Hex("#6B5A3A");
                s.hat = "scarf"; s.hatC = Hex("#E8C23A"); s.hatDark = Hex("#B8922A"); s.prop = "spear";
                break;
            case "Enemy_Scout":
                s.robe = Hex("#C9743A"); s.dark = Hex("#8F4F22"); s.pants = Hex("#5A3A24"); s.bw = 14f;
                s.hat = "scarf"; s.hatC = Hex("#D9A23A"); s.hatDark = Hex("#A87A22"); s.mask = true; s.prop = "dagger";
                break;
            case "Enemy_YellowArcher":
                s.robe = Hex("#C9A85C"); s.dark = Hex("#8F7438"); s.pants = Hex("#6B5A3A"); s.bw = 15f;
                s.hat = "scarf"; s.hatC = Hex("#F0D04A"); s.hatDark = Hex("#B8922A"); s.prop = "bow";
                break;
            case "Enemy_YellowTurbanGeneral":
                s.robe = Hex("#E3B52E"); s.dark = Hex("#A07A12"); s.pants = Hex("#7A5A12"); s.trim = Hex("#C0392B"); s.bw = 20f;
                s.hat = "turban"; s.hatC = Hex("#F0C93A"); s.hatDark = Hex("#B8902A"); s.beard = true; s.prop = "sword";
                break;
            case "Enemy_DongzhuoInfantry":
                s.robe = Hex("#7C5BB0"); s.dark = Hex("#503A80"); s.pants = Hex("#3A2A5A"); s.trim = Steel; s.bw = 17f;
                s.hat = "helmet"; s.hatC = Hex("#9AA3B5"); s.hatDark = Hex("#6A7388"); s.plume = Hex("#C0392B"); s.prop = "spear";
                break;
            case "Enemy_DongzhuoCrossbow":
                s.robe = Hex("#8E6CC0"); s.dark = Hex("#5E4290"); s.pants = Hex("#3A2A5A"); s.trim = Steel; s.bw = 16f;
                s.hat = "helmet"; s.hatC = Hex("#7F8798"); s.hatDark = Hex("#565E70"); s.prop = "crossbow";
                break;
            case "Enemy_LvbuElite":
                s.robe = Hex("#D9692A"); s.dark = Hex("#8F3A12"); s.pants = Hex("#5A2A12"); s.trim = Hex("#F2C14E"); s.bw = 18f;
                s.hat = "gold"; s.hatC = Hex("#E8B94A"); s.hatDark = Hex("#A07818"); s.plume = Hex("#2E8C7A"); s.feathers = 1; s.prop = "halberd";
                break;
            case "Enemy_LvbuArcher":
                s.robe = Hex("#E07A38"); s.dark = Hex("#9A4418"); s.pants = Hex("#5A2A12"); s.trim = Hex("#F2C14E"); s.bw = 16f;
                s.hat = "gold"; s.hatC = Hex("#D9A23A"); s.hatDark = Hex("#A07818"); s.plume = Hex("#2E8C7A"); s.feathers = 1; s.prop = "longbow";
                break;
            case "Enemy_XiliangCavalry":
                s.robe = Hex("#C98A3A"); s.dark = Hex("#8A5A22"); s.pants = Hex("#5A3A24");
                s.hat = "fur"; s.hatC = Hex("#6B4A32"); s.hatDark = Hex("#4A3220"); s.prop = "spear";
                s.cav = true; s.horse = Hex("#8A5A32"); s.horseDark = Hex("#5E3A1E");
                break;
            default: // Boss_Lvbu
                s.skin = Hex("#E4B690");
                s.robe = Hex("#C8452A"); s.dark = Hex("#7A2418"); s.pants = Hex("#5A1A12"); s.trim = Hex("#F2C14E"); s.bw = 17f;
                s.hat = "gold"; s.hatC = Hex("#F2C14E"); s.hatDark = Hex("#A07818"); s.plume = Hex("#2E8C7A"); s.feathers = 2; s.prop = "halberd";
                s.cav = true; s.horse = Hex("#B8321F"); s.horseDark = Hex("#6E1A10");
                break;
        }
        return s;
    }

    // ───────────────────────── 무기 ─────────────────────────

    static void Spear(Graphics g, float x0, float y0, float x1, float y1, bool crescent)
    {
        Limb(g, Hex("#8A6A3A"), 2.6f, x0, y0, x1, y1);
        float dx = x1 - x0, dy = y1 - y0;
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        float ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
        SF(g, Poly(x1 + nx * 2.8f, y1 + ny * 2.8f, x1 - nx * 2.8f, y1 - ny * 2.8f, x1 + ux * 10, y1 + uy * 10), Steel);
        if (crescent)
        {
            SF(g, Poly(x1 - ux * 4 + nx * 2, y1 - uy * 4 + ny * 2, x1 + ux * 3 + nx * 9, y1 + uy * 3 + ny * 9, x1 - ux * 1 + nx * 2, y1 - uy * 1 + ny * 2), Steel);
        }
        SDot(g, Hex("#C0392B"), x1 - ux * 4, y1 - uy * 4, 2.2f, 2.2f);
    }

    static void Blade(Graphics g, float x0, float y0, float x1, float y1, float w)
    {
        Limb(g, Steel, w, x0, y0, x1, y1);
        float dx = x1 - x0, dy = y1 - y0;
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        float ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
        // 칼날 아래의 날밑 + 손잡이
        Limb(g, Gold, 2.6f, x0 + nx * 4, y0 + ny * 4, x0 - nx * 4, y0 - ny * 4);
        Limb(g, Hex("#5A3A24"), 2.8f, x0, y0, x0 - ux * 5, y0 - uy * 5);
    }

    static void Bow(Graphics g, float cx, float cy, float h, float bulge, Color wood)
    {
        var path = new GraphicsPath();
        path.AddBezier(cx, cy - h / 2, cx + bulge * 1.3f, cy - h * 0.28f, cx + bulge * 1.3f, cy + h * 0.28f, cx, cy + h / 2);
        using (var pen = new Pen(Ink, 4.6f)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawPath(pen, path); }
        using (var pen = new Pen(wood, 2.8f)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawPath(pen, path); }
        Line(g, Hex("#EDE6D0"), 0.9f, cx, cy - h / 2, cx, cy + h / 2);
    }

    static float BowHeight(St st) { return st.prop == "longbow" ? 46f : 34f; }

    // ───────────────────────── 머리 장식 ─────────────────────────

    static GraphicsPath CapBack()
    {
        return Smooth(28, 33, 28, 17, 36, 9, 48, 7, 60, 9, 68, 17, 68, 33, 60, 38, 48, 40, 36, 38);
    }

    static void Feathers(Graphics g, St st, int view)
    {
        if (st.feathers <= 0 || st.plume.A == 0) return;
        if (view == RIGHT)
        {
            Leaf(g, st.plume, 8, 46, 12, 34, 5, 24, 4);
            if (st.feathers > 1) Leaf(g, st.plume, 7, 50, 11, 40, 2, 31, 0);
        }
        else if (st.feathers > 1)
        {
            Leaf(g, st.plume, 8, 40, 14, 31, 8, 27, 1);
            Leaf(g, st.plume, 8, 56, 14, 65, 8, 69, 1);
        }
        else
        {
            Leaf(g, st.plume, 8, 54, 12, 62, 6, 66, 0);
        }
    }

    static void EHatFront(Graphics g, St st)
    {
        switch (st.hat)
        {
            case "scarf":
                SF(g, HairCapFront(), st.hair);
                SF(g, Smooth(28, 26, 28, 16, 36, 9, 48, 7, 60, 9, 68, 16, 68, 26, 60, 22, 48, 19.5f, 36, 22), st.hatC);
                Line(g, st.hatDark, 2f, 29, 22, 48, 19.5f, 67, 22);
                Leaf(g, st.hatC, 5, 66, 17, 73, 24, 75, 35);
                if (st.mask) SF(g, Smooth(33, 38, 63, 38, 62, 46, 48, 51, 34, 46), st.maskC);
                break;
            case "helmet":
                SF(g, HairCapFront(), st.hatC);
                Line(g, st.hatDark, 2.6f, 28, 24, 48, 21, 68, 24);
                SF(g, Ell(28.5f, 33, 3f, 6f), st.hatDark);
                SF(g, Ell(67.5f, 33, 3f, 6f), st.hatDark);
                if (st.plume.A > 0) SF(g, RR(45.5f, 1.5f, 5, 8, 2), st.plume);
                break;
            case "gold":
                SF(g, HairCapFront(), st.hatC);
                Line(g, st.trim, 2.6f, 29, 22, 48, 19.5f, 67, 22);
                SDot(g, Hex("#C0392B"), 48, 15, 2f, 2f);
                SF(g, Ell(28.5f, 32, 3f, 6f), st.hatDark);
                SF(g, Ell(67.5f, 32, 3f, 6f), st.hatDark);
                Feathers(g, st, DOWN);
                break;
            case "turban":
                SF(g, Smooth(23, 29, 23, 15, 33, 6, 48, 3, 63, 6, 73, 15, 73, 29, 63, 22, 48, 19, 33, 22), st.hatC);
                Line(g, st.hatDark, 1.8f, 28, 14, 40, 9, 56, 8);
                Line(g, st.hatDark, 1.8f, 26, 20, 48, 14, 70, 20);
                SDot(g, Hex("#C0392B"), 48, 12, 3f, 3f);
                break;
            default: // fur
                SF(g, HairCapFront(), st.hatC);
                SF(g, RR(27, 16, 42, 8, 4), Shade(st.hatC, 1.3f));
                break;
        }
        if (st.beard) SF(g, Smooth(29, 38, 28, 48, 36, 57, 48, 62, 60, 57, 68, 48, 67, 38, 60, 43, 48, 46, 36, 43), st.hair);
    }

    static void EHatBack(Graphics g, St st)
    {
        switch (st.hat)
        {
            case "scarf":
                SF(g, CapBack(), st.hatC);
                Line(g, st.hatDark, 2f, 29, 24, 48, 22, 67, 24);
                SDot(g, st.hatC, 48, 37, 4.2f, 4f);
                Leaf(g, st.hatC, 5, 48, 38, 40, 47, 38, 59);
                Leaf(g, st.hatC, 5, 48, 38, 56, 47, 60, 57);
                break;
            case "helmet":
                SF(g, CapBack(), st.hatC);
                Line(g, st.hatDark, 2.6f, 28, 26, 48, 23, 68, 26);
                if (st.plume.A > 0) SF(g, RR(45.5f, 1.5f, 5, 8, 2), st.plume);
                break;
            case "gold":
                SF(g, CapBack(), st.hatC);
                Line(g, st.trim, 2.6f, 29, 24, 48, 22, 67, 24);
                SF(g, Ell(48, 40, 6, 3f), st.hatDark);
                Feathers(g, st, UP);
                break;
            case "turban":
                SF(g, Smooth(23, 34, 23, 15, 33, 6, 48, 3, 63, 6, 73, 15, 73, 34, 63, 40, 48, 42, 33, 40), st.hatC);
                Line(g, st.hatDark, 1.8f, 26, 22, 48, 17, 70, 22);
                Leaf(g, st.hatC, 6, 48, 40, 54, 50, 56, 62);
                break;
            default:
                SF(g, CapBack(), st.hatC);
                SF(g, RR(27, 16, 42, 8, 4), Shade(st.hatC, 1.3f));
                break;
        }
    }

    static GraphicsPath CapSide()
    {
        return Smooth(32, 36, 31, 19, 40, 10, 54, 8, 65, 12, 68, 22, 58, 20, 50, 22, 46, 28, 46, 36);
    }

    static void EHatSide(Graphics g, St st)
    {
        switch (st.hat)
        {
            case "scarf":
                SF(g, HairSide(), st.hair);
                SF(g, CapSide(), st.hatC);
                Line(g, st.hatDark, 2f, 34, 21, 50, 21, 67, 22);
                Leaf(g, st.hatC, 5, 36, 24, 26, 30, 22, 42);
                if (st.mask) SF(g, Smooth(52, 38, 67, 38, 67, 46, 58, 51, 51, 46), st.maskC);
                break;
            case "helmet":
                SF(g, CapSide(), st.hatC);
                Line(g, st.hatDark, 2.6f, 34, 22, 50, 21, 67, 23);
                SF(g, Ell(46, 33, 3f, 6f), st.hatDark);
                if (st.plume.A > 0) SF(g, RR(40, 2, 5, 8, 2), st.plume);
                break;
            case "gold":
                SF(g, CapSide(), st.hatC);
                Line(g, st.trim, 2.6f, 34, 21, 50, 21, 67, 22);
                SDot(g, Hex("#C0392B"), 56, 14, 2f, 2f);
                SF(g, Ell(46, 33, 3f, 6f), st.hatDark);
                Feathers(g, st, RIGHT);
                break;
            case "turban":
                SF(g, Smooth(30, 38, 28, 18, 38, 7, 54, 4, 68, 9, 72, 22, 62, 20, 50, 23, 45, 29, 45, 38), st.hatC);
                Line(g, st.hatDark, 1.8f, 32, 16, 46, 10, 64, 11);
                Line(g, st.hatDark, 1.8f, 31, 23, 52, 17, 70, 22);
                SDot(g, Hex("#C0392B"), 56, 13, 3f, 3f);
                break;
            default:
                SF(g, CapSide(), st.hatC);
                SF(g, RR(31, 15, 38, 8, 4), Shade(st.hatC, 1.3f));
                break;
        }
        if (st.beard) SF(g, Smooth(46, 38, 44, 48, 50, 58, 58, 57, 66, 49, 67, 40, 60, 44, 54, 41), st.hair);
    }

    // ───────────────────────── 손에 든 무기 ─────────────────────────

    static void HeldDown(Graphics g, St st, float sw, bool back)
    {
        // 정면: 오른손(화면 오른쪽)에 창/칼, 왼손(화면 왼쪽)에 활. 뒷모습은 좌우가 뒤바뀐다.
        float rx = back ? 48 - (st.bw + 4f) : 48 + (st.bw + 4f);
        float lx = back ? 48 + (st.bw + 4f) : 48 - (st.bw + 4f);
        float ry = 67 + (back ? -sw : sw) * 3f;
        float ly = 67 + (back ? sw : -sw) * 3f;
        switch (st.prop)
        {
            case "spear": Spear(g, rx, ry + 16, rx + 1, ry - 58, false); break;
            case "halberd": Spear(g, rx, ry + 16, rx + 1, ry - 56, true); break;
            case "dagger": Blade(g, rx, ry - 1, rx, ry - 16, 3f); break;
            case "sword": Blade(g, rx, ry - 1, rx + 2, ry - 32, 4.6f); break;
            case "bow":
            case "longbow":
                Bow(g, lx, ly - 4, BowHeight(st), back ? 5f : -5f, Hex("#8A5A2A"));
                break;
            case "crossbow":
                if (!back)
                {
                    SF(g, RR(34, 57, 28, 5, 2), Hex("#8A5A2A"));
                    SF(g, RR(28, 54.5f, 40, 3, 1.5f), Hex("#4A4A58"));
                    Line(g, Hex("#EDE6D0"), 0.9f, 29, 56, 48, 60, 67, 56);
                }
                break;
        }
    }

    // early = true 면 머리 뒤에 그릴 무기(창/칼), false 면 머리 앞에 그릴 무기(활/쇠뇌)
    static void HeldSide(Graphics g, St st, float near, bool early)
    {
        float hx = 48 + near * 1.1f, hy = 67.5f;
        bool isEarly = st.prop == "spear" || st.prop == "halberd" || st.prop == "dagger" || st.prop == "sword";
        if (isEarly != early) return;
        switch (st.prop)
        {
            case "spear": Spear(g, hx - 6, hy + 12, hx + 24, hy - 38, false); break;
            case "halberd": Spear(g, hx - 6, hy + 12, hx + 24, hy - 36, true); break;
            case "dagger": Blade(g, hx, hy - 1, hx + 10, hy - 9, 3f); break;
            case "sword": Blade(g, hx, hy - 1, hx + 14, hy - 24, 4.6f); break;
            case "bow":
            case "longbow":
                Bow(g, hx + 15, hy - 8, BowHeight(st), 6f, Hex("#8A5A2A"));
                SDot(g, st.skin, hx + 1, hy - 4, 3.2f, 3.2f);
                break;
            case "crossbow":
                SF(g, RR(hx - 8, hy - 11, 30, 5, 2), Hex("#8A5A2A"));
                SF(g, RR(hx + 17, hy - 18, 3, 18, 1.5f), Hex("#4A4A58"));
                Line(g, Hex("#EDE6D0"), 0.9f, hx + 18, hy - 17, hx - 4, hy - 9, hx + 18, hy - 1);
                SDot(g, st.skin, hx + 1, hy - 5, 3.2f, 3.2f);
                break;
        }
    }

    static bool IsArcher(St st) { return st.prop == "bow" || st.prop == "longbow" || st.prop == "crossbow"; }

    static void QuiverBack(Graphics g, St st, int view)
    {
        if (st.prop == "crossbow") return;
        if (!IsArcher(st)) return;
        Color wood = Hex("#7A5030");
        if (view == UP)
        {
            Limb(g, wood, 5.5f, 60, 50, 40, 72);
            Line(g, Hex("#EDE6D0"), 1.6f, 61, 48, 64, 40);
            Line(g, Hex("#EDE6D0"), 1.6f, 58, 49, 60, 40);
        }
        else if (view == RIGHT)
        {
            Limb(g, wood, 5.5f, 36, 50, 34, 68);
            Line(g, Hex("#EDE6D0"), 1.6f, 37, 48, 40, 40);
            Line(g, Hex("#EDE6D0"), 1.6f, 34, 49, 35, 40);
        }
        else
        {
            Line(g, Hex("#EDE6D0"), 1.6f, 62, 45, 66, 37);
            Line(g, Hex("#EDE6D0"), 1.6f, 59, 46, 61, 37);
        }
    }

    // ───────────────────────── 보병/궁병 그리기 ─────────────────────────

    static void EDrawDown(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        LegsFront(g, st, frame);
        var s = g.Save();
        g.TranslateTransform(0, bob);
        TorsoFront(g, st, false);
        QuiverBack(g, st, DOWN);
        ArmFront(g, st, -1, -sw * 3f);
        ArmFront(g, st, 1, sw * 3f);
        HeadFront(g, st, true);
        EHatFront(g, st);
        HeldDown(g, st, sw, false);
        g.Restore(s);
    }

    static void EDrawUp(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        LegsFront(g, st, frame);
        var s = g.Save();
        g.TranslateTransform(0, bob);
        TorsoFront(g, st, true);
        QuiverBack(g, st, UP);
        ArmFront(g, st, -1, sw * 3f);
        ArmFront(g, st, 1, -sw * 3f);
        BackHead(g, st);
        EHatBack(g, st);
        HeldDown(g, st, sw, true);
        g.Restore(s);
    }

    static void TorsoSide(Graphics g, St st)
    {
        float hw = st.bw * 0.72f;
        var torso = Smooth(48 - hw * 0.8f, 46, 48 + hw * 0.8f, 46, 48 + hw, 62, 48 + hw + 1, 77, 48 - hw - 1, 77, 48 - hw, 62);
        SF(g, torso, st.robe);
        ClipFill(g, torso, Ell(48 - hw - 3, 66, hw * 0.5f, 26), Shade(st.dark, 1f, 110));
        SF(g, RR(48 - hw - 0.5f, 60, hw * 2 + 1, 4.5f, 1.5f), st.trim);
    }

    static void EDrawRight(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        LegsSide(g, st, frame, false);
        LegsSide(g, st, frame, true);
        var s = g.Save();
        g.TranslateTransform(0, bob);
        QuiverBack(g, st, RIGHT);
        float far = sw * 5f;
        Limb(g, Shade(st.robe, 0.78f), 6.5f, 48 + far * 0.4f, 51, 48 + far, 65);
        SDot(g, Shade(st.skin, 0.85f), 48 + far * 1.1f, 67.5f, 3f, 3f);
        TorsoSide(g, st);
        float near = -sw * 5f;
        Limb(g, st.robe, 7, 48 + near * 0.4f, 51, 48 + near, 65);
        SDot(g, st.skin, 48 + near * 1.1f, 67.5f, 3.3f, 3.3f);
        HeldSide(g, st, near, true);
        HeadSide(g, st);
        EHatSide(g, st);
        HeldSide(g, st, near, false);
        g.Restore(s);
    }

    // ───────────────────────── 기병/보스 (말 + 기수) ─────────────────────────

    const float RiderScale = 0.66f;

    static void WithRider(Graphics g, float shiftX, float hipY, Action draw)
    {
        // 기수의 엉덩이(원래 y=77)가 hipY 에 오도록 줄여서 말 위에 얹는다
        var s = g.Save();
        g.TranslateTransform(48 + shiftX, hipY - (77 - 88) * RiderScale);
        g.ScaleTransform(RiderScale, RiderScale);
        g.TranslateTransform(-48, -88);
        draw();
        g.Restore(s);
    }

    static void HoofLeg(Graphics g, Color c, float x, float yTop, float xOff)
    {
        Limb(g, c, 5.2f, x, yTop, x + xOff, 83);
        SF(g, RR(x + xOff - 3.2f, 82, 6.6f, 5.5f, 2), Hex("#2A1A14"));
    }

    static void EDrawCavRight(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        // 다리: 대각선 쌍이 같은 위상
        HoofLeg(g, st.horseDark, 33, 66, -sw * 6f);
        HoofLeg(g, st.horseDark, 67, 66, sw * 6f);
        var s = g.Save();
        g.TranslateTransform(0, bob);
        // 꼬리, 몸통, 목, 머리
        Taper(g, st.horseDark, 7, 2, 18, 56, 7, 60 + sw * 3f, 9, 78);
        var body = Smooth(14, 62, 20, 52, 40, 48, 62, 50, 72, 58, 68, 72, 44, 76, 22, 74);
        SF(g, body, st.horse);
        ClipFill(g, body, Ell(44, 80, 36, 10), Color.FromArgb(90, 0, 0, 0));
        HoofLeg(g, st.horse, 27, 66, sw * 6f);
        HoofLeg(g, st.horse, 61, 66, -sw * 6f);
        SF(g, Poly(62, 54, 66, 38, 78, 32, 83, 44, 75, 60), st.horse);
        SF(g, Smooth(72, 31, 85, 32, 94, 45, 90, 53, 80, 51, 74, 42), st.horse);
        SF(g, Poly(74, 31, 76, 24, 80, 31), st.horse);
        SDot(g, Ink, 82, 38, 1.5f, 1.8f);
        SDot(g, Shade(st.horseDark, 0.7f), 91, 48, 1.4f, 1.6f);
        Taper(g, st.horseDark, 7, 2, 71, 31, 62, 40, 62, 53);
        // 안장 + 기수
        SF(g, RR(33, 45, 24, 7, 3), Hex("#8A2A2A"));
        WithRider(g, -2, 50, delegate
        {
            LegsSide(g, st, 0, false);
            TorsoSide(g, st);
            float near = 0;
            Limb(g, st.robe, 7, 48, 51, 48 + near, 65);
            SDot(g, st.skin, 48, 67.5f, 3.3f, 3.3f);
            HeldSide(g, st, 0, true);
            HeadSide(g, st);
            EHatSide(g, st);
            HeldSide(g, st, 0, false);
        });
        g.Restore(s);
    }

    static void EDrawCavDown(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        for (int side = -1; side <= 1; side += 2)
        {
            float lift = (sw * side < 0) ? 3f : 0f;
            var ls = g.Save();
            g.TranslateTransform(0, -lift);
            SF(g, RR(48 + side * 14 - 4, 68, 8, 20, 3), st.horse);
            SF(g, RR(48 + side * 14 - 4.5f, 82, 9, 6, 2), Hex("#2A1A14"));
            g.Restore(ls);
        }
        var s = g.Save();
        g.TranslateTransform(0, bob);
        SF(g, Ell(48, 62, 24, 17), st.horse);
        WithRider(g, 0, 52, delegate
        {
            TorsoFront(g, st, false);
            ArmFront(g, st, -1, 0);
            ArmFront(g, st, 1, 0);
            HeadFront(g, st, true);
            EHatFront(g, st);
            HeldDown(g, st, 0, false);
        });
        // 목 + 머리 (정면에서 기수 앞을 가림)
        SF(g, Smooth(39, 58, 57, 58, 59, 74, 48, 83, 37, 74), st.horse);
        SF(g, Poly(40, 62, 39, 53, 46, 60), st.horse);
        SF(g, Poly(56, 62, 57, 53, 50, 60), st.horse);
        SF(g, Smooth(40, 60, 56, 60, 54, 67, 42, 67), st.horseDark);
        SF(g, Ell(48, 76, 9, 10), st.horse);
        SDot(g, Shade(st.horse, 1.35f), 48, 82, 6, 3.6f);
        SDot(g, Ink, 43, 73, 1.5f, 1.8f);
        SDot(g, Ink, 53, 73, 1.5f, 1.8f);
        SDot(g, Shade(st.horseDark, 0.7f), 45.5f, 82, 1.1f, 1.3f);
        SDot(g, Shade(st.horseDark, 0.7f), 50.5f, 82, 1.1f, 1.3f);
        g.Restore(s);
    }

    static void EDrawCavUp(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        for (int side = -1; side <= 1; side += 2)
        {
            float lift = (sw * side < 0) ? 3f : 0f;
            var ls = g.Save();
            g.TranslateTransform(0, -lift);
            SF(g, RR(48 + side * 14 - 4, 68, 8, 20, 3), st.horse);
            SF(g, RR(48 + side * 14 - 4.5f, 82, 9, 6, 2), Hex("#2A1A14"));
            g.Restore(ls);
        }
        var s = g.Save();
        g.TranslateTransform(0, bob);
        SF(g, Ell(48, 62, 24, 17), st.horse);
        WithRider(g, 0, 52, delegate
        {
            TorsoFront(g, st, true);
            ArmFront(g, st, -1, 0);
            ArmFront(g, st, 1, 0);
            BackHead(g, st);
            EHatBack(g, st);
            HeldDown(g, st, 0, true);
        });
        Taper(g, st.horseDark, 10, 3, 48, 58, 48 + sw * 7f, 72, 48 + sw * 4f, 86);
        g.Restore(s);
    }

    // ───────────────────────── 시트 ─────────────────────────

    static void DrawEnemyCell(Graphics g, St st, int dir, int frame)
    {
        if (st.cav)
        {
            if (dir == DOWN) EDrawCavDown(g, st, frame);
            else if (dir == UP) EDrawCavUp(g, st, frame);
            else if (dir == RIGHT) EDrawCavRight(g, st, frame);
            else
            {
                var s = g.Save();
                g.TranslateTransform(CELL, 0);
                g.ScaleTransform(-1, 1);
                EDrawCavRight(g, st, frame);
                g.Restore(s);
            }
            return;
        }

        if (dir == DOWN) EDrawDown(g, st, frame);
        else if (dir == UP) EDrawUp(g, st, frame);
        else if (dir == RIGHT) EDrawRight(g, st, frame);
        else
        {
            var s = g.Save();
            g.TranslateTransform(CELL, 0);
            g.ScaleTransform(-1, 1);
            EDrawRight(g, st, frame);
            g.Restore(s);
        }
    }

    public static Bitmap RenderEnemySheet(string name)
    {
        var st = EnemyStyleOf(name);
        int size = CELL * 4;
        var big = new Bitmap(size * SS, size * SS, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    var state = g.Save();
                    g.ScaleTransform(SS, SS);
                    g.TranslateTransform(col * CELL, row * CELL);
                    g.SetClip(new RectangleF(0, 0, CELL, CELL));
                    DrawEnemyCell(g, st, row, col);
                    g.Restore(state);
                }
        }

        var sheet = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(big, new Rectangle(0, 0, size, size), 0, 0, big.Width, big.Height, GraphicsUnit.Pixel);
        }
        big.Dispose();
        return sheet;
    }

    public static void GenerateEnemySheets(string outDir, string previewPath)
    {
        Directory.CreateDirectory(outDir);
        float zoom = 1.3f;
        int cw = (int)(CELL * zoom);
        var prev = new Bitmap(cw * 8 + 20, cw * EnemyNames.Length + 20, PixelFormat.Format32bppArgb);
        using (var pg = Graphics.FromImage(prev))
        {
            pg.Clear(Color.FromArgb(255, 80, 96, 62));
            pg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < EnemyNames.Length; i++)
            {
                using (var sheet = RenderEnemySheet(EnemyNames[i]))
                {
                    sheet.Save(Path.Combine(outDir, EnemyNames[i] + "_Walk.png"), ImageFormat.Png);
                    for (int d = 0; d < 4; d++)
                        pg.DrawImage(sheet, new Rectangle(10 + d * cw, 10 + i * cw, cw, cw),
                            new Rectangle(0, d * CELL, CELL, CELL), GraphicsUnit.Pixel);
                    for (int f = 0; f < 4; f++)
                        pg.DrawImage(sheet, new Rectangle(10 + (4 + f) * cw, 10 + i * cw, cw, cw),
                            new Rectangle(f * CELL, 3 * CELL, CELL, CELL), GraphicsUnit.Pixel);
                }
            }
        }
        prev.Save(previewPath, ImageFormat.Png);
        prev.Dispose();
    }
}
