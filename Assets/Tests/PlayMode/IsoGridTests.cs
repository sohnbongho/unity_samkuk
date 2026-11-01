using NUnit.Framework;
using Samkuk.Field;

namespace Samkuk.Tests
{
    /// <summary>야전 아이소메트릭 격자(Step 15-2, `IsoGrid`): 칸↔월드 변환이 서로 맞고, 전장이 화면에 들어오고, 이동 방향이 그림 방향으로 맞게 바뀌는지.</summary>
    public class IsoGridTests
    {
        [Test]
        public void CellToWorld_RoundTrips_ForEveryCell()
        {
            for (int c = 0; c < FieldRules.Cols; c++)
                for (int r = 0; r < FieldRules.Rows; r++)
                {
                    float x, y;
                    IsoGrid.CellToWorld(c, r, out x, out y);
                    int c2, r2;
                    Assert.IsTrue(IsoGrid.WorldToCell(x, y, out c2, out r2), $"({c},{r}) 는 격자 안");
                    Assert.AreEqual(c, c2);
                    Assert.AreEqual(r, r2);
                    // 칸 가운데에서 조금 벗어나도 같은 칸 (마름모 안쪽)
                    Assert.IsTrue(IsoGrid.WorldToCell(x + IsoGrid.TileWidth * 0.2f, y, out c2, out r2) && c2 == c && r2 == r);
                    Assert.IsTrue(IsoGrid.WorldToCell(x, y + IsoGrid.TileHeight * 0.2f, out c2, out r2) && c2 == c && r2 == r);
                }
        }

        [Test]
        public void Axes_ColGoesUpRight_RowGoesUpLeft_CenteredAtOrigin()
        {
            float x0, y0, x1, y1;
            IsoGrid.CellToWorld(IsoGrid.CenterCol, IsoGrid.CenterRow, out x0, out y0);
            Assert.AreEqual(0f, x0, 1e-4f);
            Assert.AreEqual(0f, y0, 1e-4f);

            IsoGrid.CellToWorld(1, 0, out x0, out y0);
            IsoGrid.CellToWorld(2, 0, out x1, out y1);
            Assert.Greater(x1, x0, "열이 늘면 오른쪽");
            Assert.Greater(y1, y0, "열이 늘면 위(멀어짐)");
            Assert.AreEqual(IsoGrid.TileWidth * 0.5f, x1 - x0, 1e-4f);
            Assert.AreEqual(IsoGrid.TileHeight * 0.5f, y1 - y0, 1e-4f);

            IsoGrid.CellToWorld(0, 1, out x0, out y0);
            IsoGrid.CellToWorld(0, 2, out x1, out y1);
            Assert.Less(x1, x0, "행이 늘면 왼쪽");
            Assert.Greater(y1, y0, "행이 늘면 위");
        }

        [Test]
        public void Field_FitsVirtualResolution()
        {
            Assert.AreEqual(1.5f, IsoGrid.TileWidth, 1e-5f);
            Assert.AreEqual(0.75f, IsoGrid.TileHeight, 1e-5f);
            Assert.AreEqual(15.75f, IsoGrid.FieldWidth, 1e-4f);
            Assert.AreEqual(7.875f, IsoGrid.FieldHeight, 1e-4f);
            Assert.Less(IsoGrid.FieldWidth, 20f, "가상 해상도 640 = 20 유닛");
            Assert.LessOrEqual(IsoGrid.FieldHeight, 11.25f - 108f / 32f, "아래 108 픽셀 명령 띠를 남기고도 든다");

            // 모든 칸의 가운데가 외곽 안에 있고, 내 편은 왼쪽 아래·적은 오른쪽 위
            float px, py, ex, ey;
            IsoGrid.CellToWorld(FieldRules.PlayerFrontCol, IsoGrid.CenterRow, out px, out py);
            IsoGrid.CellToWorld(FieldRules.EnemyFrontCol, IsoGrid.CenterRow, out ex, out ey);
            Assert.Less(px, ex);
            Assert.Less(py, ey);
        }

        [Test]
        public void OutsideField_ReportsFalse()
        {
            int c, r;
            Assert.IsFalse(IsoGrid.WorldToCell(IsoGrid.FieldWidth, 0f, out c, out r));
            Assert.IsFalse(IsoGrid.WorldToCell(0f, IsoGrid.FieldHeight, out c, out r));
        }

        [Test]
        public void Facing_FromMovement_AndSpriteMapping()
        {
            Assert.AreEqual(IsoFacing.UpRight, IsoGrid.FacingFor(1, 0, IsoFacing.DownRight));
            Assert.AreEqual(IsoFacing.DownLeft, IsoGrid.FacingFor(-1, 0, IsoFacing.DownRight));
            Assert.AreEqual(IsoFacing.UpLeft, IsoGrid.FacingFor(0, 1, IsoFacing.DownRight));
            Assert.AreEqual(IsoFacing.DownRight, IsoGrid.FacingFor(0, -1, IsoFacing.UpRight));
            Assert.AreEqual(IsoFacing.UpLeft, IsoGrid.FacingFor(0, 0, IsoFacing.UpLeft), "안 움직이면 그대로");

            Assert.IsTrue(IsoGrid.UsesBackSprite(IsoFacing.UpRight));
            Assert.IsTrue(IsoGrid.UsesBackSprite(IsoFacing.UpLeft));
            Assert.IsFalse(IsoGrid.UsesBackSprite(IsoFacing.DownRight));
            Assert.IsFalse(IsoGrid.UsesBackSprite(IsoFacing.DownLeft));
            Assert.IsTrue(IsoGrid.FlipX(IsoFacing.DownLeft));
            Assert.IsTrue(IsoGrid.FlipX(IsoFacing.UpLeft));
            Assert.IsFalse(IsoGrid.FlipX(IsoFacing.UpRight));

            Assert.AreEqual(IsoFacing.UpRight, IsoGrid.DefaultFacing(FieldSide.Player), "내 편은 적이 있는 오른쪽 위를 본다");
            Assert.AreEqual(IsoFacing.DownLeft, IsoGrid.DefaultFacing(FieldSide.Enemy));
        }

        [Test]
        public void ArtSpec_SheetColumns_AndNames()
        {
            Assert.AreEqual(0, FieldArtSpec.SheetColumn(false, 0));
            Assert.AreEqual(1, FieldArtSpec.SheetColumn(false, 1));
            Assert.AreEqual(2, FieldArtSpec.SheetColumn(true, 0));
            Assert.AreEqual(3, FieldArtSpec.SheetColumn(true, 1));
            Assert.AreEqual(1, FieldArtSpec.SheetColumn(false, 3), "프레임은 돌려 쓴다");
            Assert.AreEqual(192, FieldArtSpec.SheetWidth);
            Assert.AreEqual(12, FieldArtSpec.UnitFootPixels);
            Assert.AreEqual("Unit_Infantry_Player", FieldArtSpec.UnitFile(UnitClass.Infantry, false, FieldSide.Player));
            Assert.AreEqual("Unit_General_Enemy", FieldArtSpec.UnitFile(UnitClass.Cavalry, true, FieldSide.Enemy));
            Assert.AreEqual("Tile_Plain", FieldArtSpec.TileFile("Plain"));
        }
    }
}
