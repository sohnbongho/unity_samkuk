namespace Samkuk.Field
{
    /// <summary>병과: 부대의 종류 (용어집 "야전"). 장수 부대는 병과가 늘 기병이다.</summary>
    public enum UnitClass { Infantry, Archer, Cavalry }

    /// <summary>명령: 전진·대기·후퇴. 바꾸기 전까지 유지된다.</summary>
    public enum UnitOrder { Advance, Hold, Retreat }

    /// <summary>어느 편인가. 내 편은 격자 왼쪽(열 0~2)에서, 적은 오른쪽(열 9~11)에서 시작한다.</summary>
    public enum FieldSide { Player, Enemy }

    /// <summary>배치: 시작 전에 고르는 미리 짜인 자리 (장기의 마상마상/상마상마처럼). 이름은 앞줄·가운데·뒷줄의 병과 순서.</summary>
    public enum FormationKind { BoGiGung, GiBoGung, GungBoGi, Wings }

    /// <summary>야전의 결말. None 이면 아직 진행 중.</summary>
    public enum BattleOutcome { None, Victory, Defeat }

    /// <summary>화면에 보이는 이름들 (한 곳에서 관리).</summary>
    public static class FieldNames
    {
        public static string Class(UnitClass c)
        {
            switch (c)
            {
                case UnitClass.Infantry: return "보병";
                case UnitClass.Archer: return "궁병";
                default: return "기병";
            }
        }

        public static string Order(UnitOrder o)
        {
            switch (o)
            {
                case UnitOrder.Advance: return "전진";
                case UnitOrder.Hold: return "대기";
                default: return "후퇴";
            }
        }

        public static string Formation(FormationKind k)
        {
            switch (k)
            {
                case FormationKind.BoGiGung: return "보기궁";
                case FormationKind.GiBoGung: return "기보궁";
                case FormationKind.GungBoGi: return "궁보기";
                default: return "날개";
            }
        }
    }
}
