using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 게임 안에서 쓰는 장수 걷기 스프라이트 시트.
// 시트 배치: 열 = 걷기 프레임 4장, 행 = 방향(위에서부터 아래, 위, 왼쪽, 오른쪽). 한 칸 96x96.
// 프레임: 0 = 서 있기(정지 자세), 1 = 왼발/한쪽 앞, 2 = 서 있기(몸이 가장 높음), 3 = 반대쪽 앞.
public static partial class HeroArt
{
    const int CELL = 96;
    const int SS = 4;          // 슈퍼샘플링 배수
    const float LW = 1.7f;     // 외곽선 굵기 (96 기준)

    public const int DOWN = 0, UP = 1, LEFT = 2, RIGHT = 3;

    class St
    {
        public string name;
        public Color skin, robe, dark, trim, pants, boots, hair, accent;
        public float bw = 16f;   // 몸통 반폭
    }

    static St StyleOf(string name)
    {
        var s = new St();
        s.name = name;
        s.hair = Hex("#2B2420");
        s.boots = Hex("#3A2A22");
        s.trim = Gold;
        switch (name)
        {
            case "Hero_LiuBei":
                s.skin = Hex("#F2CFA8"); s.robe = Hex("#3FB37F"); s.dark = Hex("#1F7A55"); s.pants = Hex("#EDE3CC"); s.accent = Hex("#8A3B34");
                break;
            case "Hero_GuanYu":
                s.skin = Hex("#D2674E"); s.robe = Hex("#1F6F46"); s.dark = Hex("#124A2E"); s.pants = Hex("#16492E"); s.accent = Hex("#2A8A55");
                s.bw = 17f;
                break;
            case "Hero_ZhangFei":
                s.skin = Hex("#CC9366"); s.robe = Hex("#566A9C"); s.dark = Hex("#36457A"); s.pants = Hex("#2E3860"); s.accent = Hex("#C0392B");
                s.trim = Steel; s.bw = 19f;
                break;
            case "Hero_CaoCao":
                s.skin = Hex("#ECCBA8"); s.robe = Hex("#3A62D0"); s.dark = Hex("#223C8F"); s.pants = Hex("#1B2757"); s.accent = Hex("#1B2757");
                break;
            default: // Hero_LvBu
                s.skin = Hex("#E4B690"); s.robe = Hex("#C8452A"); s.dark = Hex("#7A2418"); s.pants = Hex("#5A1A12"); s.accent = Hex("#8E1F1F");
                s.trim = Hex("#F2C14E"); s.bw = 17f;
                break;
        }
        return s;
    }

    // ───────────────────────── 작은 도구 (96 기준) ─────────────────────────

    static GraphicsPath RR(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void SF(Graphics g, GraphicsPath p, Color c) { Fill(g, p, c, LW); }

    // 외곽선 있는 굵은 선 (팔다리)
    static void Limb(Graphics g, Color c, float w, float x0, float y0, float x1, float y1)
    {
        using (var pen = new Pen(Ink, w + LW * 1.6f)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, x0, y0, x1, y1); }
        using (var pen = new Pen(c, w)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, x0, y0, x1, y1); }
    }

    static void Line(Graphics g, Color c, float w, params float[] xy)
    {
        var pts = Pts(xy);
        using (var pen = new Pen(c, w)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; DrawOpen(g, pen, pts); }
    }

    static void SDot(Graphics g, Color c, float cx, float cy, float rx, float ry)
    {
        using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
    }

    static void Leaf(Graphics g, Color c, float w, float x0, float y0, float cx, float cy, float x1, float y1)
    {
        Taper(g, c, 0, 0, w, 1.4f, x0, y0, cx, cy, x1, y1);
    }

    static void Shadow(Graphics g)
    {
        SDot(g, Color.FromArgb(70, 0, 0, 0), 48, 88, 20, 5);
    }

    static float Swing(int frame) { return frame == 1 ? 1f : (frame == 3 ? -1f : 0f); }
    static float Bob(int frame) { return (frame == 1 || frame == 3) ? 1.3f : 0f; }

    // ───────────────────────── 공통 부품 ─────────────────────────

    static void LegsFront(Graphics g, St st, int frame)
    {
        float sw = Swing(frame);
        for (int side = -1; side <= 1; side += 2)
        {
            // sw>0 이면 왼쪽(side=-1) 다리가 디딤, 오른쪽이 들림
            float lift = (sw * side < 0) ? 3f : 0f;
            float x = side < 0 ? 38f : 49f;
            var s = g.Save();
            g.TranslateTransform(0, -lift);
            SF(g, RR(x, 68, 10, 18, 3), st.pants);
            SF(g, RR(x - 1f, 81, 12, 7, 3), st.boots);
            g.Restore(s);
        }
    }

    static void LegsSide(Graphics g, St st, int frame, bool near)
    {
        float sw = Swing(frame);
        float off = near ? sw * 6f : -sw * 6f;
        Color pants = near ? st.pants : Shade(st.pants, 0.78f);
        Color boots = near ? st.boots : Shade(st.boots, 0.85f);
        Limb(g, pants, 8, 48 + off * 0.3f, 72, 48 + off, 82);
        SF(g, RR(44 + off, 81, 13, 7, 3), boots);
    }

    static void TorsoFront(Graphics g, St st, bool back)
    {
        float bw = st.bw;
        var robe = Smooth(48 - bw * 0.8f, 46, 48 + bw * 0.8f, 46, 48 + bw, 62, 48 + bw + 2, 77, 48 - bw - 2, 77, 48 - bw, 62);
        SF(g, robe, st.robe);
        ClipFill(g, robe, Ell(48 + bw + 4, 66, bw * 0.55f, 26), Shade(st.dark, 1f, 120));
        // 허리띠
        SF(g, RR(48 - bw - 0.5f, 60, bw * 2 + 1, 4.5f, 1.5f), st.trim);
        if (!back)
        {
            SF(g, Poly(42, 46, 48, 56, 54, 46), Shade(st.pants, 1.15f));
            Line(g, st.trim, 2.6f, 41, 46, 48, 57, 55, 46);
        }
    }

    static void ArmFront(Graphics g, St st, int side, float dy)
    {
        float x = 48 + side * (st.bw + 3f);
        Limb(g, st.robe, 7, x, 50 + dy, x + side * 1f, 64 + dy);
        SDot(g, st.skin, x + side * 1f, 67 + dy, 3.3f, 3.3f);
        Line(g, Ink, 1f, x - 3, 67 + dy, x + 3, 67 + dy);
    }

    static void HeadFront(Graphics g, St st, bool face)
    {
        SF(g, Ell(28.5f, 33, 3.5f, 5), st.skin);
        SF(g, Ell(67.5f, 33, 3.5f, 5), st.skin);
        var head = Ell(48, 30, 20, 19);
        SF(g, head, st.skin);
        ClipFill(g, head, Ell(66, 32, 9, 22), Shade(st.skin, 0.78f, 75));
        if (face)
        {
            ClipFill(g, head, Ell(35, 40, 5, 3), Color.FromArgb(60, 230, 90, 80));
            ClipFill(g, head, Ell(61, 40, 5, 3), Color.FromArgb(60, 230, 90, 80));
            SDot(g, Ink, 40.5f, 34, 2.4f, 3.3f);
            SDot(g, Ink, 55.5f, 34, 2.4f, 3.3f);
            SDot(g, Color.White, 41.2f, 32.8f, 0.9f, 0.9f);
            SDot(g, Color.White, 56.2f, 32.8f, 0.9f, 0.9f);
            Line(g, Hex("#7A2E2E"), 1.5f, 44.5f, 42.5f, 48, 43.7f, 51.5f, 42.5f);
        }
    }

    static GraphicsPath HairCapFront()
    {
        return Smooth(28, 30, 28, 17, 36, 9, 48, 7, 60, 9, 68, 17, 68, 30, 61, 22, 48, 19, 35, 22);
    }

    static void BackHead(Graphics g, St st)
    {
        SF(g, Ell(28.5f, 33, 3.5f, 5), st.skin);
        SF(g, Ell(67.5f, 33, 3.5f, 5), st.skin);
        SF(g, Ell(48, 30, 20, 19), st.hair);
        ClipFill(g, Ell(48, 30, 20, 19), Ell(66, 32, 9, 22), Color.FromArgb(70, 0, 0, 0));
    }

    // ───────────────────────── 아래(정면) ─────────────────────────

    static void DrawDown(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        if (st.name == "Hero_LvBu")
        {
            Leaf(g, Hex("#2E8C7A"), 8, 40, 14, 31, 8, 27, 1);
            Leaf(g, Hex("#2E8C7A"), 8, 56, 14, 65, 8, 69, 1);
        }
        LegsFront(g, st, frame);

        var s = g.Save();
        g.TranslateTransform(0, bob);
        if (st.name == "Hero_CaoCao") Fill(g, Smooth(26, 48, 70, 48, 76, 70, 70, 84, 26, 84, 20, 70), Hex("#1B2757"), LW);
        TorsoFront(g, st, false);
        ArmFront(g, st, -1, -sw * 3f);
        ArmFront(g, st, 1, sw * 3f);
        if (st.name == "Hero_ZhangFei")
        {
            SF(g, Poly(30, 46, 26, 38, 36, 44), Steel);
            SF(g, Poly(66, 46, 70, 38, 60, 44), Steel);
        }
        HeadFront(g, st, true);
        HatFront(g, st);
        g.Restore(s);
    }

    static void HatFront(Graphics g, St st)
    {
        switch (st.name)
        {
            case "Hero_LiuBei":
                SF(g, HairCapFront(), st.hair);
                SDot(g, st.hair, 48, 7, 5.5f, 5f);
                SF(g, RR(43, 10, 10, 5, 2), Gold);
                SDot(g, Hex("#E15B4A"), 48, 12.5f, 1.3f, 1.3f);
                Line(g, st.hair, 2f, 47, 39, 43, 41, 40.5f, 42);
                Line(g, st.hair, 2f, 49, 39, 53, 41, 55.5f, 42);
                SF(g, Smooth(46, 46, 50, 46, 50, 52, 48, 55, 46, 52), st.hair);
                break;
            case "Hero_GuanYu":
                SF(g, Smooth(29, 40, 28, 50, 35, 66, 48, 77, 61, 66, 68, 50, 67, 40, 60, 44, 48, 47, 36, 44), st.hair);
                Line(g, Hex("#5A4C44"), 1.2f, 40, 50, 40, 62, 42, 70);
                Line(g, Hex("#5A4C44"), 1.2f, 48, 50, 48, 66, 48, 74);
                Line(g, Hex("#5A4C44"), 1.2f, 56, 50, 56, 62, 54, 70);
                Line(g, st.hair, 2.4f, 47, 40, 40, 42, 36, 46);
                Line(g, st.hair, 2.4f, 49, 40, 56, 42, 60, 46);
                SF(g, HairCapFront(), st.accent);
                SDot(g, st.accent, 48, 6, 5, 4.6f);
                Line(g, Gold, 2.2f, 29, 22, 48, 19.5f, 67, 22);
                SDot(g, Hex("#C0392B"), 48, 20, 1.6f, 1.6f);
                break;
            case "Hero_ZhangFei":
                SF(g, Poly(28, 28, 26, 18, 30, 12, 32, 3, 38, 10, 42, 1, 48, 8, 54, 1, 58, 10, 64, 3, 66, 12, 70, 18, 68, 28, 62, 22, 48, 19, 34, 22), st.hair);
                Line(g, st.accent, 3.6f, 29, 23, 48, 20.5f, 67, 23);
                SF(g, Poly(29, 36, 26, 44, 31, 50, 35, 57, 41, 52, 48, 59, 55, 52, 61, 57, 65, 50, 70, 44, 67, 36, 59, 42, 48, 44, 37, 42), st.hair);
                SF(g, Ell(48, 46, 4.5f, 3f), Hex("#5A1414"));
                break;
            case "Hero_CaoCao":
                SF(g, HairCapFront(), st.hair);
                SF(g, RR(38, 3, 20, 14, 2), st.hair);
                SF(g, RR(35, 1, 26, 4, 1.5f), Gold);
                Line(g, Gold, 2.2f, 38, 14, 58, 14);
                SDot(g, Hex("#E15B4A"), 48, 14, 1.4f, 1.4f);
                Line(g, st.hair, 1.8f, 47, 39, 41, 41.5f, 36, 45);
                Line(g, st.hair, 1.8f, 49, 39, 55, 41.5f, 60, 45);
                SF(g, Poly(45, 46, 51, 46, 48, 54), st.hair);
                break;
            default: // 여포
                SF(g, HairCapFront(), Gold);
                Line(g, st.accent, 3f, 29, 22, 48, 19.5f, 67, 22);
                SDot(g, Hex("#C0392B"), 48, 15, 2.2f, 2.2f);
                SF(g, Ell(28.5f, 32, 3.2f, 6f), Gold);
                SF(g, Ell(67.5f, 32, 3.2f, 6f), Gold);
                Line(g, st.hair, 1.8f, 47, 39.5f, 41, 41.5f, 37, 44);
                Line(g, st.hair, 1.8f, 49, 39.5f, 55, 41.5f, 59, 44);
                break;
        }
    }

    // ───────────────────────── 위(뒷모습) ─────────────────────────

    static void DrawUp(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        LegsFront(g, st, frame);

        var s = g.Save();
        g.TranslateTransform(0, bob);
        // 등 뒤 소품
        if (st.name == "Hero_LiuBei")
        {
            Limb(g, st.accent, 3.6f, 40, 52, 33, 36);
            Limb(g, st.accent, 3.6f, 56, 52, 63, 36);
            SF(g, RR(29, 33, 8, 3.5f, 1.5f), Gold);
            SF(g, RR(59, 33, 8, 3.5f, 1.5f), Gold);
        }
        TorsoFront(g, st, true);
        if (st.name == "Hero_LiuBei")
        {
            // 등에서 엇갈린 칼집
            Limb(g, st.accent, 3.6f, 38, 49, 58, 72);
            Limb(g, st.accent, 3.6f, 58, 49, 38, 72);
        }
        if (st.name == "Hero_CaoCao") Fill(g, Smooth(25, 47, 71, 47, 77, 70, 71, 84, 25, 84, 19, 70), Hex("#1B2757"), LW);
        if (st.name == "Hero_LvBu") Fill(g, Smooth(25, 47, 71, 47, 77, 70, 71, 84, 25, 84, 19, 70), st.accent, LW);
        if (st.name == "Hero_LvBu" || st.name == "Hero_CaoCao")
            ClipFill(g, Smooth(25, 47, 71, 47, 77, 70, 71, 84, 25, 84, 19, 70), Ell(70, 66, 12, 26), Color.FromArgb(70, 0, 0, 0));
        ArmFront(g, st, -1, sw * 3f);
        ArmFront(g, st, 1, -sw * 3f);
        if (st.name == "Hero_ZhangFei")
        {
            SF(g, Poly(30, 46, 26, 38, 36, 44), Steel);
            SF(g, Poly(66, 46, 70, 38, 60, 44), Steel);
        }
        BackHead(g, st);
        HatBack(g, st);
        g.Restore(s);
    }

    static void HatBack(Graphics g, St st)
    {
        switch (st.name)
        {
            case "Hero_LiuBei":
                SDot(g, st.hair, 48, 7, 5.5f, 5f);
                SF(g, RR(43, 10, 10, 5, 2), Gold);
                break;
            case "Hero_GuanYu":
                SF(g, Smooth(28, 33, 28, 17, 36, 9, 48, 7, 60, 9, 68, 17, 68, 33, 60, 38, 48, 40, 36, 38), st.accent);
                SDot(g, st.accent, 48, 6, 5, 4.6f);
                Line(g, Gold, 2.2f, 29, 24, 48, 22, 67, 24);
                Line(g, st.accent, 3.5f, 48, 38, 46, 48, 49, 56);
                break;
            case "Hero_ZhangFei":
                SF(g, Poly(28, 34, 26, 20, 30, 12, 32, 3, 38, 10, 42, 1, 48, 8, 54, 1, 58, 10, 64, 3, 66, 12, 70, 20, 68, 34, 60, 40, 48, 42, 36, 40), st.hair);
                Line(g, st.accent, 3.6f, 29, 24, 48, 22, 67, 24);
                Line(g, st.accent, 3, 62, 24, 70, 34, 72, 46);
                break;
            case "Hero_CaoCao":
                SF(g, RR(38, 3, 20, 14, 2), st.hair);
                SF(g, RR(35, 1, 26, 4, 1.5f), Gold);
                Line(g, Gold, 2.2f, 38, 14, 58, 14);
                break;
            default:
                SF(g, Smooth(28, 34, 28, 17, 36, 9, 48, 7, 60, 9, 68, 17, 68, 34, 60, 40, 48, 42, 36, 40), Gold);
                Line(g, st.accent, 3f, 29, 24, 48, 22, 67, 24);
                SF(g, Ell(48, 40, 6, 3f), st.accent);
                Leaf(g, Hex("#2E8C7A"), 8, 40, 14, 31, 8, 27, 1);
                Leaf(g, Hex("#2E8C7A"), 8, 56, 14, 65, 8, 69, 1);
                break;
        }
    }

    // ───────────────────────── 옆(오른쪽을 본 모습, 왼쪽은 좌우 반전) ─────────────────────────

    static void DrawRight(Graphics g, St st, int frame)
    {
        float sw = Swing(frame), bob = Bob(frame);
        Shadow(g);
        LegsSide(g, st, frame, false);
        LegsSide(g, st, frame, true);
        // 멀리 있는 팔
        var s = g.Save();
        g.TranslateTransform(0, bob);
        float far = sw * 5f;
        Limb(g, Shade(st.robe, 0.78f), 6.5f, 48 + far * 0.4f, 51, 48 + far, 65);
        SDot(g, Shade(st.skin, 0.85f), 48 + far * 1.1f, 67.5f, 3f, 3f);

        if (st.name == "Hero_CaoCao") Fill(g, Smooth(26, 48, 46, 48, 50, 70, 40, 84, 22, 84, 20, 66), Hex("#1B2757"), LW);
        if (st.name == "Hero_LvBu") Fill(g, Smooth(26, 48, 46, 48, 50, 70, 40, 84, 22, 84, 20, 66), st.accent, LW);

        float hw = st.bw * 0.72f;
        var torso = Smooth(48 - hw * 0.8f, 46, 48 + hw * 0.8f, 46, 48 + hw, 62, 48 + hw + 1, 77, 48 - hw - 1, 77, 48 - hw, 62);
        SF(g, torso, st.robe);
        ClipFill(g, torso, Ell(48 - hw - 3, 66, hw * 0.5f, 26), Shade(st.dark, 1f, 110));
        SF(g, RR(48 - hw - 0.5f, 60, hw * 2 + 1, 4.5f, 1.5f), st.trim);
        if (st.name == "Hero_LiuBei")
        {
            Limb(g, st.accent, 3.4f, 42, 49, 54, 72);
        }

        // 가까운 팔
        float near = -sw * 5f;
        Limb(g, st.robe, 7, 48 + near * 0.4f, 51, 48 + near, 65);
        SDot(g, st.skin, 48 + near * 1.1f, 67.5f, 3.3f, 3.3f);

        HeadSide(g, st);
        HatSide(g, st);
        g.Restore(s);
    }

    static void HeadSide(Graphics g, St st)
    {
        var head = Ell(50, 30, 19, 19);
        SF(g, head, st.skin);
        ClipFill(g, head, Ell(40, 34, 10, 22), Shade(st.skin, 0.8f, 70));
        SF(g, Ell(68.5f, 36, 2.8f, 2.8f), st.skin);              // 코
        SDot(g, Color.FromArgb(60, 230, 90, 80), 60, 40, 4.5f, 3f);
        SDot(g, Ink, 58, 33.5f, 2.2f, 3.2f);
        SDot(g, Color.White, 58.7f, 32.3f, 0.9f, 0.9f);
        Line(g, Hex("#7A2E2E"), 1.5f, 61, 43, 65, 43);
        SF(g, Ell(46, 34, 3.4f, 4.6f), st.skin);                 // 귀
    }

    static GraphicsPath HairSide()
    {
        return Smooth(36, 40, 30, 28, 33, 16, 42, 9, 55, 8, 65, 13, 68, 22, 58, 19, 50, 22, 46, 29, 46, 38);
    }

    static void HatSide(Graphics g, St st)
    {
        switch (st.name)
        {
            case "Hero_LiuBei":
                SF(g, HairSide(), st.hair);
                SDot(g, st.hair, 40, 8, 5.5f, 5f);
                SF(g, RR(36, 11, 9, 5, 2), Gold);
                Line(g, st.hair, 2f, 64, 40, 60, 42, 57, 42.5f);
                SF(g, Ell(61, 48, 2.6f, 3.6f), st.hair);
                break;
            case "Hero_GuanYu":
                SF(g, Smooth(46, 38, 43, 48, 47, 64, 57, 76, 66, 66, 70, 50, 67, 40, 61, 44, 54, 41), st.hair);
                Line(g, Hex("#5A4C44"), 1.2f, 52, 48, 52, 62, 56, 72);
                Line(g, Hex("#5A4C44"), 1.2f, 60, 48, 62, 60, 62, 68);
                Line(g, st.hair, 2.4f, 64, 41, 69, 44, 71, 48);
                SF(g, Smooth(32, 36, 31, 19, 40, 10, 54, 8, 65, 12, 68, 22, 58, 20, 50, 22, 46, 28, 46, 36), st.accent);
                SDot(g, st.accent, 42, 7, 5, 4.6f);
                Line(g, Gold, 2.2f, 34, 21, 50, 21, 67, 22);
                break;
            case "Hero_ZhangFei":
                SF(g, Poly(34, 40, 30, 28, 32, 14, 34, 4, 40, 10, 45, 1, 51, 9, 58, 3, 63, 11, 68, 18, 67, 23, 58, 20, 50, 23, 46, 30, 46, 38), st.hair);
                Line(g, st.accent, 3.6f, 33, 23, 50, 21, 67, 23);
                Line(g, st.accent, 3, 36, 24, 28, 32, 26, 44);
                SF(g, Poly(46, 38, 42, 46, 48, 54, 54, 52, 58, 57, 63, 52, 68, 55, 69, 47, 67, 41, 60, 44, 54, 41), st.hair);
                Line(g, Hex("#5A1414"), 1.8f, 61, 44, 65, 44);
                break;
            case "Hero_CaoCao":
                SF(g, HairSide(), st.hair);
                SF(g, RR(36, 3, 20, 14, 2), st.hair);
                SF(g, RR(33, 1, 26, 4, 1.5f), Gold);
                Line(g, Gold, 2.2f, 36, 14, 56, 14);
                Line(g, st.hair, 1.8f, 64, 40, 59, 41.5f, 55, 44);
                SF(g, Poly(62, 44, 68, 44, 66, 53), st.hair);
                break;
            default:
                SF(g, Smooth(32, 36, 31, 19, 40, 10, 54, 8, 65, 12, 68, 22, 58, 20, 50, 22, 46, 28, 46, 36), Gold);
                Line(g, st.accent, 3f, 34, 21, 50, 21, 67, 22);
                SDot(g, Hex("#C0392B"), 56, 14, 2f, 2f);
                SF(g, Ell(46, 33, 3.2f, 6f), Gold);
                Leaf(g, Hex("#2E8C7A"), 8, 46, 12, 34, 5, 24, 4);
                Leaf(g, Hex("#2E8C7A"), 7, 50, 11, 40, 2, 31, 0);
                Line(g, st.hair, 1.8f, 64, 40.5f, 60, 42, 57, 44);
                break;
        }
    }

    // ───────────────────────── 시트 ─────────────────────────

    static void DrawCell(Graphics g, St st, int dir, int frame)
    {
        if (dir == DOWN) DrawDown(g, st, frame);
        else if (dir == UP) DrawUp(g, st, frame);
        else if (dir == RIGHT) DrawRight(g, st, frame);
        else
        {
            // 왼쪽: 오른쪽 그림을 좌우 반전
            var s = g.Save();
            g.TranslateTransform(CELL, 0);
            g.ScaleTransform(-1, 1);
            DrawRight(g, st, frame);
            g.Restore(s);
        }
    }

    public static Bitmap RenderWalkSheet(string heroName)
    {
        var st = StyleOf(heroName);
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
                    DrawCell(g, st, row, col);
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

    public static void GenerateWalkSheets(string outDir, string previewPath)
    {
        string[] names = { "Hero_LiuBei", "Hero_GuanYu", "Hero_ZhangFei", "Hero_CaoCao", "Hero_LvBu" };
        Directory.CreateDirectory(outDir);
        // 미리보기: 장수마다 한 줄(방향 4개 x 프레임 0), 그 아래 줄에 걷기 프레임 4장(아래 방향)을 확대
        float zoom = 1.6f;
        int cw = (int)(CELL * zoom);
        var prev = new Bitmap(cw * 8 + 20, cw * names.Length + 20, PixelFormat.Format32bppArgb);
        using (var pg = Graphics.FromImage(prev))
        {
            pg.Clear(Color.FromArgb(255, 70, 100, 70));
            pg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < names.Length; i++)
            {
                using (var sheet = RenderWalkSheet(names[i]))
                {
                    sheet.Save(Path.Combine(outDir, names[i] + "_Walk.png"), ImageFormat.Png);
                    for (int d = 0; d < 4; d++)
                        pg.DrawImage(sheet, new Rectangle(10 + d * cw, 10 + i * cw, cw, cw),
                            new Rectangle(0, d * CELL, CELL, CELL), GraphicsUnit.Pixel);
                    // 오른쪽 방향 걷기 프레임 0~3
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
