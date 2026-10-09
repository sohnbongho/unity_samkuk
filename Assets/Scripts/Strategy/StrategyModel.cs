using System;
using System.Collections.Generic;
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
    /// 내 영토의 성에서 이웃한 적 성으로 출진하는 규칙. 승리해서 성을 차지하는 것은 전투 결과 정산(ResultController)이
    /// <see cref="Territory"/> 로 저장하고, 이 모델은 다음에 열릴 때 저장 데이터에서 읽는다.
    /// UI와 분리해 두어 화면 없이 테스트할 수 있다. (내정 수치와 명령은 이후 단계에서 여기에 붙는다.)
    /// </summary>
    public class StrategyModel
    {
        readonly CastleCatalog catalog;

        public StrategyModel(CastleCatalog catalog)
        {
            this.catalog = catalog;
            if (catalog == null) return;

            // 저장된 내 성. 타이틀에 다녀와도 유지된다.
            var save = SaveSystem.Current;
            if (!string.IsNullOrEmpty(save.homeCastleId)) Home = catalog.Find(save.homeCastleId);

            // 영토 기능 이전의 저장(내 성만 있고 영토 목록이 없는 경우)도 내 성은 영토로 친다
            if (Home != null && Territory.Conquer(save, Home.id)) SaveSystem.SaveCurrent();

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

        // ───────────────────────── 영토 ─────────────────────────

        /// <summary>내가 차지한 성인가 (시작 성 포함).</summary>
        public bool IsOwned(CastleData castle) => castle != null && Territory.IsOwned(SaveSystem.Current, castle.id);

        public int OwnedCount => Territory.Count(SaveSystem.Current);
        public int TotalCount => catalog != null ? catalog.castles.Count : 0;

        /// <summary>모든 성을 차지했는가 (천하통일).</summary>
        public bool IsUnified => TotalCount > 0 && OwnedCount >= TotalCount;

        /// <summary>내 성이 아니면서 내 영토의 성과 이웃한 성: 공격할 수 있는 성.</summary>
        public bool IsAttackable(CastleData castle)
        {
            if (castle == null || IsOwned(castle)) return false;
            foreach (var n in castle.neighbors)
                if (IsOwned(n)) return true;
            return false;
        }

        /// <summary>선택한 성에서 출진해 공격할 수 있는 성들 (이웃한 적 성). 내 성이 아니면 비어 있다.</summary>
        public List<CastleData> AttackTargets()
        {
            var list = new List<CastleData>();
            if (Selected == null || !IsOwned(Selected)) return list;
            foreach (var n in Selected.neighbors)
                if (n != null && !IsOwned(n)) list.Add(n);
            return list;
        }

        /// <summary>지금 성 화면에서 출진을 시작할 수 있는가: 내 영토의 성 안에 있을 때.</summary>
        public bool CanSortie => InCastle && Selected != null && IsOwned(Selected);

        /// <summary>선택/화면 상태가 바뀔 때마다.</summary>
        public event Action Changed;

        /// <summary>출진이 시작될 때 (UI가 전투 씬으로 넘어간다). 인자는 공격 대상 성.</summary>
        public event Action<CastleData> SortieStarted;

        // ───────────────────────── 보기 / 이동 ─────────────────────────

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

        // ───────────────────────── 시작 성 ─────────────────────────

        /// <summary>고른 성을 내 성으로 정하고 그 성 안으로 들어간다 (내정의 시작). 이미 내 성이 있거나 고른 성이 없으면 무시한다.</summary>
        public bool StartHere()
        {
            if (HasHome || Selected == null) return false;
            Home = Selected;
            var save = SaveSystem.Current;
            save.homeCastleId = Home.id;
            Territory.Conquer(save, Home.id);
            SaveSystem.SaveCurrent();
            InCastle = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>시작 성을 취소하고 다시 고르는 상태로 돌아간다. 이미 다른 성을 차지했다면 영토가 사라지므로 허용하지 않는다.</summary>
        public bool ClearHome()
        {
            if (!HasHome || OwnedCount > 1) return false;
            var save = SaveSystem.Current;
            Territory.Release(save, Home.id);
            save.homeCastleId = "";
            SaveSystem.SaveCurrent();
            Home = null;
            InCastle = false;
            Changed?.Invoke();
            return true;
        }

        // ───────────────────────── 출진 ─────────────────────────

        /// <summary>선택한 성(내 영토)에서 이웃한 적 성 <paramref name="target"/> 을 공격하러 전투로 출진한다.</summary>
        public bool Sortie(CastleData target)
        {
            if (!CanSortie || target == null || !Selected.IsAdjacent(target) || IsOwned(target)) return false;
            GameSession.SortieOrigin = Selected;
            GameSession.SortieCastle = target;
            GameSession.MapTest = false;
            SortieStarted?.Invoke(target);
            return true;
        }
    }
}
