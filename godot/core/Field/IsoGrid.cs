using System;
using Samkuk.Core;

namespace Samkuk.Field
{
    /// <summary>부대 그림이 바라보는 아이소메트릭 방향 (열이 늘면 오른쪽 위, 행이 늘면 왼쪽 위). 그림은 왼쪽/오른쪽 둘만 구분한다(15-8).</summary>
    public enum IsoFacing { DownRight, DownLeft, UpRight, UpLeft }

    /// <summary>
    /// 야전 격자(12x9)와 아이소메트릭 월드 좌표의 변환 (ADR 0003). 마름모 타일 48x24 픽셀 = 1.5 x 0.75 유닛(PPU 32).
    /// 열(col)이 늘면 화면 오른쪽 위로, 행(row)이 늘면 왼쪽 위로 간다: 내 편(열 0~2)은 왼쪽 아래, 적(열 9~11)은 오른쪽 위에 선다.
    /// 전장 가운데 칸이 원점이라 전장 외곽은 가로 15.75 x 세로 7.875 유닛(504x252 픽셀)으로 640x360 안에 든다.
    /// 유니티 타입을 쓰지 않아 유니티 밖에서도 테스트한다 (화면 쪽이 Vector3 로 감싼다).
    /// 월드 y 가 클수록 멀리 있는 칸이므로 기존 y 축 정렬(World 정렬 레이어, 축 (0,1,0))이 그대로 앞뒤를 맞춘다.
    /// </summary>
    public static class IsoGrid
    {
        public const int TileWidthPx = 48, TileHeightPx = 24;
        public const float TileWidth = (float)TileWidthPx / PixelArt.PPU;    // 1.5
        public const float TileHeight = (float)TileHeightPx / PixelArt.PPU;  // 0.75

        /// <summary>격자 가운데 칸 (원점이 되는 자리). 짝수 칸이면 .5 가 된다.</summary>
        public const float CenterCol = (FieldRules.Cols - 1) * 0.5f, CenterRow = (FieldRules.Rows - 1) * 0.5f;

        /// <summary>전장 외곽 크기(유닛): 마름모를 다 담는 사각형.</summary>
        public const float FieldWidth = (FieldRules.Cols + FieldRules.Rows) * 0.5f * TileWidth;
        public const float FieldHeight = (FieldRules.Cols + FieldRules.Rows) * 0.5f * TileHeight;

        /// <summary>칸(소수 가능: 이동 보간)의 가운데 월드 좌표.</summary>
        public static void CellToWorld(float col, float row, out float x, out float y)
        {
            float c = col - CenterCol, r = row - CenterRow;
            x = (c - r) * TileWidth * 0.5f;
            y = (c + r) * TileHeight * 0.5f;
        }

        /// <summary>월드 좌표가 든 칸 (가장 가까운 칸 가운데 = 마름모 안). 격자 밖이면 false 지만 col/row 는 채운다.</summary>
        public static bool WorldToCell(float x, float y, out int col, out int row)
        {
            float a = x / (TileWidth * 0.5f);   // c - r
            float b = y / (TileHeight * 0.5f);  // c + r
            float c = (a + b) * 0.5f + CenterCol;
            float r = (b - a) * 0.5f + CenterRow;
            col = (int)Math.Round(c);
            row = (int)Math.Round(r);
            return FieldBattleModel.InBounds(col, row);
        }

        /// <summary>이동 방향(-1/0/1)에서 그림 방향. 열이 느는 쪽이 오른쪽 위, 행이 느는 쪽이 왼쪽 위. 안 움직이면 fallback.</summary>
        public static IsoFacing FacingFor(int dx, int dy, IsoFacing fallback)
        {
            if (dx > 0) return IsoFacing.UpRight;
            if (dx < 0) return IsoFacing.DownLeft;
            if (dy > 0) return IsoFacing.UpLeft;
            if (dy < 0) return IsoFacing.DownRight;
            return fallback;
        }

        /// <summary>편의 기본 방향 (움직인 적이 없을 때): 내 편은 적이 있는 오른쪽 위를, 적은 왼쪽 아래를 본다.</summary>
        public static IsoFacing DefaultFacing(FieldSide side) => side == FieldSide.Player ? IsoFacing.UpRight : IsoFacing.DownLeft;

        /// <summary>그 방향이 왼쪽을 보는가 (왼쪽을 보는 둘). 걷기 시트의 왼쪽 줄을 쓴다 (15-8: 위/아래 줄은 안 쓴다).</summary>
        public static bool FacesLeft(IsoFacing f) => f == IsoFacing.DownLeft || f == IsoFacing.UpLeft;
    }
}
