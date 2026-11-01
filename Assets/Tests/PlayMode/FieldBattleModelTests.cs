using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Field;

namespace Samkuk.Tests
{
    /// <summary>
    /// 야전 규칙(Step 15-1, `FieldBattleModel`): 배치, 명령(전진·대기·후퇴)과 목표, 지나치며 때리기, 궁병 사거리, 상성, 돌격,
    /// 후퇴 퇴장, 자동 정지, 승패, 적 AI, 결정성. 유니티 객체 없이 순수 로직만 돌린다.
    /// </summary>
    public class FieldBattleModelTests
    {
        /// <summary>진행 틱. 1/16 초는 부동소수점으로 정확히 더해져 칸 도착 시점이 어긋나지 않는다.</summary>
        const float Dt = 1f / 16f;

        static FieldSetup Quiet()
        {
            return new FieldSetup { enemyAi = false };
        }

        static void Run(FieldBattleModel m, float seconds)
        {
            m.Resume();
            int ticks = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < ticks && !m.Finished; i++) m.Tick(Dt);
        }

        // ───────────────────────── 배치 ─────────────────────────

        [Test]
        public void Formation_AllKinds_NoOverlap_AndOnOwnSide()
        {
            int[] counts = { 0, 2, 3 };
            foreach (FormationKind kind in System.Enum.GetValues(typeof(FormationKind)))
            foreach (bool mirror in new[] { false, true })
            foreach (FieldSide side in new[] { FieldSide.Player, FieldSide.Enemy })
            foreach (int n in counts)
            {
                var slots = FieldFormation.Layout(kind, mirror, side, n, n, n);
                Assert.AreEqual(1 + 3 * n, slots.Count, $"{kind} {mirror} {side} {n}: 부대 수");
                var cells = new HashSet<(int, int)>();
                int generals = 0;
                foreach (var s in slots)
                {
                    Assert.IsTrue(cells.Add((s.col, s.row)), $"{kind} {mirror} {side} {n}: ({s.col},{s.row}) 겹침");
                    Assert.IsTrue(s.row >= 0 && s.row < FieldRules.Rows, "행 범위");
                    if (side == FieldSide.Player) Assert.IsTrue(s.col >= 0 && s.col <= FieldRules.PlayerFrontCol, $"내 편 열 {s.col}");
                    else Assert.IsTrue(s.col >= FieldRules.EnemyFrontCol && s.col < FieldRules.Cols, $"적 열 {s.col}");
                    if (s.isGeneral) { generals++; Assert.AreEqual(UnitClass.Cavalry, s.cls, "장수 부대는 기병"); }
                }
                Assert.AreEqual(1, generals, "장수 부대는 하나");
            }
        }

        [Test]
        public void Formation_Mirror_FlipsRows()
        {
            var a = FieldFormation.Layout(FormationKind.Wings, false, FieldSide.Player, 2, 2, 2);
            var b = FieldFormation.Layout(FormationKind.Wings, true, FieldSide.Player, 2, 2, 2);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].col, b[i].col);
                Assert.AreEqual(FieldRules.Rows - 1 - a[i].row, b[i].row);
            }
        }

        [Test]
        public void DefaultSetup_Places14Units_AllHolding_Paused()
        {
            var m = new FieldBattleModel(new FieldSetup());
            Assert.AreEqual(14, m.Units.Count);
            Assert.IsTrue(m.Paused);
            Assert.IsNotNull(m.PlayerGeneral);
            Assert.IsNotNull(m.EnemyGeneral);
            Assert.AreEqual(6, m.InitialSoldierUnits(FieldSide.Player));
            foreach (var u in m.Units)
            {
                Assert.AreEqual(UnitOrder.Hold, u.Order);
                Assert.AreSame(u, m.UnitAt(u.Col, u.Row));
                Assert.AreEqual(300f, u.Soldiers);
            }
        }

        // ───────────────────────── 명령·이동 ─────────────────────────

        [Test]
        public void Advance_NoTarget_WalksToNearestEnemy_AndStopsAdjacent()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var inf = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 2, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 8, 4, 100000);
            Assert.IsTrue(m.SetOrder(inf.Id, UnitOrder.Advance));
            Run(m, 12f);
            Assert.AreEqual(7, inf.Col);
            Assert.AreEqual(4, inf.Row);
            Assert.IsFalse(inf.Moving);
            Assert.AreEqual(1, FieldBattleModel.Distance(inf, foe));
            Assert.Less(foe.Soldiers, 100000f, "닿은 뒤 때린다");
        }

        [Test]
        public void Cavalry_MovesTwiceAsFast_AsInfantry()
        {
            var a = FieldBattleModel.CreateEmpty(Quiet());
            var inf = a.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 2, 4, 300);
            a.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 11, 4, 300);
            a.SetOrder(inf.Id, UnitOrder.Advance);
            Run(a, 3f);

            var b = FieldBattleModel.CreateEmpty(Quiet());
            var cav = b.AddUnit(FieldSide.Player, UnitClass.Cavalry, false, 2, 4, 300);
            b.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 11, 4, 300);
            b.SetOrder(cav.Id, UnitOrder.Advance);
            Run(b, 3f);

            int infMoved = inf.Col - 2, cavMoved = cav.Col - 2;
            Assert.AreEqual(2, infMoved, "보병 1칸/초 (칸마다 고르는 틱 하나가 끼어 3초에 2칸)");
            Assert.GreaterOrEqual(cavMoved, 4, "기병 2칸/초");
        }

        [Test]
        public void Advance_WithTarget_PassesOtherEnemy_HittingWithoutStopping()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var cav = m.AddUnit(FieldSide.Player, UnitClass.Cavalry, false, 2, 4, 1000);
            var bystander = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 3, 1000);   // 길목 (4,4) 옆
            var target = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 10, 4, 1000);
            Assert.IsTrue(m.SetOrder(cav.Id, UnitOrder.Advance, target.Id));
            Assert.AreEqual(target.Id, cav.TargetId);
            Run(m, 6f);
            Assert.GreaterOrEqual(cav.Col, 8, "지나쳐서 목표 앞까지 간다");
            Assert.Less(bystander.Soldiers, 1000f, "지나치며 때렸다");
            Assert.Less(target.Soldiers, 1000f, "목표도 때린다");
        }

        [Test]
        public void Hold_DoesNotMove_ButFightsAdjacent()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var inf = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 3, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 4, 300);
            Run(m, 1f);
            Assert.AreEqual(3, inf.Col);
            Assert.IsFalse(inf.Moving);
            Assert.AreEqual(300f - 20f * 0.8f, foe.Soldiers, 0.01f, "보병 20/초, 보병 받는 피해 x0.8");
            Assert.AreEqual(300f - 20f * 0.8f, inf.Soldiers, 0.01f, "서로 때린다");
        }

        [Test]
        public void Paused_TickDoesNothing_AndOrdersOnlyWhilePaused()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var inf = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 3, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 4, 300);
            m.SetOrder(inf.Id, UnitOrder.Advance);
            m.Tick(1f);
            Assert.AreEqual(0f, m.Elapsed);
            Assert.AreEqual(300f, foe.Soldiers);

            Assert.IsFalse(m.SetOrder(foe.Id, UnitOrder.Advance), "적 부대에는 명령 못 함");
            m.Resume();
            Assert.IsFalse(m.CanCommand);
            Assert.IsFalse(m.SetOrder(inf.Id, UnitOrder.Hold), "진행 중엔 명령 못 함");
            Assert.AreEqual(UnitOrder.Advance, inf.Order);
            m.Pause();
            Assert.IsTrue(m.SetOrder(inf.Id, UnitOrder.Hold));
        }

        [Test]
        public void BoxedIn_Unit_SidestepsAndReachesEnemy()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var a = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 0, 4, 300);
            m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 1, 4, 300);   // 바로 앞을 막는 아군 (대기)
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 9, 4, 100000);
            m.SetOrder(a.Id, UnitOrder.Advance);
            Run(m, 20f);
            Assert.AreEqual(1, FieldBattleModel.Distance(a, foe), $"비켜 가서 닿아야 한다 (지금 ({a.Col},{a.Row}))");
        }

        // ───────────────────────── 궁병 ─────────────────────────

        [Test]
        public void Archer_HitsAtRange3_NotAt4_ThroughFriends()
        {
            var a = FieldBattleModel.CreateEmpty(Quiet());
            var arc = a.AddUnit(FieldSide.Player, UnitClass.Archer, false, 2, 4, 300);
            a.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 3, 4, 300);   // 시야를 가리는 아군
            var foe3 = a.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, false, 5, 4, 300);
            Run(a, 1f);
            Assert.AreEqual(300f - 14f * 1.5f, foe3.Soldiers, 0.01f, "궁병 14/초 x 기병 상성 1.5, 아군 너머로");
            Assert.AreEqual(300f, arc.Soldiers, "기병은 닿지 않아 못 때린다");

            var b = FieldBattleModel.CreateEmpty(Quiet());
            b.AddUnit(FieldSide.Player, UnitClass.Archer, false, 2, 4, 300);
            var foe4 = b.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, false, 6, 4, 300);
            Run(b, 1f);
            Assert.AreEqual(300f, foe4.Soldiers, "4칸은 사거리 밖");
        }

        [Test]
        public void Archer_AdjacentDamageHalved()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            m.AddUnit(FieldSide.Player, UnitClass.Archer, false, 2, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, false, 3, 4, 300);
            Run(m, 1f);
            Assert.AreEqual(300f - 14f * 1.5f * 0.5f, foe.Soldiers, 0.01f);
        }

        [Test]
        public void Archer_Advance_StopsWhenInRange()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var arc = m.AddUnit(FieldSide.Player, UnitClass.Archer, false, 1, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 9, 4, 100000);
            m.SetOrder(arc.Id, UnitOrder.Advance);
            Run(m, 12f);
            Assert.AreEqual(3, FieldBattleModel.Distance(arc, foe));
            Assert.IsFalse(arc.Moving);
        }

        // ───────────────────────── 상성·돌격 ─────────────────────────

        [Test]
        public void Affinity_RockPaperScissors_GeneralImmuneToArchers()
        {
            var r = FieldRules.Default();
            Assert.AreEqual(r.strongMul, r.Affinity(UnitClass.Infantry, UnitClass.Archer, false));
            Assert.AreEqual(r.weakMul, r.Affinity(UnitClass.Archer, UnitClass.Infantry, false));
            Assert.AreEqual(r.strongMul, r.Affinity(UnitClass.Archer, UnitClass.Cavalry, false));
            Assert.AreEqual(r.weakMul, r.Affinity(UnitClass.Cavalry, UnitClass.Archer, false));
            Assert.AreEqual(r.strongMul, r.Affinity(UnitClass.Cavalry, UnitClass.Infantry, false));
            Assert.AreEqual(r.weakMul, r.Affinity(UnitClass.Infantry, UnitClass.Cavalry, false));
            Assert.AreEqual(1f, r.Affinity(UnitClass.Archer, UnitClass.Cavalry, true), "장수 부대에는 궁병 상성이 없다");
            Assert.AreEqual(1f, r.Affinity(UnitClass.Infantry, UnitClass.Infantry, false));
        }

        [Test]
        public void General_DealsBonusDamage()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 3, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Archer, false, 4, 4, 300);
            Run(m, 1f);
            Assert.AreEqual(300f - 28f * 1.3f * 0.6f, foe.Soldiers, 0.01f, "기병 28 x 장수 1.3 x 궁병 상대 0.6");
        }

        // ───────────────────────── 장수 개성 (15-7) ─────────────────────────

        [Test]
        public void GeneralMultipliers_ScaleSoldiers_OnlyForThePlayerGeneral()
        {
            var s = Quiet();
            s.playerGeneralSoldierMul = 1.3f; s.playerGeneralDamageMul = 2f; s.playerGeneralSpeedMul = 1.5f;
            var m = new FieldBattleModel(s);
            Assert.AreEqual(390, m.PlayerGeneral.MaxSoldiers);
            Assert.AreEqual(390f, m.PlayerGeneral.Soldiers, 0.01f);
            Assert.AreEqual(2f, m.PlayerGeneral.DamageMul);
            Assert.AreEqual(1.5f, m.PlayerGeneral.SpeedMul);
            Assert.AreEqual(300, m.EnemyGeneral.MaxSoldiers, "적 장수 부대는 그대로");
            Assert.AreEqual(1f, m.EnemyGeneral.DamageMul);
            foreach (var u in m.Units)
                if (!u.IsGeneral) { Assert.AreEqual(300, u.MaxSoldiers, u.Name); Assert.AreEqual(1f, u.DamageMul); Assert.AreEqual(1f, u.SpeedMul); }
        }

        [Test]
        public void GeneralDamageMul_DoublesDamage_IncludingCharge()
        {
            float Loss(float mul, bool charge)
            {
                var s = Quiet(); s.playerGeneralDamageMul = mul;
                var m = FieldBattleModel.CreateEmpty(s);
                var gen = m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, charge ? 2 : 3, 4, 100000);
                var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 4, 100000);
                if (charge) m.SetOrder(gen.Id, UnitOrder.Advance);
                Run(m, charge ? 3f : 1f);
                return 100000f - foe.Soldiers;
            }
            // 틱 수가 1 어긋날 수 있어(부동소수) 비율로 본다
            Assert.AreEqual(2f, Loss(2f, false) / Loss(1f, false), 0.02f, "서 있는 타격 2배");
            Assert.AreEqual(2f, Loss(2f, true) / Loss(1f, true), 0.02f, "돌격 포함 2배");
        }

        [Test]
        public void GeneralSpeedMul_MovesFaster()
        {
            int TicksToLeaveCell(float mul)
            {
                var s = Quiet(); s.playerGeneralSpeedMul = mul;
                var m = FieldBattleModel.CreateEmpty(s);
                var gen = m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 1, 4, 300);
                m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 10, 4, 300);
                m.SetOrder(gen.Id, UnitOrder.Advance);
                m.Resume();
                int ticks = 0;
                while (gen.Col == 1 && ticks < 200) { m.Tick(0.05f); ticks++; }
                return ticks;
            }
            int normal = TicksToLeaveCell(1f), fast = TicksToLeaveCell(2f);
            Assert.Less(fast, normal, "2배면 더 빨리 다음 칸에 닿는다");
            Assert.AreEqual(normal, fast * 2, 1, "대략 절반");
        }

        [Test]
        public void Charge_AppliesOnceAfterMoving_NotWhenStanding()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var cav = m.AddUnit(FieldSide.Player, UnitClass.Cavalry, false, 2, 4, 100000);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 5, 4, 100000);
            int charges = 0;
            float chargeAmount = 0f;
            m.Hit += (a, v, amount, ranged, charge) => { if (charge) { charges++; chargeAmount = amount; } };
            m.SetOrder(cav.Id, UnitOrder.Advance);
            Run(m, 5f);
            Assert.AreEqual(1, charges, "이동 뒤 첫 타격에만");
            Assert.Greater(chargeAmount, 60f * 1.5f * 0.8f - 0.01f, "돌격 60 x 보병 상대 1.5 x 보병 받는 피해 0.8 + 틱 피해");

            var s = FieldBattleModel.CreateEmpty(Quiet());
            s.AddUnit(FieldSide.Player, UnitClass.Cavalry, false, 4, 4, 300);
            s.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 5, 4, 300);
            int standing = 0;
            s.Hit += (a, v, amount, ranged, charge) => { if (charge) standing++; };
            Run(s, 2f);
            Assert.AreEqual(0, standing, "서서 때리면 돌격이 없다");
        }

        // ───────────────────────── 후퇴·퇴장 ─────────────────────────

        [Test]
        public void Retreat_WalksToOwnEdge_AndExits()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var inf = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 2, 4, 300);
            m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 0, 1, 300);
            m.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 11, 4, 300);
            FieldUnit exited = null;
            m.UnitExited += u => exited = u;
            m.SetOrder(inf.Id, UnitOrder.Retreat);
            Run(m, 1.5f);
            Assert.AreEqual(1, inf.Col, "뒤로 걸어간다 (아직 보인다)");
            Assert.IsTrue(inf.Alive);
            Run(m, 5f);
            Assert.IsTrue(inf.Exited);
            Assert.AreSame(inf, exited);
            Assert.IsNull(m.UnitAt(0, 4));
            Assert.AreEqual(BattleOutcome.None, m.Outcome, "병사 부대가 빠져도 끝나지 않는다");
        }

        [Test]
        public void Retreat_CanBeCancelled_BeforeExit()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var inf = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 2, 4, 300);
            m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 11, 4, 300);
            m.SetOrder(inf.Id, UnitOrder.Retreat);
            Run(m, 1.5f);
            m.Pause();
            m.SetOrder(inf.Id, UnitOrder.Advance);
            Run(m, 5f);   // 가던 칸(0)까지 마저 간 뒤 돌아선다
            Assert.IsTrue(inf.Alive);
            Assert.GreaterOrEqual(inf.Col, 2, "돌아선다");
        }

        [Test]
        public void EnemyRetreat_ExitsAtRightEdge()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 0, 4, 300);
            var foe = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 10, 4, 300);
            m.ForceOrder(foe, UnitOrder.Retreat);
            Run(m, 5f);
            Assert.IsTrue(foe.Exited);
        }

        [Test]
        public void PlayerGeneral_Exit_IsDefeat()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var gen = m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 1, 4, 300);
            m.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 11, 4, 300);
            BattleOutcome got = BattleOutcome.None;
            m.FinishedEvent += o => got = o;
            m.SetOrder(gen.Id, UnitOrder.Retreat);
            Run(m, 5f);
            Assert.AreEqual(BattleOutcome.Defeat, m.Outcome);
            Assert.AreEqual(BattleOutcome.Defeat, got);
            Assert.IsTrue(m.Paused);
        }

        // ───────────────────────── 무너짐·승패 ─────────────────────────

        [Test]
        public void PlayerUnitRouted_AutoPauses()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            var weak = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 3, 4, 1);
            m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 0, 4, 300);
            m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 4, 300);
            FieldUnit paused = null;
            m.AutoPaused += u => paused = u;
            m.Resume();
            m.Tick(1f);
            Assert.IsTrue(weak.Routed);
            Assert.IsTrue(m.Paused);
            Assert.AreSame(weak, paused);
            Assert.IsNull(m.UnitAt(3, 4));
            Assert.AreEqual(BattleOutcome.None, m.Outcome);
        }

        [Test]
        public void EnemyUnitRouted_DoesNotPause()
        {
            var m = FieldBattleModel.CreateEmpty(Quiet());
            m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 3, 4, 300);
            var weak = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 4, 1);
            m.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 11, 4, 300);
            m.Resume();
            m.Tick(1f);
            Assert.IsTrue(weak.Routed);
            Assert.IsFalse(m.Paused);
            Assert.AreEqual(BattleOutcome.None, m.Outcome);
        }

        [Test]
        public void EnemyGeneralRouted_IsVictory_PlayerGeneralRouted_IsDefeat()
        {
            var a = FieldBattleModel.CreateEmpty(Quiet());
            a.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 3, 4, 300);
            a.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 4, 4, 1);
            a.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 11, 4, 300);
            a.Resume();
            a.Tick(1f);
            Assert.AreEqual(BattleOutcome.Victory, a.Outcome, "적 병사 부대가 남아도 장수가 무너지면 승리");
            Assert.IsTrue(a.Finished);
            Assert.IsFalse(a.CanCommand);

            var b = FieldBattleModel.CreateEmpty(Quiet());
            b.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 3, 4, 1);
            b.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 4, 4, 300);
            b.Resume();
            b.Tick(1f);
            Assert.AreEqual(BattleOutcome.Defeat, b.Outcome);
        }

        // ───────────────────────── 적 AI ─────────────────────────

        [Test]
        public void Ai_ArcherHoldsInRange_AdvancesOtherwise_InfantryAdvances()
        {
            var a = FieldBattleModel.CreateEmpty(new FieldSetup { enemyAi = true });
            a.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 6, 4, 300);
            var arcNear = a.AddUnit(FieldSide.Enemy, UnitClass.Archer, false, 9, 4, 300);     // 거리 3
            var arcFar = a.AddUnit(FieldSide.Enemy, UnitClass.Archer, false, 10, 7, 300);     // 거리 7
            var inf = a.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 11, 1, 300);
            a.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 11, 8, 300);
            Run(a, 1f);
            Assert.AreEqual(UnitOrder.Hold, arcNear.Order);
            Assert.AreEqual(UnitOrder.Advance, arcFar.Order);
            Assert.AreEqual(UnitOrder.Advance, inf.Order);
        }

        [Test]
        public void Ai_LowSoldiers_Retreat_ExceptGeneral()
        {
            var m = FieldBattleModel.CreateEmpty(new FieldSetup { enemyAi = true });
            m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 0, 0, 300);
            var low = m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 11, 4, 30, null, 300);   // 10% 남음
            var gen = m.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 11, 6, 30, null, 300);
            Run(m, 1f);
            Assert.AreEqual(UnitOrder.Retreat, low.Order);
            Assert.AreNotEqual(UnitOrder.Retreat, gen.Order, "장수 부대는 후퇴하지 않는다");
        }

        [Test]
        public void Ai_GeneralHolds_UntilPlayerSoldierUnitsHalved()
        {
            var m = FieldBattleModel.CreateEmpty(new FieldSetup { enemyAi = true });
            m.AddUnit(FieldSide.Player, UnitClass.Cavalry, true, 0, 0, 300);
            m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 0, 8, 300);
            var weak = m.AddUnit(FieldSide.Player, UnitClass.Infantry, false, 8, 4, 1);
            m.AddUnit(FieldSide.Enemy, UnitClass.Infantry, false, 9, 4, 300);
            var gen = m.AddUnit(FieldSide.Enemy, UnitClass.Cavalry, true, 11, 4, 300);
            Assert.AreEqual(2, m.InitialSoldierUnits(FieldSide.Player));

            // 첫 틱(0.5초)에 약한 부대가 무너져 자동 정지. AI 는 1초마다라 아직 판단하지 않았다.
            m.Resume();
            m.Tick(0.5f);
            Assert.IsTrue(weak.Routed);
            Assert.IsTrue(m.Paused);
            Assert.AreEqual(UnitOrder.Hold, gen.Order);

            Run(m, 1f);
            Assert.AreEqual(UnitOrder.Advance, gen.Order, "내 병사 부대 2 → 1 (절반) 이면 전진");
        }

        [Test]
        public void Ai_Off_EnemyStaysHolding()
        {
            var m = new FieldBattleModel(new FieldSetup { enemyAi = false });
            Run(m, 3f);
            foreach (var u in m.Units)
                if (u.Side == FieldSide.Enemy) Assert.AreEqual(UnitOrder.Hold, u.Order);
        }

        // ───────────────────────── 결정성 ─────────────────────────

        [Test]
        public void SameSetup_ProducesSameState()
        {
            FieldBattleModel Make()
            {
                var m = new FieldBattleModel(new FieldSetup { enemyAi = true, playerFormation = FormationKind.Wings, enemyFormation = FormationKind.GiBoGung });
                foreach (var u in m.Units)
                    if (u.Side == FieldSide.Player) m.SetOrder(u.Id, UnitOrder.Advance);
                return m;
            }
            var a = Make();
            var b = Make();
            Run(a, 20f);
            Run(b, 20f);
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.Elapsed, b.Elapsed);
            for (int i = 0; i < a.Units.Count; i++)
            {
                Assert.AreEqual(a.Units[i].Soldiers, b.Units[i].Soldiers);
                Assert.AreEqual(a.Units[i].Col, b.Units[i].Col);
                Assert.AreEqual(a.Units[i].Row, b.Units[i].Row);
                Assert.AreEqual(a.Units[i].Order, b.Units[i].Order);
            }
        }

        [Test]
        public void FullBattle_AllAdvance_EndsWithinTwoMinutes()
        {
            var m = new FieldBattleModel(new FieldSetup { enemyAi = true });
            foreach (var u in m.Units)
                if (u.Side == FieldSide.Player) m.SetOrder(u.Id, UnitOrder.Advance);
            int guard = 0;
            m.Resume();
            while (!m.Finished && guard++ < 16 * 120)
            {
                if (m.Paused) m.Resume();   // 자동 정지는 바로 재개
                m.Tick(Dt);
            }
            Assert.IsTrue(m.Finished, "양쪽이 다 전진하면 2분 안에 끝난다");
        }
    }
}
