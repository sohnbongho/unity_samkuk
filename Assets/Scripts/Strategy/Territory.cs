using Samkuk.Meta;

namespace Samkuk.Strategy
{
    /// <summary>
    /// 내가 차지한 성(영토)의 저장 규칙. 저장 데이터(<see cref="SaveData.ownedCastleIds"/>)만 다루는 순수 함수라
    /// 전투 결과 정산(ResultController)과 내정 화면(StrategyModel)이 같은 규칙을 쓴다.
    /// </summary>
    public static class Territory
    {
        public static bool IsOwned(SaveData save, string castleId) =>
            !string.IsNullOrEmpty(castleId) && save.ownedCastleIds.Contains(castleId);

        public static int Count(SaveData save) => save.ownedCastleIds.Count;

        /// <summary>성을 영토에 넣는다. 새로 차지했으면 true, 이미 내 성이거나 아이디가 비었으면 false.</summary>
        public static bool Conquer(SaveData save, string castleId)
        {
            if (string.IsNullOrEmpty(castleId) || save.ownedCastleIds.Contains(castleId)) return false;
            save.ownedCastleIds.Add(castleId);
            return true;
        }

        /// <summary>성을 영토에서 뺀다 (시작 성을 다시 고를 때). 있었으면 true.</summary>
        public static bool Release(SaveData save, string castleId) => save.ownedCastleIds.Remove(castleId);
    }
}
