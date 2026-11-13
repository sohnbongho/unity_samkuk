using System.Collections.Generic;
using Samkuk.Data;

namespace Samkuk.Core
{
    /// <summary>씬이 다시 로드되어도 유지되는 세션 정보 (마지막으로 고른 장수와 아군 등).</summary>
    public static class GameSession
    {
        static readonly List<HeroData> allies = new List<HeroData>();

        public static HeroData SelectedHero { get; set; }

        /// <summary>내정에서 공격하러 간 성 = 전투가 벌어지는 성 (비어 있으면 타이틀의 [시작]처럼 성 없이 시작한 판). 전투 지형과 승리 시 정복 대상에 쓴다.</summary>
        public static CastleData SortieCastle { get; set; }

        /// <summary>출진한 성 (공격 대상이 아니라 떠나온 쪽). 결과 화면과 내정 복귀 위치에 쓴다.</summary>
        public static CastleData SortieOrigin { get; set; }

        /// <summary>맵 편집기에서 고친 맵을 시험해 보는 전투인가. 켜져 있으면 결과를 저장(골드, 기록, 정복)하지 않고, 전투 안에서 M 으로 편집기로 돌아간다.</summary>
        public static bool MapTest { get; set; }

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
