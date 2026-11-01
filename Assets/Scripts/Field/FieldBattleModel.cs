using System;
using System.Collections.Generic;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전의 규칙 (화면 없이 계산하는 순수 로직이라 테스트할 수 있다). 12x9 격자 위에서 부대들이 실시간으로 움직이고 싸운다.
    ///
    /// - **정지**: <see cref="Paused"/> 가 참이면 <see cref="Tick"/> 이 아무것도 하지 않는다. 명령은 정지 중에만 바꿀 수 있다(<see cref="CanCommand"/>).
    ///   ESC 일시정지는 timeScale 을 쓰지만 정지는 모델의 깃발이라 둘이 섞이지 않는다. 시작은 전원 대기·정지 상태.
    /// - **공격은 명령과 무관**: 매 틱 살아 있는 모든 부대가 적 하나를 때린다 (백병전은 인접한 적, 궁병은 사거리 안의 적; 목표가 있으면 목표 우선).
    ///   그래서 전진하는 부대는 길에서 인접한 적을 지나치며 때리되 멈추지 않고, 대기·후퇴 중인 부대도 다가온 적을 상대한다.
    /// - **전진의 멈춤**: 목표(없으면 가장 가까운 적)에 인접하면(궁병은 사거리에 들면) 멈춘다. 목표가 있으면 다른 적을 지나쳐 간다.
    /// - **칸 예약**: 이동 중인 부대는 출발 칸과 도착 칸을 둘 다 차지한다. 다음 칸은 남은 거리가 큰 축으로 먼저, 막히면 다른 축,
    ///   그래도 막히면 옆으로 비켜 간다. 부대는 아이디 순서로 처리해 결과가 결정적이다.
    /// - **후퇴**: 자기 진영 가장자리 쪽 열로만 물러나고, 가장자리 밖으로 한 걸음 더 가면 전장에서 빠진다.
    /// - **돌격**: 기병(장수 부대 포함)이 칸을 하나 옮기면 장전되고, 다음 백병전 피해에 추가 피해가 한 번 붙는다.
    /// - **승패**: 적 장수 부대가 무너지거나 빠지면 승리, 내 장수 부대가 무너지거나 빠지면 패배. 내 부대가 무너지면 자동 정지.
    /// </summary>
    public sealed class FieldBattleModel
    {
        readonly FieldRules rules;
        readonly FieldSetup setup;
        readonly List<FieldUnit> units = new List<FieldUnit>(20);
        readonly FieldUnit[,] grid = new FieldUnit[FieldRules.Cols, FieldRules.Rows];
        FieldUnit[] victims = new FieldUnit[20];
        readonly int[] initialSoldierUnits = new int[2];
        float aiTimer;

        public FieldRules Rules => rules;
        public FieldSetup Setup => setup;
        public IReadOnlyList<FieldUnit> Units => units;
        public bool Paused { get; private set; } = true;
        public BattleOutcome Outcome { get; private set; }
        public bool Finished => Outcome != BattleOutcome.None;
        public float Elapsed { get; private set; }
        public FieldUnit PlayerGeneral { get; private set; }
        public FieldUnit EnemyGeneral { get; private set; }

        /// <summary>명령을 바꿀 수 있는가: 정지 중이고 아직 끝나지 않았을 때만.</summary>
        public bool CanCommand => Paused && !Finished;

        /// <summary>피해가 들어갔다 (공격자, 맞은 부대, 양, 원거리인가, 돌격인가). 매 틱 일어나므로 화면은 알아서 묶어서 보여 준다.</summary>
        public event Action<FieldUnit, FieldUnit, float, bool, bool> Hit;
        public event Action<FieldUnit> UnitRouted;
        public event Action<FieldUnit> UnitExited;
        /// <summary>내 부대가 무너져 저절로 정지했다.</summary>
        public event Action<FieldUnit> AutoPaused;
        public event Action<BattleOutcome> FinishedEvent;

        public FieldBattleModel(FieldSetup setup, FieldRules rules = null) : this(setup, rules, true) { }

        FieldBattleModel(FieldSetup setup, FieldRules rules, bool placeUnits)
        {
            this.setup = setup ?? new FieldSetup();
            this.rules = rules ?? FieldRules.Default();
            if (!placeUnits) return;
            Place(FieldSide.Player, this.setup.playerFormation, this.setup.playerMirror, this.setup.playerGeneralName);
            Place(FieldSide.Enemy, this.setup.enemyFormation, this.setup.enemyMirror, this.setup.enemyGeneralName);
        }

        /// <summary>부대 없이 빈 전장을 만든다. 테스트나 특별한 장면이 <see cref="AddUnit"/> 으로 직접 채운다.</summary>
        public static FieldBattleModel CreateEmpty(FieldSetup setup = null, FieldRules rules = null) => new FieldBattleModel(setup, rules, false);

        void Place(FieldSide side, FormationKind kind, bool mirror, string generalName)
        {
            var slots = FieldFormation.Layout(kind, mirror, side,
                setup.Count(side, UnitClass.Infantry), setup.Count(side, UnitClass.Archer), setup.Count(side, UnitClass.Cavalry));
            int[] serial = new int[3];
            foreach (var s in slots)
            {
                string name = s.isGeneral ? generalName : $"{FieldNames.Class(s.cls)}대 {++serial[(int)s.cls]}";
                AddUnit(side, s.cls, s.isGeneral, s.col, s.row, setup.soldiers, name);
            }
        }

        /// <summary>
        /// 부대를 한 칸에 놓는다 (칸이 차 있거나 밖이면 null). 장수 부대는 편마다 하나만: 이미 있으면 그 자리를 대신한다.
        /// 시작 뒤(한 번이라도 진행한 뒤)에는 쓰지 않는다. maxSoldiers 를 주면 이미 피해를 입은 부대로 놓는다.
        /// </summary>
        public FieldUnit AddUnit(FieldSide side, UnitClass cls, bool isGeneral, int col, int row, int soldiers, string name = null, int maxSoldiers = 0)
        {
            if (!InBounds(col, row) || grid[col, row] != null) return null;
            // 장수 개성(15-7): 내 장수 부대만 시작 조건의 배율을 받는다 (병사 수는 maxSoldiers 를 따로 주지 않았을 때만)
            bool heroGeneral = isGeneral && side == FieldSide.Player;
            if (heroGeneral && maxSoldiers <= 0)
            {
                soldiers = (int)Math.Round(soldiers * setup.playerGeneralSoldierMul);
                if (soldiers < 1) soldiers = 1;
            }
            var u = new FieldUnit
            {
                Id = units.Count, Side = side, Class = isGeneral ? UnitClass.Cavalry : cls, IsGeneral = isGeneral,
                Soldiers = soldiers, MaxSoldiers = maxSoldiers > 0 ? maxSoldiers : soldiers,
                DamageMul = heroGeneral ? setup.playerGeneralDamageMul : 1f, SpeedMul = heroGeneral ? setup.playerGeneralSpeedMul : 1f,
                Col = col, Row = row, Order = UnitOrder.Hold,
                FacingDx = side == FieldSide.Player ? 1 : -1,
            };
            u.Name = name ?? (isGeneral ? (side == FieldSide.Player ? setup.playerGeneralName : setup.enemyGeneralName) : FieldNames.Class(cls) + "대");
            units.Add(u);
            grid[col, row] = u;
            if (isGeneral)
            {
                if (side == FieldSide.Player) PlayerGeneral = u; else EnemyGeneral = u;
            }
            else initialSoldierUnits[(int)side]++;
            return u;
        }

        // ───────────────────────── 조회 ─────────────────────────

        public static bool InBounds(int col, int row) => col >= 0 && col < FieldRules.Cols && row >= 0 && row < FieldRules.Rows;
        public static int Distance(int c1, int r1, int c2, int r2) => Math.Abs(c1 - c2) + Math.Abs(r1 - r2);
        public static int Distance(FieldUnit a, FieldUnit b) => Distance(a.Col, a.Row, b.Col, b.Row);

        public FieldUnit Find(int id) => id >= 0 && id < units.Count ? units[id] : null;

        /// <summary>그 칸을 차지한 부대 (이동 중인 부대의 도착 칸도 차지한 것으로 본다).</summary>
        public FieldUnit UnitAt(int col, int row) => InBounds(col, row) ? grid[col, row] : null;

        public int CountAlive(FieldSide side, bool soldierUnitsOnly = false)
        {
            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Alive && u.Side == side && !(soldierUnitsOnly && u.IsGeneral)) n++;
            }
            return n;
        }

        /// <summary>시작할 때 그 편의 병사 부대 수 (적 장수 AI 가 절반 문턱을 재는 기준).</summary>
        public int InitialSoldierUnits(FieldSide side) => initialSoldierUnits[(int)side];

        /// <summary>가장 가까운 살아 있는 적 (거리가 같으면 아이디가 작은 쪽). maxDistance 를 주면 그 안에서만.</summary>
        public FieldUnit NearestEnemy(FieldUnit u, int maxDistance = int.MaxValue)
        {
            FieldUnit best = null;
            int bestDist = int.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                var o = units[i];
                if (!o.Alive || o.Side == u.Side) continue;
                int d = Distance(u, o);
                if (d > maxDistance || d >= bestDist) continue;
                bestDist = d;
                best = o;
            }
            return best;
        }

        // ───────────────────────── 명령 ─────────────────────────

        public void Pause() { Paused = true; }
        public void Resume() { if (!Finished) Paused = false; }
        public bool TogglePause() { if (Paused) Resume(); else Pause(); return Paused; }

        /// <summary>
        /// 내 부대에 명령을 준다. 정지 중에만, 살아 있는 내 부대에만 통한다. 전진의 목표는 살아 있는 적 부대여야 하고(아니면 목표 없음),
        /// 전진이 아닌 명령은 목표를 지운다.
        /// </summary>
        public bool SetOrder(int unitId, UnitOrder order, int targetId = -1)
        {
            if (!CanCommand) return false;
            var u = Find(unitId);
            if (u == null || !u.Alive || u.Side != FieldSide.Player) return false;
            ForceOrder(u, order, targetId);
            return true;
        }

        /// <summary>검사 없이 명령을 넣는다 (AI, SetOrder, 테스트/연출용). 어느 편이든, 정지 여부와 무관.</summary>
        public void ForceOrder(FieldUnit u, UnitOrder order, int targetId = -1)
        {
            u.Order = order;
            var t = Find(targetId);
            u.TargetId = order == UnitOrder.Advance && t != null && t.Alive && t.Side != u.Side ? targetId : -1;
        }

        // ───────────────────────── 진행 ─────────────────────────

        public void Tick(float dt)
        {
            if (Paused || Finished || dt <= 0f) return;
            Elapsed += dt;

            if (setup.enemyAi)
            {
                aiTimer += dt;
                if (aiTimer >= rules.aiInterval)
                {
                    aiTimer -= rules.aiInterval;
                    FieldAi.Decide(this, FieldSide.Enemy);
                }
            }

            // 1) 공격: 먼저 모두 상대를 고른 뒤 피해를 넣어 같은 틱 안에서는 동시에 때린 것이 된다.
            if (victims.Length < units.Count) victims = new FieldUnit[units.Count];
            for (int i = 0; i < units.Count; i++)
                victims[i] = units[i].Alive ? PickVictim(units[i]) : null;
            for (int i = 0; i < units.Count; i++)
                if (victims[i] != null) Strike(units[i], victims[i], dt);

            // 2) 무너진 부대 정리 (피해가 다 들어간 뒤 한 번에)
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Alive && u.Soldiers <= 0f) Rout(u);
            }
            if (Paused || Finished) return;   // 자동 정지는 무너진 그 순간의 전장을 그대로 멈춘다

            // 3) 이동
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Alive) Move(u, dt);
                if (Finished) return;
            }
        }

        /// <summary>이 틱에 때릴 적: 목표가 사거리 안이면 목표, 아니면 사거리 안에서 가장 가까운 적.</summary>
        FieldUnit PickVictim(FieldUnit u)
        {
            int range = rules.Stats(u.Class).range;
            var t = Find(u.TargetId);
            if (t != null && t.Alive && Distance(u, t) <= range) return t;
            return NearestEnemy(u, range);
        }

        void Strike(FieldUnit attacker, FieldUnit victim, float dt)
        {
            var st = rules.Stats(attacker.Class);
            float mul = rules.Affinity(attacker.Class, victim.Class, victim.IsGeneral) * rules.Stats(victim.Class).incomingMul;
            if (attacker.IsGeneral) mul *= rules.generalDamageMul;
            bool adjacent = Distance(attacker, victim) <= 1;
            if (adjacent) mul *= st.adjacentMul;
            bool ranged = !adjacent;

            float amount = st.damagePerSec * dt * mul;
            bool charge = false;
            if (adjacent && attacker.ChargeArmed)
            {
                attacker.ChargeArmed = false;
                if (st.chargeBonus > 0f)
                {
                    amount += st.chargeBonus * rules.Affinity(attacker.Class, victim.Class, victim.IsGeneral) * rules.Stats(victim.Class).incomingMul;
                    charge = true;
                }
            }

            amount *= attacker.DamageMul;   // 장수 개성 (돌격도 함께)
            victim.Soldiers -= amount;
            Hit?.Invoke(attacker, victim, amount, ranged, charge);
        }

        void Rout(FieldUnit u)
        {
            u.Soldiers = 0f;
            u.Routed = true;
            Release(u);
            u.Moving = false;   // 무너진 부대의 보간 위치가 더 움직이지 않게
            UnitRouted?.Invoke(u);
            if (u.IsGeneral)
            {
                Finish(u.Side == FieldSide.Player ? BattleOutcome.Defeat : BattleOutcome.Victory);
                return;
            }
            if (u.Side == FieldSide.Player)
            {
                Paused = true;
                AutoPaused?.Invoke(u);
            }
        }

        void Exit(FieldUnit u)
        {
            u.Exited = true;
            u.Moving = false;
            Release(u);
            UnitExited?.Invoke(u);
            if (u.IsGeneral) Finish(u.Side == FieldSide.Player ? BattleOutcome.Defeat : BattleOutcome.Victory);
        }

        void Finish(BattleOutcome outcome)
        {
            if (Finished) return;
            Outcome = outcome;
            Paused = true;
            FinishedEvent?.Invoke(outcome);
        }

        /// <summary>부대가 차지한 칸(출발·도착)을 비운다.</summary>
        void Release(FieldUnit u)
        {
            if (InBounds(u.Col, u.Row) && grid[u.Col, u.Row] == u) grid[u.Col, u.Row] = null;
            if (u.Moving && InBounds(u.NextCol, u.NextRow) && grid[u.NextCol, u.NextRow] == u) grid[u.NextCol, u.NextRow] = null;
        }

        // ───────────────────────── 이동 ─────────────────────────

        void Move(FieldUnit u, float dt)
        {
            var st = rules.Stats(u.Class);
            if (u.Moving)
            {
                u.MoveProgress += st.moveCellsPerSec * u.SpeedMul * dt;
                if (u.MoveProgress < 1f) return;
                // 도착: 출발 칸을 비우고 도착 칸에 선다. 기병은 돌격이 장전된다.
                grid[u.Col, u.Row] = null;
                u.LastCol = u.Col; u.LastRow = u.Row;
                u.Col = u.NextCol; u.Row = u.NextRow;
                u.Moving = false;
                u.MoveProgress = 0f;
                if (st.chargeBonus > 0f) u.ChargeArmed = true;
                return;   // 다음 칸은 다음 틱에 고른다 (한 틱에 두 칸을 건너뛰지 않게)
            }

            switch (u.Order)
            {
                case UnitOrder.Advance:
                {
                    var goal = Find(u.TargetId);
                    if (goal == null || !goal.Alive) goal = NearestEnemy(u);
                    if (goal == null) return;
                    if (Distance(u, goal) <= st.range) return;   // 닿았다(궁병은 사거리에 들었다): 멈춰서 싸운다
                    StepToward(u, goal.Col, goal.Row);
                    break;
                }
                case UnitOrder.Retreat:
                {
                    int dir = u.Side == FieldSide.Player ? -1 : 1;
                    int nc = u.Col + dir;
                    if (!InBounds(nc, u.Row)) { Exit(u); return; }
                    if (TryReserve(u, nc, u.Row)) return;
                    // 뒤가 막혔으면 옆으로 한 칸 비킨다
                    if (TryReserve(u, u.Col, u.Row + 1)) return;
                    TryReserve(u, u.Col, u.Row - 1);
                    break;
                }
            }
        }

        /// <summary>
        /// 목표 칸 쪽으로 한 칸 예약한다. 남은 거리가 큰 축 → 다른 축 → 거리가 늘지 않는 옆 칸 → 마지막으로 거리가 느는 옆 칸(아군 뒤에 갇힌 부대가
        /// 돌아 나갈 수 있게) 순서로 시도하고, 바로 직전에 떠난 칸은 다른 길이 없을 때만 돌아간다.
        /// </summary>
        void StepToward(FieldUnit u, int gc, int gr)
        {
            int dx = gc - u.Col, dy = gr - u.Row;
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            bool colFirst = Math.Abs(dx) >= Math.Abs(dy);

            if (colFirst)
            {
                if (sx != 0 && TryReserve(u, u.Col + sx, u.Row, avoidLast: true)) return;
                if (sy != 0 && TryReserve(u, u.Col, u.Row + sy, avoidLast: true)) return;
            }
            else
            {
                if (sy != 0 && TryReserve(u, u.Col, u.Row + sy, avoidLast: true)) return;
                if (sx != 0 && TryReserve(u, u.Col + sx, u.Row, avoidLast: true)) return;
            }
            // 옆으로 비켜 가기: 목표와 정렬된 축의 양쪽
            if (sy == 0)
            {
                if (TryReserve(u, u.Col, u.Row + 1, avoidLast: true)) return;
                if (TryReserve(u, u.Col, u.Row - 1, avoidLast: true)) return;
            }
            if (sx == 0)
            {
                if (TryReserve(u, u.Col + 1, u.Row, avoidLast: true)) return;
                if (TryReserve(u, u.Col - 1, u.Row, avoidLast: true)) return;
            }
            // 마지막: 직전 칸으로 되돌아가는 것도 허용
            if (sx != 0 && TryReserve(u, u.Col + sx, u.Row)) return;
            if (sy != 0 && TryReserve(u, u.Col, u.Row + sy)) return;
            if (TryReserve(u, u.Col, u.Row + 1)) return;
            if (TryReserve(u, u.Col, u.Row - 1)) return;
        }

        bool TryReserve(FieldUnit u, int col, int row, bool avoidLast = false)
        {
            if (!InBounds(col, row) || grid[col, row] != null) return false;
            if (avoidLast && col == u.LastCol && row == u.LastRow) return false;
            grid[col, row] = u;
            u.NextCol = col; u.NextRow = row;
            u.Moving = true;
            u.MoveProgress = 0f;
            u.FacingDx = Math.Sign(col - u.Col);
            u.FacingDy = Math.Sign(row - u.Row);
            return true;
        }
    }
}
