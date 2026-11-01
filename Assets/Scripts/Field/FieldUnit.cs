namespace Samkuk.Field
{
    /// <summary>
    /// 부대 하나: 병과·병사 수·칸·명령·목표. 칸 사이를 움직이는 동안은 출발 칸(<see cref="Col"/>)과 도착 칸(<see cref="NextCol"/>)을
    /// 둘 다 차지하고, 그림은 <see cref="VisualCol"/> 로 보간한 자리에 그린다. 규칙은 <see cref="FieldBattleModel"/> 이 돌린다.
    /// </summary>
    public sealed class FieldUnit
    {
        public int Id { get; internal set; }
        public FieldSide Side { get; internal set; }
        public UnitClass Class { get; internal set; }
        /// <summary>장수 부대인가. 무너지면 그 편이 패배하고, 병과는 늘 기병이다.</summary>
        public bool IsGeneral { get; internal set; }
        public string Name { get; internal set; }

        public float Soldiers { get; internal set; }
        public int MaxSoldiers { get; internal set; }

        /// <summary>서 있는 칸 (이동 중이면 출발 칸).</summary>
        public int Col { get; internal set; }
        public int Row { get; internal set; }
        /// <summary>이동 중일 때 도착 칸.</summary>
        public int NextCol { get; internal set; }
        public int NextRow { get; internal set; }
        public bool Moving { get; internal set; }
        /// <summary>출발 칸에서 도착 칸까지 0~1.</summary>
        public float MoveProgress { get; internal set; }
        /// <summary>마지막으로 떠난 칸 (막혔을 때 바로 되돌아가 왕복하는 것을 막기 위해).</summary>
        internal int LastCol = -1, LastRow = -1;

        public UnitOrder Order { get; internal set; } = UnitOrder.Hold;
        /// <summary>전진 명령의 목표 부대 아이디 (-1 이면 없음 = 가장 가까운 적).</summary>
        public int TargetId { get; internal set; } = -1;

        public bool Routed { get; internal set; }
        public bool Exited { get; internal set; }
        public bool Alive => !Routed && !Exited;

        /// <summary>돌격이 장전됐나: 기병이 칸을 하나 옮긴 뒤 처음 백병전 피해를 줄 때 추가 피해가 붙고 풀린다.</summary>
        public bool ChargeArmed { get; internal set; }

        /// <summary>마지막 이동 방향 (-1/0/1). 그림의 방향에 쓴다.</summary>
        public int FacingDx { get; internal set; }
        public int FacingDy { get; internal set; }

        public float VisualCol => Moving ? Col + (NextCol - Col) * MoveProgress : Col;
        public float VisualRow => Moving ? Row + (NextRow - Row) * MoveProgress : Row;
        public float SoldierRatio => MaxSoldiers > 0 ? Soldiers / MaxSoldiers : 0f;
    }
}
