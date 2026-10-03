using Samkuk.Data;

namespace Samkuk.Core
{
    /// <summary>씬이 다시 로드되어도 유지되는 세션 정보 (마지막으로 고른 장수 등).</summary>
    public static class GameSession
    {
        public static HeroData SelectedHero { get; set; }
    }
}
