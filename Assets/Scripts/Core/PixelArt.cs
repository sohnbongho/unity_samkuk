namespace Samkuk.Core
{
    /// <summary>
    /// 도트(픽셀 아트) 규격 한 곳 (HD-2D Step 14-3, ADR 0002). 1유닛 = 32픽셀, 가상 해상도 640x360.
    /// 지원 해상도(1080p, 1440p)에서 정수 배(3배, 4배)가 되어 스프라이트가 흔들리지 않는다.
    /// 그림 생성기(tools/hero_art, tools/terrain_art)와 임포터, Pixel Perfect Camera 가 모두 이 값을 따른다. 게임 수치(유닛)는 바꾸지 않는다.
    /// </summary>
    public static class PixelArt
    {
        /// <summary>월드 스프라이트의 픽셀/유닛.</summary>
        public const int PPU = 32;
        /// <summary>가상 해상도 (Pixel Perfect Camera 기준 해상도).</summary>
        public const int RefWidth = 640, RefHeight = 360;
        /// <summary>걷기 시트 한 칸 (4x4 칸 시트 = 192x192). 생성기의 PCELL 과 같아야 한다.</summary>
        public const int WalkCell = 48;
        /// <summary>예전(도트 전) 걷기 시트 칸. 이 크기의 시트는 예전 PPU 값을 그대로 쓴다.</summary>
        public const int LegacyWalkCell = 96;
        /// <summary>지형 바닥 타일 한 변 (= 4유닛).</summary>
        public const int GroundTile = 128;
        /// <summary>지형 소품 그림에서 바닥에 닿는 점이 아래 가장자리에서 떨어진 픽셀 수 (생성기가 이만큼 띄워 그린다).</summary>
        public const int PropFootPixels = 3;
        /// <summary>Pixel Perfect Camera 를 끈 상태의 전투 카메라 크기 (Step 1 셋업 값).</summary>
        public const float DefaultOrthographicSize = 6f;

        /// <summary>Pixel Perfect Camera 가 켜졌을 때의 카메라 반높이(유닛) = 360 / 2 / 32.</summary>
        public static float OrthographicSize => RefHeight / 2f / PPU;

        /// <summary>화면 세로 픽셀 수에서 정수 확대 배율 (1080 → 3, 1440 → 4). 나누어떨어지지 않으면 내림.</summary>
        public static int IntegerZoom(int screenHeight) => screenHeight / RefHeight;

        /// <summary>걷기 시트가 도트 규격(칸 48)인가. 시트 폭으로 판단한다.</summary>
        public static bool IsPixelWalkSheet(int sheetWidth) => sheetWidth == WalkCell * 4;
    }
}
