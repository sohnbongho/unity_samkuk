using Samkuk.Core;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 그림 규격 한 곳 (생성기 tools/field_art + tools/hero_art/FieldSprites.cs, 임포터, 화면이 함께 따른다). 설명은 docs/FIELD_BATTLE.md "그림 규격".
    /// - 바닥: <c>Tile_&lt;지형&gt;.png</c> 48x24 마름모(피벗 가운데), 지형 6종.
    /// - 부대: <c>Unit_&lt;병과|General&gt;_&lt;Player|Enemy&gt;.png</c> — 15-8 부터 **장수 걷기 시트와 같은 규격**(docs/HERO_WALK_SHEETS.md):
    ///   192x192 = 48x48 칸 4열(걷기 프레임 0~3) x 4행(아래, 위, 왼쪽, 오른쪽), PPU 32, 발 피벗(아래에서 <see cref="PixelArt.WalkFootPixels"/>px).
    ///   야전은 왼쪽/오른쪽 줄만 쓴다(아이소메트릭 한 걸음은 가로가 세로의 두 배). 자르기는 <c>HeroSpriteSet</c>.
    ///   내 장수 부대는 고른 장수의 시트(<c>HeroData.walkSheet</c>)를 그대로 쓴다.
    /// </summary>
    public static class FieldArtSpec
    {
        public const string Dir = "Assets/Sprites/Field";
        public const int UnitCell = PixelArt.WalkCell;
        public const int SheetColumns = 4, SheetRows = 4;
        public const int SheetWidth = UnitCell * SheetColumns, SheetHeight = UnitCell * SheetRows;
        /// <summary>칸 아래 가장자리에서 발(= 부대가 선 마름모 가운데)까지의 픽셀 수. 걷기 시트와 같다.</summary>
        public const int UnitFootPixels = PixelArt.WalkFootPixels;
        /// <summary>걷기 프레임 수 (시트 열 수).</summary>
        public const int WalkFrames = SheetColumns;

        public static string TileFile(string terrainName) => "Tile_" + terrainName;

        public static string UnitFile(UnitClass cls, bool isGeneral, FieldSide side) =>
            "Unit_" + (isGeneral ? "General" : cls.ToString()) + "_" + side;

        /// <summary>야전이 쓰는 시트 줄 번호: 왼쪽을 보면 2(왼쪽 줄), 아니면 3(오른쪽 줄). 걷기 시트의 행 순서(아래 0, 위 1, 왼쪽 2, 오른쪽 3)와 같다.</summary>
        public static int SheetRow(bool facesLeft) => facesLeft ? 2 : 3;
    }
}
