namespace Samkuk.Field
{
    /// <summary>
    /// 야전 그림 규격 한 곳 (생성기 tools/field_art, 임포터, 화면이 함께 따른다). 설명은 docs/FIELD_BATTLE.md "그림 규격".
    /// - 바닥: <c>Tile_&lt;지형&gt;.png</c> 48x24 마름모(피벗 가운데), 지형 6종.
    /// - 부대: <c>Unit_&lt;병과|General&gt;_&lt;Player|Enemy&gt;.png</c> 192x48 시트 = 48x48 칸 4개 [앞모습 0, 앞모습 1, 뒷모습 0, 뒷모습 1].
    ///   앞모습은 오른쪽 아래로 오는 모습, 뒷모습은 오른쪽 위로 가는 모습이고, 왼쪽 방향은 좌우 반전으로 만든다.
    ///   부대가 서 있는 마름모 가운데는 칸 아래 가장자리에서 12픽셀 위(피벗 (0.5, 0.25)). 실행 중에 잘라 쓴다(에디터 슬라이스 없음).
    /// </summary>
    public static class FieldArtSpec
    {
        public const string Dir = "Assets/Sprites/Field";
        public const int UnitCell = 48;
        public const int SheetColumns = 4;
        public const int SheetWidth = UnitCell * SheetColumns;
        /// <summary>칸 아래 가장자리에서 부대가 선 마름모 가운데까지의 픽셀 수 (= 타일 반높이).</summary>
        public const int UnitFootPixels = IsoGrid.TileHeightPx / 2;
        public const int WalkFrames = 2;

        public static string TileFile(string terrainName) => "Tile_" + terrainName;

        public static string UnitFile(UnitClass cls, bool isGeneral, FieldSide side) =>
            "Unit_" + (isGeneral ? "General" : cls.ToString()) + "_" + side;

        /// <summary>시트에서 (뒷모습인가, 프레임) 에 해당하는 칸 번호.</summary>
        public static int SheetColumn(bool back, int frame) => (back ? WalkFrames : 0) + (frame % WalkFrames);
    }
}
