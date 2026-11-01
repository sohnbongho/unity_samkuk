namespace Samkuk.Field
{
    /// <summary>야전 한 판의 시작 조건 (준비 화면이 채운다). 병과별 부대 수는 보병·궁병·기병 순서로 0~3.</summary>
    public sealed class FieldSetup
    {
        public const int MaxPerClass = 3;

        public int[] playerCounts = { 2, 2, 2 };
        public int[] enemyCounts = { 2, 2, 2 };
        /// <summary>부대 하나의 병사 수 (= 체력).</summary>
        public int soldiers = 300;
        public FormationKind playerFormation = FormationKind.BoGiGung;
        public FormationKind enemyFormation = FormationKind.BoGiGung;
        public bool playerMirror, enemyMirror;
        /// <summary>끄면 적은 모두 대기만 한다 (내 조작만 시험할 때).</summary>
        public bool enemyAi = true;
        public string playerGeneralName = "주인공";
        public string enemyGeneralName = "적장";

        public int Count(FieldSide side, UnitClass c)
        {
            var arr = side == FieldSide.Player ? playerCounts : enemyCounts;
            int v = arr != null && (int)c < arr.Length ? arr[(int)c] : 0;
            return v < 0 ? 0 : (v > MaxPerClass ? MaxPerClass : v);
        }
    }
}
