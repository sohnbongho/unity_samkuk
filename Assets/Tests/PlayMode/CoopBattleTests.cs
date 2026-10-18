using System.Collections.Generic;
using Messages;
using NUnit.Framework;
using Samkuk.Coop;
using Samkuk.Player;
using Samkuk.Upgrades;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>협동 전투(Step 15-4)의 순수 부분: 단위 변환, 보간 트랙, 스냅샷 모델(분할 조각, 숨김, 궤도 각도), 서버 선택지 → 카드.</summary>
    public class CoopBattleTests
    {
        static BattleSnapshotNoti Snapshot(int tick, int elapsedMs, params (int slot, int x, int y, int flags)[] players)
        {
            var n = new BattleSnapshotNoti { Tick = tick, ElapsedMs = elapsedMs, Enemies = new EnemyStates(), Gems = new GemStates(), Stage = new StageState { RemainingMs = 60000 } };
            foreach (var p in players) n.Players.Add(new PlayerState { Slot = p.slot, X = p.x, Y = p.y, Flags = p.flags, Hp = 100, MaxHp = 100, Level = 1 });
            return n;
        }

        static void AddEnemy(BattleSnapshotNoti n, int id, int x, int y, int flags)
        {
            n.Enemies.Ids.Add(id); n.Enemies.Xs.Add(x); n.Enemies.Ys.Add(y); n.Enemies.Flags.Add(flags);
        }

        [Test]
        public void NetUnits_ConvertsServerIntegers()
        {
            Assert.AreEqual(new Vector2(1.5f, -2f), NetUnits.Pos(150, -200));
            Assert.AreEqual(1f, NetUnits.Dir(1000, 0).x, 1e-5f);
            Assert.AreEqual(1f, NetUnits.Dir(300, 400).magnitude, 1e-5f, "방향은 정규화");
            Assert.AreEqual(Vector2.right, NetUnits.Dir(0, 0), "0 벡터면 오른쪽");
            Assert.AreEqual(12f, NetUnits.Speed(1200));
            Assert.AreEqual(2.5f, NetUnits.Seconds(2500));
        }

        [Test]
        public void MotionTrack_InterpolatesOverSnapshotInterval_AndSnapsWhenFar()
        {
            var t = new MotionTrack();
            t.Push(new Vector2(0f, 0f), 0.1f);
            Assert.AreEqual(Vector2.zero, t.Current, "처음 위치는 바로 놓는다");
            t.Push(new Vector2(1f, 0f), 0.1f);
            Assert.AreEqual(Vector2.zero, t.Current);
            t.Advance(0.05f);
            Assert.AreEqual(0.5f, t.Current.x, 1e-4f);
            Assert.IsTrue(t.IsMoving);
            Assert.AreEqual(10f, t.Velocity.x, 1e-4f);
            t.Advance(0.1f);
            Assert.AreEqual(1f, t.Current.x, 1e-4f);
            Assert.IsFalse(t.IsMoving);

            t.Push(new Vector2(40f, 0f), 0.1f);
            Assert.AreEqual(40f, t.Current.x, "너무 멀면(재배치) 보간 없이 바로");
        }

        [Test]
        public void CoopBattleModel_AppliesFullSnapshot_AndEventOnlyFragment()
        {
            var model = new CoopBattleModel(1, 10);
            var events = new List<BattleEvent>();
            var full = Snapshot(5, 1500, (0, 100, 0, 0), (1, 0, 200, PlayerTrack.FlagMoving));
            AddEnemy(full, 7, 300, 300, EnemyTrack.FlagMoving | EnemyTrack.FlagShotWarning);
            full.Events.Add(new BattleEvent { EnemySpawn = new EnemySpawnEvent { Id = 7, EnemyId = "Enemy_Soldier", X = 300, Y = 300 } });
            model.Apply(full, 10f, events);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1500, model.ElapsedMs);
            Assert.IsTrue(model.HasSnapshot);
            Assert.AreEqual(new Vector2(0f, 2f), model.Me.Motion.Current);
            Assert.IsTrue(model.Me.Moving);
            Assert.IsTrue(model.Players[0].Seen);
            Assert.IsFalse(model.Players[2].Seen);
            Assert.IsTrue(model.Enemies.TryGetValue(7, out var enemy));
            Assert.IsTrue(enemy.Moving);
            Assert.IsTrue(enemy.ShotWarning);
            Assert.IsFalse(enemy.Stunned);
            Assert.AreEqual(10f, enemy.LastSeen);

            // 같은 tick 의 뒤쪽 조각: players 가 비어 있으면 상태는 그대로, 이벤트만
            var fragment = new BattleSnapshotNoti { Tick = 5, ElapsedMs = 999999 };
            fragment.Events.Add(new BattleEvent { GemSpawn = new GemSpawnEvent { Id = 3, X = 0, Y = 0, Value = 1 } });
            events.Clear();
            model.Apply(fragment, 10.05f, events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1500, model.ElapsedMs, "조각은 경과 시간을 덮어쓰지 않는다");
            Assert.AreEqual(10f, model.LastSnapshotAt);
        }

        [Test]
        public void CoopBattleModel_Expire_HidesUnseenEnemies_KeepsFloorGems()
        {
            var model = new CoopBattleModel(0, 10);
            model.SpawnEnemy(1, "Enemy_Soldier", Vector2.zero, 0f);
            model.SpawnEnemy(2, "Enemy_Soldier", Vector2.one, 0f);
            model.SpawnGem(10, Vector2.zero, 1, 0f);                  // 바닥의 젬 (스냅샷에 안 담김)
            var gem = model.SpawnGem(11, Vector2.zero, 1, 0f);
            gem.Attracted = true;                                     // 끌려가는 중인데 스냅샷에서 빠짐

            var snap = Snapshot(1, 100, (0, 0, 0, 0));
            AddEnemy(snap, 1, 0, 0, 0);
            model.Apply(snap, 1f, null);

            var gone = new List<int>(); var goneGems = new List<int>();
            model.Expire(1.3f, gone, goneGems);
            Assert.IsEmpty(gone, "0.5초까지는 기다린다");
            model.Expire(1.6f, gone, goneGems);
            CollectionAssert.AreEqual(new[] { 2 }, gone);
            CollectionAssert.AreEqual(new[] { 11 }, goneGems);
            Assert.IsTrue(model.Enemies.ContainsKey(1));
            Assert.IsTrue(model.Gems.ContainsKey(10), "바닥의 젬은 GemCollect 가 올 때까지 남는다");
            Assert.IsTrue(model.RemoveGem(10));
        }

        [Test]
        public void CoopBattleModel_FaceDirection_Elapsed_AndOrbitAngle()
        {
            var model = new CoopBattleModel(0, 10);
            model.Apply(Snapshot(1, 2000, (0, 0, 0, 0), (1, 1000, 0, PlayerTrack.FlagDead)), 5f, null);
            var dir = model.FaceDirectionFrom(new Vector2(5f, 0f));
            Assert.Less(dir.x, 0f, "죽은 플레이어(1)는 빼고 살아 있는 0번 쪽(왼쪽)을 본다");

            Assert.AreEqual(2.5f, model.ElapsedSeconds(5.5f), 1e-4f, "마지막 스냅샷 뒤 흐른 시간을 더한다");
            model.Apply(new BattleSnapshotNoti { Tick = 2, ElapsedMs = 2000, Stage = new StageState { Paused = true }, Players = { new PlayerState { Slot = 0 } } }, 6f, null);
            Assert.AreEqual(2f, model.ElapsedSeconds(9f), 1e-4f, "일시정지 중에는 멈춘다");

            Assert.AreEqual(90f, CoopBattleModel.OrbitAngle(0.5f, 180f, 0, 2), 1e-4f);
            Assert.AreEqual(270f, CoopBattleModel.OrbitAngle(0.5f, 180f, 1, 2), 1e-4f);
            Assert.AreEqual(0f, CoopBattleModel.OrbitAngle(2f, 180f, 0, 1), 1e-4f, "360 으로 나눈 나머지");
        }

        [Test]
        public void CoopBattleModel_ChoosingFlag_ReviveCountdown_AndRevivedSnap()
        {
            var model = new CoopBattleModel(0, 10);
            var snap = Snapshot(1, 100, (0, 0, 0, PlayerTrack.FlagDead), (1, 500, 0, PlayerTrack.FlagChoosing | PlayerTrack.FlagInvulnerable));
            snap.Players[0].ReviveMs = 7300;
            snap.Stage.RevivesLeft = 2;
            model.Apply(snap, 1f, null);
            Assert.AreEqual(2, model.Stage.RevivesLeft, "팀 공용 부활 횟수는 스테이지 상태로 온다");
            Assert.IsTrue(model.Me.Dead);
            Assert.AreEqual(7.3f, model.Me.ReviveSeconds, 1e-4f);
            Assert.IsTrue(model.Players[1].Choosing);
            Assert.IsTrue(model.Players[1].Invulnerable);
            Assert.IsFalse(model.Players[1].Dead);

            model.Apply(Snapshot(2, 200, (0, 100, 0, PlayerTrack.FlagDead)), 1.1f, null);
            model.Advance(0.05f);
            model.RevivePlayer(0, new Vector2(9f, 9f));
            Assert.AreEqual(new Vector2(9f, 9f), model.Me.Motion.Current, "부활 위치로 바로 옮긴다");
            Assert.IsFalse(model.Me.Dead);
            Assert.AreEqual(0f, model.Me.ReviveSeconds);
            model.RevivePlayer(9, Vector2.zero); // 범위 밖은 무시
        }

        [Test]
        public void CoopBattleDriver_BuildOptions_MapsServerKindToCards()
        {
            var noti = new LevelUpChoiceNoti { ChoiceId = 3, TimeoutMs = 15000 };
            noti.Options.Add(new UpgradeChoice { Index = 0, Kind = 0, Title = "새 무기: 활", Description = "활을 얻는다", Id = "Weapon_Bow", Level = 1 });
            noti.Options.Add(new UpgradeChoice { Index = 1, Kind = 4, Title = "", Id = "Weapon_Sword", EvolvedWeaponId = "Weapon_Evo_Zanmato" });
            noti.Options.Add(new UpgradeChoice { Index = 2, Kind = 3, Title = "회복" });
            noti.Options.Add(new UpgradeChoice { Index = 3, Kind = 99, Title = "?" });
            var options = CoopBattleDriver.BuildOptions(noti);
            Assert.AreEqual(4, options.Count);
            Assert.AreEqual(UpgradeKind.NewWeapon, options[0].Kind);
            Assert.AreEqual("새 무기: 활", options[0].Title);
            Assert.AreEqual(UpgradeKind.Evolve, options[1].Kind);
            Assert.AreEqual("Weapon_Sword", options[1].Title, "제목이 비면 id");
            Assert.AreEqual(UpgradeKind.Heal, options[2].Kind);
            Assert.AreEqual(UpgradeKind.Passive, options[3].Kind, "모르는 종류는 패시브 색");
            Assert.AreEqual("", options[2].Description);
        }

        [Test]
        public void PuppetWalker_FacingOf_MatchesSheetRowOrder()
        {
            Assert.AreEqual(FacingDir.Down, PuppetWalker.FacingOf(0));
            Assert.AreEqual(FacingDir.Up, PuppetWalker.FacingOf(1));
            Assert.AreEqual(FacingDir.Left, PuppetWalker.FacingOf(2));
            Assert.AreEqual(FacingDir.Right, PuppetWalker.FacingOf(3));
            Assert.AreEqual(FacingDir.Down, PuppetWalker.FacingOf(9));
        }
    }
}
