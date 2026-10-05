using System;
using Samkuk.Data;

namespace Samkuk.Strategy
{
    /// <summary>전략 지도 화면이 다시 로드되어도 유지되는 정보 (마지막으로 보던 성).</summary>
    public static class StrategySession
    {
        public static string LastCastleId { get; set; }
    }

    /// <summary>
    /// 내정 화면의 상태: 지도에서 성을 고르고, 성 안으로 들어가고, 인접한 성으로 옮겨 가는 규칙.
    /// UI와 분리해 두어 화면 없이 테스트할 수 있다. (내정 수치와 명령은 이후 단계에서 여기에 붙는다.)
    /// </summary>
    public class StrategyModel
    {
        readonly CastleCatalog catalog;

        public StrategyModel(CastleCatalog catalog)
        {
            this.catalog = catalog;
            // 타이틀로 나갔다 돌아와도 보던 성을 기억한다 (성 안이 아니라 지도에서 선택된 상태로 시작)
            if (catalog != null && !string.IsNullOrEmpty(StrategySession.LastCastleId))
                Selected = catalog.Find(StrategySession.LastCastleId);
        }

        public CastleCatalog Catalog => catalog;
        public CastleData Selected { get; private set; }
        public bool InCastle { get; private set; }

        /// <summary>선택/화면 상태가 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>지도에서 성을 고른다. 카탈로그에 없는 성이면 무시한다.</summary>
        public bool Select(CastleData castle)
        {
            if (castle == null || catalog == null || !catalog.castles.Contains(castle)) return false;
            Selected = castle;
            StrategySession.LastCastleId = castle.id;
            Changed?.Invoke();
            return true;
        }

        /// <summary>고른 성의 안(성 화면)으로 들어간다. 고른 성이 없으면 아무 일도 없다.</summary>
        public bool Enter()
        {
            if (Selected == null || InCastle) return false;
            InCastle = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>성 화면에서 지도로 돌아온다.</summary>
        public bool Leave()
        {
            if (!InCastle) return false;
            InCastle = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>성 화면에서 인접한 성으로 옮겨 간다. 인접하지 않으면 무시한다.</summary>
        public bool MoveTo(CastleData neighbor)
        {
            if (!InCastle || Selected == null || !Selected.IsAdjacent(neighbor)) return false;
            return Select(neighbor);
        }
    }
}
