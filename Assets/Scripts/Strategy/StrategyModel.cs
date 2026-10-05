using System;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Meta;

namespace Samkuk.Strategy
{
    /// <summary>전략 지도 화면이 다시 로드되어도 유지되는 정보 (마지막으로 보던 성).</summary>
    public static class StrategySession
    {
        public static string LastCastleId { get; set; }
    }

    /// <summary>
    /// 내정 화면의 상태: 시작 성(내 성) 고르기, 지도에서 성을 고르고, 성 안으로 들어가고, 인접한 성으로 옮겨 가고,
    /// 내 성에서 전투로 출진하는 규칙. UI와 분리해 두어 화면 없이 테스트할 수 있다.
    /// (내정 수치와 명령은 이후 단계에서 여기에 붙는다.)
    /// </summary>
    public class StrategyModel
    {
        readonly CastleCatalog catalog;

        public StrategyModel(CastleCatalog catalog)
        {
            this.catalog = catalog;
            if (catalog == null) return;

            // 저장된 내 성. 타이틀에 다녀와도 유지된다.
            string homeId = SaveSystem.Current.homeCastleId;
            if (!string.IsNullOrEmpty(homeId)) Home = catalog.Find(homeId);

            // 보던 성이 있으면 그 성, 없으면 내 성이 선택된 채로 지도에서 시작한다 (성 안이 아니라 지도)
            if (!string.IsNullOrEmpty(StrategySession.LastCastleId)) Selected = catalog.Find(StrategySession.LastCastleId);
            if (Selected == null) Selected = Home;
        }

        public CastleCatalog Catalog => catalog;
        public CastleData Selected { get; private set; }
        public bool InCastle { get; private set; }

        /// <summary>시작할 때 고른 내 성 (아직 안 골랐으면 null).</summary>
        public CastleData Home { get; private set; }
        public bool HasHome => Home != null;
        public bool IsHome(CastleData castle) => castle != null && castle == Home;

        /// <summary>지금 성 화면에서 출진할 수 있는가: 내 성 안에 있을 때만.</summary>
        public bool CanSortie => InCastle && Home != null && Selected == Home;

        /// <summary>선택/화면 상태가 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>출진이 시작될 때 (UI가 전투 씬으로 넘어간다). 인자는 출진한 성.</summary>
        public event Action<CastleData> SortieStarted;

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

        /// <summary>고른 성을 내 성으로 정하고 그 성 안으로 들어간다 (내정의 시작). 이미 내 성이 있거나 고른 성이 없으면 무시한다.</summary>
        public bool StartHere()
        {
            if (HasHome || Selected == null) return false;
            SetHome(Selected);
            InCastle = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>내 성을 취소하고 시작 성을 다시 고르는 상태로 돌아간다 (성 안에 있었다면 지도로).</summary>
        public bool ClearHome()
        {
            if (!HasHome) return false;
            SetHome(null);
            InCastle = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>내 성에서 전투(서바이버 한 판)로 출진한다. 내 성 안이 아니면 무시한다.</summary>
        public bool Sortie()
        {
            if (!CanSortie) return false;
            GameSession.SortieCastle = Home;
            SortieStarted?.Invoke(Home);
            return true;
        }

        void SetHome(CastleData castle)
        {
            Home = castle;
            SaveSystem.Current.homeCastleId = castle != null ? castle.id : "";
            SaveSystem.SaveCurrent();
        }
    }
}
