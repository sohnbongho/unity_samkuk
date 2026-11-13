namespace Samkuk.Player
{
    /// <summary>
    /// 레벨업에 필요한 경험치 곡선 (유니티 때는 PlayerExperience 안에 있던 상수). 밸런스 모델과 플레이어 경험치가 같이 쓴다.
    /// 60초에 약 11레벨이 기준이며 값을 바꾸면 BalanceTests 가 범위를 지킨다.
    /// </summary>
    public static class ExperienceCurve
    {
        public const int BaseRequired = 7;
        public const int RequiredStep = 6;

        /// <summary>level 에서 다음 레벨로 가는 데 필요한 경험치.</summary>
        public static int RequiredFor(int level) => BaseRequired + (level - 1) * RequiredStep;
    }
}
