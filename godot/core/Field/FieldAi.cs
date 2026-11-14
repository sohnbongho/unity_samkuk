namespace Samkuk.Field
{
    /// <summary>
    /// 적의 단순 규칙 (1단계): 보병·기병은 전진, 궁병은 사거리 안에 적이 있으면 대기·없으면 전진, 장수 부대는 상대 병사 부대가
    /// 절반 이하로 줄면 전진, 병사 수가 처음의 20% 밑이면 후퇴(장수 부대는 후퇴하지 않음). 판단은 <see cref="FieldRules.aiInterval"/> 마다
    /// <see cref="FieldBattleModel.Tick"/> 안에서 하므로 정지 중에는 생각도 멈춘다. 어느 편이든 돌릴 수 있어 테스트에서 내 편에도 쓸 수 있다.
    /// </summary>
    public static class FieldAi
    {
        public static void Decide(FieldBattleModel model, FieldSide side)
        {
            var rules = model.Rules;
            var other = side == FieldSide.Player ? FieldSide.Enemy : FieldSide.Player;
            int otherSoldierUnits = model.CountAlive(other, soldierUnitsOnly: true);
            int otherInitial = model.InitialSoldierUnits(other);
            bool otherHalved = otherInitial == 0 || otherSoldierUnits * 2 <= otherInitial;

            var units = model.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Alive || u.Side != side) continue;

                if (!u.IsGeneral && u.Soldiers < u.MaxSoldiers * rules.aiRetreatRatio)
                {
                    model.ForceOrder(u, UnitOrder.Retreat, -1);
                    continue;
                }

                if (u.IsGeneral)
                {
                    model.ForceOrder(u, otherHalved ? UnitOrder.Advance : UnitOrder.Hold, -1);
                    continue;
                }

                switch (u.Class)
                {
                    case UnitClass.Archer:
                        bool inRange = model.NearestEnemy(u, rules.Stats(UnitClass.Archer).range) != null;
                        model.ForceOrder(u, inRange ? UnitOrder.Hold : UnitOrder.Advance, -1);
                        break;
                    default:
                        model.ForceOrder(u, UnitOrder.Advance, -1);
                        break;
                }
            }
        }
    }
}
