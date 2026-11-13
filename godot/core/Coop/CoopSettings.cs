namespace Samkuk.Coop
{
    /// <summary>협동 모드(친구 2~4인, 서버 권위 시뮬레이션)의 상수. 서버 설계는 csharpServer/Docs/coop-design.md.</summary>
    public static class CoopSettings
    {
        /// <summary>방 정원. 서버와 같은 값.</summary>
        public const int MaxMembers = 4;
        /// <summary>초대 코드 길이(영문 대문자/숫자).</summary>
        public const int CodeLength = 6;
        /// <summary>방장이 방을 만들면 서버에 정해 주는 스테이지(에셋 파일명). 스테이지 선택 UI 는 아직 없다.</summary>
        public const string DefaultStageId = "Stage_YellowTurban";
    }
}
