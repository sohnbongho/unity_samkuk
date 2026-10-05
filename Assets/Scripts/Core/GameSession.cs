using System.Collections.Generic;
using Samkuk.Data;

namespace Samkuk.Core
{
    /// <summary>씬이 다시 로드되어도 유지되는 세션 정보 (마지막으로 고른 장수와 아군 등).</summary>
    public static class GameSession
    {
        static readonly List<HeroData> allies = new List<HeroData>();

        public static HeroData SelectedHero { get; set; }

        /// <summary>내정에서 출진한 성 (비어 있으면 타이틀의 [시작]처럼 성 없이 시작한 판). 전투 지형과 결과 화면의 "내정으로"에 쓴다.</summary>
        public static CastleData SortieCastle { get; set; }

        /// <summary>마지막으로 고른 아군 장수들 (다시 시작할 때 미리 선택된 상태로 보여 준다).</summary>
        public static IReadOnlyList<HeroData> SelectedAllies => allies;

        public static void SetAllies(IEnumerable<HeroData> heroes)
        {
            allies.Clear();
            if (heroes == null) return;
            foreach (var h in heroes)
                if (h != null && !allies.Contains(h)) allies.Add(h);
        }
    }
}
