namespace Samkuk.Field
{
    /// <summary>
    /// 야전의 수치 한 곳: 격자 크기, 병과 표, 상성, 돌격, 적 AI 문턱 (설계 docs/FIELD_BATTLE.md "병과 표").
    /// 순수 데이터라 테스트가 값을 바꿔 쓸 수 있고, 에셋(FieldUnitClassData)으로 옮기는 것은 씬을 만드는 15-3 에서 한다.
    /// </summary>
    public sealed class FieldRules
    {
        /// <summary>격자 칸 수. 12x9 는 가상 해상도 640x360 에 48x24 마름모 타일로 전장이 다 들어오는 크기 (ADR 0003).</summary>
        public const int Cols = 12, Rows = 9;
        /// <summary>각 편의 앞줄 열. 내 편은 이 열에서 왼쪽으로, 적은 오른쪽으로 배치가 펼쳐진다.</summary>
        public const int PlayerFrontCol = 2, EnemyFrontCol = 9;

        /// <summary>병과 하나의 수치.</summary>
        public struct ClassStats
        {
            public float moveCellsPerSec;   // 이동 속도 (칸/초)
            public int range;               // 공격 사거리 (맨해튼 칸 수, 1 = 인접)
            public float damagePerSec;      // 초당 피해 (병사 수)
            public float incomingMul;       // 받는 피해 배율 (보병 0.8 = 튼튼함)
            public float adjacentMul;       // 인접한 적에게 주는 피해 배율 (궁병 0.5 = 백병전에 약함)
            public float chargeBonus;       // 돌격: 이동 뒤 처음 닿는 순간의 추가 피해 (기병)
        }

        public ClassStats infantry = new ClassStats { moveCellsPerSec = 1f, range = 1, damagePerSec = 20f, incomingMul = 0.8f, adjacentMul = 1f, chargeBonus = 0f };
        public ClassStats archer = new ClassStats { moveCellsPerSec = 1f, range = 3, damagePerSec = 14f, incomingMul = 1f, adjacentMul = 0.5f, chargeBonus = 0f };
        public ClassStats cavalry = new ClassStats { moveCellsPerSec = 2f, range = 1, damagePerSec = 28f, incomingMul = 1f, adjacentMul = 1f, chargeBonus = 60f };

        /// <summary>장수 부대가 주는 피해 배율 (기병 수치에 곱함).</summary>
        public float generalDamageMul = 1.3f;
        /// <summary>상성: 강한 쪽이 주는 피해 / 약한 쪽이 주는 피해 배율. 보병 > 궁병 > 기병(장수 제외) > 보병.</summary>
        public float strongMul = 1.5f, weakMul = 0.6f;

        /// <summary>적 AI 가 판단하는 간격(초), 후퇴 문턱(처음 병사 수 비율), 적 장수가 전진하는 문턱(내 병사 부대 남은 비율).</summary>
        public float aiInterval = 1f, aiRetreatRatio = 0.2f, aiGeneralAdvanceRatio = 0.5f;

        public static FieldRules Default() => new FieldRules();

        public ClassStats Stats(UnitClass c)
        {
            switch (c)
            {
                case UnitClass.Infantry: return infantry;
                case UnitClass.Archer: return archer;
                default: return cavalry;
            }
        }

        /// <summary>
        /// 상성 배율. 보병은 궁병에게 강하고(궁병이 보병에게 주는 피해가 줄고), 궁병은 기병에게 강하되 장수 부대에는 통하지 않으며,
        /// 기병은 보병에게 강하다.
        /// </summary>
        public float Affinity(UnitClass attacker, UnitClass victim, bool victimIsGeneral)
        {
            switch (attacker)
            {
                case UnitClass.Infantry:
                    if (victim == UnitClass.Archer) return strongMul;
                    if (victim == UnitClass.Cavalry) return weakMul;
                    break;
                case UnitClass.Archer:
                    if (victim == UnitClass.Infantry) return weakMul;
                    if (victim == UnitClass.Cavalry && !victimIsGeneral) return strongMul;
                    break;
                case UnitClass.Cavalry:
                    if (victim == UnitClass.Infantry) return strongMul;
                    if (victim == UnitClass.Archer) return weakMul;
                    break;
            }
            return 1f;
        }
    }
}
