using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 야전(Step 15-8) 부대 걷기 시트. 규격은 장수/적 시트와 똑같다 (4열 프레임 x 4행 방향, 도트 칸 48x48, PPU 32, 발 피벗 8픽셀).
// 야전에서 장수 부대는 서바이버 전투의 장수 시트를 그대로 쓰므로, 병사 부대도 같은 몸·같은 그리기(적 시트의 병과 그림)로 맞춘다.
// 편 색만 다르다: 내 편 파랑 갑옷, 적 빨강 갑옷. 기병·장수 부대는 말 + 기수(EDrawCav*).
// 파일 이름은 Assets/Scripts/Field/FieldArtSpec.UnitFile 과 같다: Unit_<Infantry|Archer|Cavalry|General>_<Player|Enemy>.png
// Windows PowerShell 5.1 = C# 5 문법만 사용.
public static partial class HeroArt
{
    public static readonly string[] FieldUnitNames =
    {
        "Unit_Infantry_Player", "Unit_Infantry_Enemy",
        "Unit_Archer_Player", "Unit_Archer_Enemy",
        "Unit_Cavalry_Player", "Unit_Cavalry_Enemy",
        "Unit_General_Player", "Unit_General_Enemy"
    };

    /// <summary>칸 안에서 몸이 차지하는 픽셀: 병사는 장수(40)보다 조금 작게, 기병은 말이 있어 조금 크게, 장수 부대는 장수와 같게.</summary>
    static int FieldBodyPx(string name)
    {
        if (name.Contains("General")) return BODY_PX;
        if (name.Contains("Cavalry")) return 38;
        return 36;
    }

    static St FieldStyleOf(string name)
    {
        bool player = name.EndsWith("_Player");
        var s = new St();
        s.name = name;
        s.skin = Hex("#E9C49C");
        s.hair = Hex("#2B2420");
        s.boots = Hex("#3A2A22");
        s.trim = Steel;
        s.plume = Color.FromArgb(0, 0, 0, 0);
        s.maskC = Hex("#8E3B2B");
        // 편 색: 갑옷(robe)·그늘·바지
        if (player) { s.robe = Hex("#3B63C8"); s.dark = Hex("#233E8A"); s.pants = Hex("#26305E"); }
        else { s.robe = Hex("#C23A3A"); s.dark = Hex("#7A2020"); s.pants = Hex("#4E1E1E"); }

        if (name.Contains("Infantry"))
        {
            s.bw = 17f;
            s.hat = "helmet"; s.hatC = Hex("#9AA3B5"); s.hatDark = Hex("#6A7388"); s.prop = "spear";
        }
        else if (name.Contains("Archer"))
        {
            s.bw = 15f;
            s.hat = "scarf"; s.hatC = player ? Hex("#6F8FE0") : Hex("#E07A6A"); s.hatDark = player ? Hex("#3E5AA8") : Hex("#A8463A"); s.prop = "bow";
        }
        else if (name.Contains("Cavalry"))
        {
            s.bw = 16f;
            s.hat = "fur"; s.hatC = Hex("#6B4A32"); s.hatDark = Hex("#4A3220"); s.prop = "spear";
            s.cav = true; s.horse = Hex("#8A5A32"); s.horseDark = Hex("#5E3A1E");
        }
        else // General: 금빛 투구 + 깃털, 흰 말(내 편) / 검붉은 말(적), 극
        {
            s.bw = 17f; s.trim = Hex("#F2C14E");
            s.hat = "gold"; s.hatC = Hex("#F2C14E"); s.hatDark = Hex("#A07818"); s.plume = player ? Hex("#2E8C7A") : Hex("#F0D04A"); s.feathers = 1;
            s.prop = "halberd"; s.beard = !player;
            s.cav = true;
            if (player) { s.horse = Hex("#E2DCD2"); s.horseDark = Hex("#9E968C"); }
            else { s.horse = Hex("#5E2A22"); s.horseDark = Hex("#3A1812"); }
        }
        return s;
    }

    public static Bitmap RenderFieldSheet(string name)
    {
        var st = FieldStyleOf(name);
        return RenderPixelSheet(delegate(Graphics g, int dir, int frame) { DrawEnemyCell(g, st, dir, frame); }, FieldBodyPx(name));
    }

    /// <summary>시트 8장을 outDir 에 쓰고, 미리보기(방향 4 + 오른쪽 걷기 4 프레임, 3배)를 previewPath 에 남긴다.</summary>
    public static void GenerateFieldSheets(string outDir, string previewPath)
    {
        Directory.CreateDirectory(outDir);
        int zoom = 3;
        int cw = PCELL * zoom;
        var prev = new Bitmap(cw * 8 + 20, cw * FieldUnitNames.Length + 20, PixelFormat.Format32bppArgb);
        using (var pg = Graphics.FromImage(prev))
        {
            pg.Clear(Color.FromArgb(255, 62, 86, 54));
            for (int i = 0; i < FieldUnitNames.Length; i++)
            {
                using (var sheet = RenderFieldSheet(FieldUnitNames[i]))
                {
                    sheet.Save(Path.Combine(outDir, FieldUnitNames[i] + ".png"), ImageFormat.Png);
                    for (int d = 0; d < 4; d++)
                        PixelTools.DrawCrisp(pg, sheet, new Rectangle(0, d * PCELL, PCELL, PCELL), new Rectangle(10 + d * cw, 10 + i * cw, cw, cw));
                    for (int f = 0; f < 4; f++)
                        PixelTools.DrawCrisp(pg, sheet, new Rectangle(f * PCELL, 3 * PCELL, PCELL, PCELL), new Rectangle(10 + (4 + f) * cw, 10 + i * cw, cw, cw));
                }
            }
        }
        prev.Save(previewPath, ImageFormat.Png);
        prev.Dispose();
    }
}
