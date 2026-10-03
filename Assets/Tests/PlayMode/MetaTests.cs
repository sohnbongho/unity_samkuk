using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Heroes;
using Samkuk.Meta;
using Samkuk.Player;
using Samkuk.Stages;
using Samkuk.UI;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    public class MetaTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string MetaCatalogPath = "Assets/ScriptableObjects/MetaCatalog.asset";

        string savePath;
        GameObject playerGo;
        PlayerStats stats;
        PlayerHealth health;
        PlayerExperience exp;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            // 실제 저장 파일을 건드리지 않도록 임시 경로를 쓴다
            savePath = Path.Combine(Application.temporaryCachePath, $"test_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();

            playerGo = new GameObject("TestPlayer");
            stats = playerGo.AddComponent<PlayerStats>();
            health = playerGo.AddComponent<PlayerHealth>();
            exp = playerGo.AddComponent<PlayerExperience>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");

            if (playerGo != null) UnityEngine.Object.Destroy(playerGo);
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        MetaUpgradeData MakeMeta(string name, MetaStat stat, float perLevel = 0.1f, int maxLevel = 5,
            int baseCost = 100, float growth = 2f)
        {
            var u = ScriptableObject.CreateInstance<MetaUpgradeData>();
            u.name = name; u.displayName = name; u.stat = stat; u.valuePerLevel = perLevel;
            u.maxLevel = maxLevel; u.baseCost = baseCost; u.costGrowth = growth;
            toDestroy.Add(u);
            return u;
        }

        MetaCatalog MakeCatalog(params MetaUpgradeData[] upgrades)
        {
            var c = ScriptableObject.CreateInstance<MetaCatalog>();
            c.upgrades.AddRange(upgrades);
            toDestroy.Add(c);
            return c;
        }

        // ───────────────────────── 저장 ─────────────────────────

        [Test]
        public void Save_RoundTrip_PreservesAllFields()
        {
            var data = new SaveData { gold = 345, totalRuns = 7, clears = 2, bestSeconds = 61.5f, bestKills = 123, lastHero = "관우" };
            data.SetUpgradeLevel("Meta_Health", 3);
            data.SetUpgradeLevel("Meta_Might", 1);

            Assert.IsTrue(SaveSystem.Save(data));
            SaveSystem.ResetCache();
            var loaded = SaveSystem.Load();

            Assert.AreEqual(345, loaded.gold);
            Assert.AreEqual(7, loaded.totalRuns);
            Assert.AreEqual(2, loaded.clears);
            Assert.AreEqual(61.5f, loaded.bestSeconds, 0.001f);
            Assert.AreEqual(123, loaded.bestKills);
            Assert.AreEqual("관우", loaded.lastHero);
            Assert.AreEqual(3, loaded.GetUpgradeLevel("Meta_Health"));
            Assert.AreEqual(1, loaded.GetUpgradeLevel("Meta_Might"));
            Assert.AreEqual(0, loaded.GetUpgradeLevel("없는 강화"));
        }

        [Test]
        public void Save_MissingFile_StartsWithFreshData()
        {
            Assert.IsFalse(File.Exists(savePath));

            var loaded = SaveSystem.Load();

            Assert.AreEqual(0, loaded.gold);
            Assert.AreEqual(0, loaded.totalRuns);
            Assert.AreEqual(0, loaded.upgrades.Count);
        }

        [Test]
        public void Save_CorruptFile_FallsBackToFreshData_WithoutThrowing()
        {
            File.WriteAllText(savePath, "{ 이건 JSON 이 아니다 ::: ");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                SaveData loaded = null;
                Assert.DoesNotThrow(() => loaded = SaveSystem.Load());
                Assert.AreEqual(0, loaded.gold);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Save_Sanitizes_NegativeOrBrokenValues()
        {
            File.WriteAllText(savePath,
                "{\"gold\":-50,\"totalRuns\":-1,\"bestKills\":-9,\"bestSeconds\":-3," +
                "\"upgrades\":[{\"id\":\"A\",\"level\":-2},{\"id\":\"\",\"level\":3}]}");

            var loaded = SaveSystem.Load();

            Assert.AreEqual(0, loaded.gold);
            Assert.AreEqual(0, loaded.totalRuns);
            Assert.AreEqual(0, loaded.bestKills);
            Assert.AreEqual(0f, loaded.bestSeconds);
            Assert.AreEqual(1, loaded.upgrades.Count, "id 없는 항목은 제거");
            Assert.AreEqual(0, loaded.GetUpgradeLevel("A"), "음수 레벨은 0으로 보정");
        }

        [Test]
        public void Save_Overwrites_AndLeavesNoTempFile()
        {
            SaveSystem.Save(new SaveData { gold = 10 });
            SaveSystem.Save(new SaveData { gold = 99 });

            Assert.IsFalse(File.Exists(savePath + ".tmp"), "임시 파일이 남지 않음");
            SaveSystem.ResetCache();
            Assert.AreEqual(99, SaveSystem.Load().gold);
        }

        [Test]
        public void Save_Current_IsCached_AndDeleteClearsEverything()
        {
            var a = SaveSystem.Current;
            var b = SaveSystem.Current;
            Assert.AreSame(a, b, "같은 인스턴스를 재사용");

            a.gold = 500;
            Assert.IsTrue(SaveSystem.SaveCurrent());
            Assert.IsTrue(File.Exists(savePath));

            SaveSystem.Delete();
            Assert.IsFalse(File.Exists(savePath));
            Assert.AreEqual(0, SaveSystem.Current.gold, "삭제 후에는 새 데이터");
        }

        // ───────────────────────── 영구 강화 ─────────────────────────

        [Test]
        public void Meta_CostGrowsPerLevel()
        {
            var u = MakeMeta("U", MetaStat.Damage, baseCost: 100, growth: 2f);
            Assert.AreEqual(100, u.CostForLevel(0));
            Assert.AreEqual(200, u.CostForLevel(1));
            Assert.AreEqual(400, u.CostForLevel(2));
            Assert.AreEqual(100, u.CostForLevel(-5), "음수 레벨은 0레벨로 취급");
        }

        [Test]
        public void Meta_Purchase_SpendsGold_AndRaisesLevel()
        {
            var u = MakeMeta("Meta_Might", MetaStat.Damage, baseCost: 100, growth: 2f);
            var save = new SaveData { gold = 350 };

            Assert.IsTrue(MetaProgression.CanPurchase(save, u));
            Assert.IsTrue(MetaProgression.TryPurchase(save, u));    // 100
            Assert.AreEqual(250, save.gold);
            Assert.AreEqual(1, MetaProgression.GetLevel(save, u));

            Assert.IsTrue(MetaProgression.TryPurchase(save, u));    // 200
            Assert.AreEqual(50, save.gold);
            Assert.AreEqual(2, MetaProgression.GetLevel(save, u));

            Assert.IsFalse(MetaProgression.CanPurchase(save, u), "다음 비용 400 > 보유 50");
            Assert.IsFalse(MetaProgression.TryPurchase(save, u));
            Assert.AreEqual(50, save.gold, "실패하면 골드가 줄지 않음");
            Assert.AreEqual(2, MetaProgression.GetLevel(save, u));
        }

        [Test]
        public void Meta_Purchase_StopsAtMaxLevel()
        {
            var u = MakeMeta("U", MetaStat.Damage, maxLevel: 2, baseCost: 10, growth: 1f);
            var save = new SaveData { gold = 1000 };

            Assert.IsTrue(MetaProgression.TryPurchase(save, u));
            Assert.IsTrue(MetaProgression.TryPurchase(save, u));
            Assert.IsTrue(MetaProgression.IsMaxLevel(save, u));
            Assert.AreEqual(-1, MetaProgression.NextCost(save, u));
            Assert.IsFalse(MetaProgression.TryPurchase(save, u), "최대 레벨 초과 구매 불가");
            Assert.AreEqual(980, save.gold);
        }

        [Test]
        public void Meta_Purchase_HandlesNulls()
        {
            var u = MakeMeta("U", MetaStat.Damage);
            Assert.IsFalse(MetaProgression.CanPurchase(null, u));
            Assert.IsFalse(MetaProgression.CanPurchase(new SaveData { gold = 999 }, null));
        }

        [Test]
        public void Meta_Level_IsClampedToMax_WhenSaveHasTooHighValue()
        {
            var u = MakeMeta("U", MetaStat.Damage, maxLevel: 3);
            var save = new SaveData();
            save.SetUpgradeLevel("U", 99);

            Assert.AreEqual(3, MetaProgression.GetLevel(save, u));
        }

        [Test]
        public void Meta_ComputeBonuses_SumsLevelTimesValuePerStat()
        {
            var hp = MakeMeta("hp", MetaStat.MaxHp, perLevel: 10f);
            var dmg = MakeMeta("dmg", MetaStat.Damage, perLevel: 0.04f);
            var dmg2 = MakeMeta("dmg2", MetaStat.Damage, perLevel: 0.1f);
            var gold = MakeMeta("gold", MetaStat.GoldGain, perLevel: 0.1f);
            var catalog = MakeCatalog(hp, dmg, dmg2, gold);
            var save = new SaveData();
            save.SetUpgradeLevel("hp", 3);
            save.SetUpgradeLevel("dmg", 2);
            save.SetUpgradeLevel("dmg2", 1);

            var b = MetaProgression.ComputeBonuses(catalog, save);

            Assert.AreEqual(30f, b.maxHp, 0.0001f);
            Assert.AreEqual(0.18f, b.damage, 0.0001f, "같은 스탯은 합산");
            Assert.AreEqual(0f, b.goldGain, 0.0001f, "구매하지 않은 강화는 0");
            Assert.AreEqual(0f, b.moveSpeed);
            Assert.AreEqual(default(MetaBonuses), MetaProgression.ComputeBonuses(null, save));
            Assert.AreEqual(default(MetaBonuses), MetaProgression.ComputeBonuses(catalog, null));
        }

        // ───────────────────────── 런 보상 ─────────────────────────

        [Test]
        public void RunRewards_Formula()
        {
            // 100킬 × 0.5 + 60초 + 클리어 50 = 160
            Assert.AreEqual(160, RunRewards.Calculate(100, 60f, true));
            Assert.AreEqual(110, RunRewards.Calculate(100, 60f, false));
            Assert.AreEqual(0, RunRewards.Calculate(0, 0f, false));
            Assert.AreEqual(240, RunRewards.Calculate(100, 60f, true, 1.5f), "골드 배율 적용");
            Assert.AreEqual(25, RunRewards.Calculate(1, 24.9f, false), "소수점 버림 (0.5 + 24.9 = 25.4)");
            Assert.AreEqual(0, RunRewards.Calculate(-5, -3f, false), "음수 입력 방어");
        }

        // ───────────────────────── 능력치 적용 ─────────────────────────

        [Test]
        public void Stats_ApplyMeta_ScalesStats_AndStacksWithOtherLayers()
        {
            var hero = ScriptableObject.CreateInstance<HeroData>();
            hero.damageMultiplier = 1.2f; toDestroy.Add(hero);
            stats.ApplyHero(hero);

            stats.ApplyMeta(new MetaBonuses
            {
                maxHp = 30f, damage = 0.1f, moveSpeed = 0.06f, regen = 0.3f, pickupRadius = 0.2f, expGain = 0.1f
            });

            Assert.AreEqual(1.2f * 1.1f, stats.DamageMultiplier, 0.0001f, "장수 × 메타");
            Assert.AreEqual(1.06f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(1.2f, stats.PickupRadiusMultiplier, 0.0001f);
            Assert.AreEqual(1.1f, stats.ExpMultiplier, 0.0001f);
            Assert.AreEqual(0.3f, stats.RegenPerSecond, 0.0001f);
            Assert.AreEqual(130f, health.Max, 0.001f);
            Assert.AreEqual(130f, health.Current, 0.001f, "최대 체력 증가분만큼 회복");

            stats.ApplyMeta(default);
            Assert.AreEqual(1.2f, stats.DamageMultiplier, 0.0001f, "메타만 제거되고 장수 보정은 유지");
            Assert.AreEqual(100f, health.Max, 0.001f);
        }

        [UnityTest]
        public IEnumerator MetaApplier_AppliesSavedUpgradesAtStart()
        {
            var hp = MakeMeta("Meta_Health", MetaStat.MaxHp, perLevel: 10f);
            var might = MakeMeta("Meta_Might", MetaStat.Damage, perLevel: 0.05f);
            var fortune = MakeMeta("Meta_Fortune", MetaStat.GoldGain, perLevel: 0.1f);
            var save = SaveSystem.Current;
            save.SetUpgradeLevel("Meta_Health", 2);
            save.SetUpgradeLevel("Meta_Might", 3);
            save.SetUpgradeLevel("Meta_Fortune", 1);

            var go = new GameObject("TestMetaApplier");
            toDestroy.Add(go);
            go.SetActive(false);
            var applier = go.AddComponent<MetaApplier>();
            applier.Catalog = MakeCatalog(hp, might, fortune);
            applier.Stats = stats;
            go.SetActive(true);

            yield return null; // Start

            Assert.AreEqual(1.15f, stats.DamageMultiplier, 0.0001f);
            Assert.AreEqual(120f, health.Max, 0.001f);
            Assert.AreEqual(0.1f, applier.Bonuses.goldGain, 0.0001f);
        }

        // ───────────────────────── 런 통계 ─────────────────────────

        [UnityTest]
        public IEnumerator RunStats_CountsKills_AndReadsStageTimeAndLevel()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");

            var sys = new GameObject("TestSystems");
            toDestroy.Add(sys);
            sys.SetActive(false);
            var manager = sys.AddComponent<EnemyManager>();
            var spawner = sys.AddComponent<EnemySpawner>();
            var stage = sys.AddComponent<StageController>();
            var run = sys.AddComponent<RunStats>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager; spawner.EnemyPrefab = enemyPrefab; spawner.autoSpawn = false;
            run.Stage = stage; run.Experience = exp;
            sys.SetActive(true);

            var stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.duration = 100f; toDestroy.Add(stageData);
            stage.Stage = stageData;
            stage.Spawner = spawner;

            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.maxHp = 5; enemy.moveSpeed = 0f; toDestroy.Add(enemy);
            yield return null;

            for (int i = 0; i < 4; i++) spawner.SpawnAt(enemy, new Vector2(8f + i, 0f)).TakeDamage(100f);
            spawner.SpawnAt(enemy, new Vector2(8f, 3f)); // 살려 둔 적은 세지 않음
            stage.Tick(12.5f);
            exp.AddExp(7); // 레벨 2

            Assert.AreEqual(4, run.Kills);
            Assert.AreEqual(12.5f, run.Seconds, 0.3f, "스테이지 경과 시간 (프레임 갱신분 오차 허용)");
            Assert.AreEqual(2, run.Level);
        }

        [UnityTest]
        public IEnumerator RunStats_WithoutStage_CountsItsOwnTime()
        {
            var go = new GameObject("TestRunStats");
            toDestroy.Add(go);
            var run = go.AddComponent<RunStats>();

            yield return new WaitForSeconds(0.4f);

            Assert.Greater(run.Seconds, 0.25f);
            Assert.AreEqual(1, run.Level, "경험치 컴포넌트가 없으면 1레벨");
        }

        // ───────────────────────── 게임 매니저 / 결과 ─────────────────────────

        class FakeResultView : IResultView
        {
            public int ShowCount;
            public RunResult Last;
            public Action Retry, Title;
            public bool IsVisible { get; private set; }
            public void Show(RunResult result, Action onRetry, Action onTitle)
            {
                ShowCount++; Last = result; Retry = onRetry; Title = onTitle; IsVisible = true;
            }
            public void Hide() { IsVisible = false; }
        }

        GameManager MakeGameManager(StageController stage = null)
        {
            var go = new GameObject("TestGM");
            toDestroy.Add(go);
            go.SetActive(false);
            var gm = go.AddComponent<GameManager>();
            gm.PlayerHealth = health;
            gm.Stage = stage;
            go.SetActive(true);
            return gm;
        }

        [Test]
        public void GameManager_FiresFinishedOnce_WithClearedFlag()
        {
            var results = new List<bool>();

            var gm = MakeGameManager();
            gm.Finished += c => results.Add(c);
            health.TakeDamage(1000f);
            health.TakeDamage(1000f);

            CollectionAssert.AreEqual(new[] { false }, results, "사망 시 한 번만, 패배");
            Assert.IsTrue(gm.IsGameOver);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void GameManager_ReportsClear()
        {
            var sys = new GameObject("TestSys");
            toDestroy.Add(sys);
            var stage = sys.AddComponent<StageController>();
            var data = ScriptableObject.CreateInstance<StageData>();
            data.duration = 5f; toDestroy.Add(data);
            stage.Stage = data;

            var results = new List<bool>();
            var gm = MakeGameManager(stage);
            gm.Finished += c => results.Add(c);

            stage.Tick(6f);

            CollectionAssert.AreEqual(new[] { true }, results);
            Assert.IsTrue(gm.IsCleared);
        }

        ResultController MakeResultController(GameManager gm, RunStats run, FakeResultView view, MetaApplier meta = null)
        {
            var go = new GameObject("TestResult");
            toDestroy.Add(go);
            var rc = go.AddComponent<ResultController>();
            rc.Game = gm;
            rc.Run = run;
            rc.Meta = meta;
            rc.View = view;
            return rc;
        }

        RunStats MakeRun(int kills, float seconds, int level)
        {
            // 처치/시간/레벨을 직접 세팅하기 위해 스테이지 + 경험치를 구성한다
            var sys = new GameObject("TestRunSys");
            toDestroy.Add(sys);
            var stage = sys.AddComponent<StageController>();
            var data = ScriptableObject.CreateInstance<StageData>();
            data.duration = 1000f; toDestroy.Add(data);
            stage.Stage = data;
            stage.Tick(seconds);

            var run = sys.AddComponent<RunStats>();
            run.Stage = stage;
            run.Experience = exp;
            for (int i = 0; i < kills; i++) RunStatsKill(run);
            while (exp.Level < level) exp.AddExp(exp.ToNext);
            return run;
        }

        /// <summary>적 사망 이벤트를 흉내내기 위해 실제 적을 하나 처치한다.</summary>
        void RunStatsKill(RunStats run)
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            var sys = new GameObject("KillSys");
            toDestroy.Add(sys);
            sys.SetActive(false);
            var manager = sys.AddComponent<EnemyManager>();
            var spawner = sys.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager; spawner.EnemyPrefab = enemyPrefab; spawner.autoSpawn = false;
            sys.SetActive(true);

            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = 1; d.moveSpeed = 0f; toDestroy.Add(d);
            spawner.SpawnAt(d, new Vector2(20f, 0f)).TakeDamage(10f);
        }

        [Test]
        public void Result_Defeat_SavesGoldAndRecords_AndShowsView()
        {
            var run = MakeRun(kills: 20, seconds: 30f, level: 3);
            var gm = MakeGameManager();
            var view = new FakeResultView();
            var rc = MakeResultController(gm, run, view);

            health.TakeDamage(1000f);

            Assert.AreEqual(1, view.ShowCount);
            var r = view.Last;
            Assert.IsFalse(r.cleared);
            Assert.AreEqual(20, r.kills);
            Assert.AreEqual(30f, r.seconds, 0.001f);
            Assert.AreEqual(3, r.level);
            Assert.AreEqual(40, r.goldEarned, "20 × 0.5 + 30 = 40");
            Assert.AreEqual(40, r.totalGold);
            Assert.IsTrue(r.newBestTime);
            Assert.IsTrue(r.newBestKills);

            SaveSystem.ResetCache();
            var saved = SaveSystem.Load();
            Assert.AreEqual(40, saved.gold, "파일에 저장됨");
            Assert.AreEqual(1, saved.totalRuns);
            Assert.AreEqual(0, saved.clears);
            Assert.AreEqual(30f, saved.bestSeconds, 0.001f);
            Assert.AreEqual(20, saved.bestKills);
            Assert.IsTrue(rc.LastResult.HasValue);
        }

        [Test]
        public void Result_Clear_AddsBonus_AndCountsClear()
        {
            var sys = new GameObject("TestSys");
            toDestroy.Add(sys);
            var stage = sys.AddComponent<StageController>();
            var data = ScriptableObject.CreateInstance<StageData>();
            data.duration = 60f; toDestroy.Add(data);
            stage.Stage = data;
            var run = sys.AddComponent<RunStats>();
            run.Stage = stage; run.Experience = exp;

            var gm = MakeGameManager(stage);
            var view = new FakeResultView();
            MakeResultController(gm, run, view);

            stage.Tick(61f);

            Assert.IsTrue(view.Last.cleared);
            Assert.AreEqual(60f, run.Seconds, 0.5f + 1f);
            Assert.AreEqual(RunRewards.Calculate(run.Kills, run.Seconds, true), view.Last.goldEarned);
            Assert.GreaterOrEqual(view.Last.goldEarned, RunRewards.ClearBonus);

            SaveSystem.ResetCache();
            Assert.AreEqual(1, SaveSystem.Load().clears);
        }

        [Test]
        public void Result_AccumulatesAcrossRuns_AndOnlyFlagsActualRecords()
        {
            var save = SaveSystem.Current;
            save.gold = 100; save.totalRuns = 4; save.bestSeconds = 50f; save.bestKills = 99;

            var run = MakeRun(kills: 10, seconds: 20f, level: 2);
            var gm = MakeGameManager();
            var view = new FakeResultView();
            MakeResultController(gm, run, view);

            health.TakeDamage(1000f);

            Assert.IsFalse(view.Last.newBestTime, "기존 기록(50초)보다 짧음");
            Assert.IsFalse(view.Last.newBestKills);
            Assert.AreEqual(25, view.Last.goldEarned, "10 × 0.5 + 20");
            Assert.AreEqual(125, view.Last.totalGold, "기존 골드에 누적");

            SaveSystem.ResetCache();
            var saved = SaveSystem.Load();
            Assert.AreEqual(5, saved.totalRuns);
            Assert.AreEqual(50f, saved.bestSeconds, 0.001f, "기록은 갱신되지 않음");
            Assert.AreEqual(99, saved.bestKills);
        }

        [Test]
        public void Result_GoldMultiplier_FromMetaIsApplied()
        {
            var fortune = MakeMeta("Meta_Fortune", MetaStat.GoldGain, perLevel: 0.5f);
            SaveSystem.Current.SetUpgradeLevel("Meta_Fortune", 1);
            var go = new GameObject("TestMeta");
            toDestroy.Add(go);
            var applier = go.AddComponent<MetaApplier>();
            applier.Catalog = MakeCatalog(fortune);
            applier.Stats = stats;
            applier.Apply();

            var run = MakeRun(kills: 20, seconds: 30f, level: 1);
            var gm = MakeGameManager();
            var view = new FakeResultView();
            MakeResultController(gm, run, view, applier);

            health.TakeDamage(1000f);

            Assert.AreEqual(60, view.Last.goldEarned, "40 × 1.5");
        }

        [Test]
        public void Result_ButtonCallbacks_AreProvided()
        {
            var run = MakeRun(0, 1f, 1);
            var gm = MakeGameManager();
            var view = new FakeResultView();
            MakeResultController(gm, run, view);

            health.TakeDamage(1000f);

            Assert.IsNotNull(view.Retry);
            Assert.IsNotNull(view.Title);
        }

        [Test]
        public void ResultUI_FormatsSummary_AndWiresButtons()
        {
            var text = ResultUI.Format(new RunResult
            {
                cleared = true, seconds = 65.4f, kills = 120, level = 7, goldEarned = 180, totalGold = 530,
                newBestTime = true, newBestKills = false, heroName = "여포"
            });
            StringAssert.Contains("여포", text);
            StringAssert.Contains("01:05", text);
            StringAssert.Contains("120", text);
            StringAssert.Contains("+180", text);
            StringAssert.Contains("530", text);
            StringAssert.Contains("최고 기록", text);
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(text, "최고 기록").Count,
                "갱신된 항목에만 표시");

            // 실제 UI 컴포넌트 연결
            var go = new GameObject("TestResultUI");
            toDestroy.Add(go);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform);
            var title = new GameObject("T", typeof(RectTransform)).AddComponent<Text>();
            var details = new GameObject("D", typeof(RectTransform)).AddComponent<Text>();
            var retry = new GameObject("R", typeof(RectTransform)).AddComponent<Button>();
            var toTitle = new GameObject("TT", typeof(RectTransform)).AddComponent<Button>();
            foreach (var c in new Component[] { title, details, retry, toTitle }) c.transform.SetParent(panel.transform);

            var ui = go.AddComponent<ResultUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("detailsLabel").objectReferenceValue = details;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("titleButton").objectReferenceValue = toTitle;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool retried = false, wentTitle = false;
            ui.Show(new RunResult { cleared = false, seconds = 10f, kills = 3, level = 1 }, () => retried = true, () => wentTitle = true);

            Assert.IsTrue(ui.IsVisible);
            Assert.AreEqual("게임 오버", title.text);
            StringAssert.Contains("00:10", details.text);

            retry.onClick.Invoke();
            toTitle.onClick.Invoke();
            Assert.IsTrue(retried);
            Assert.IsTrue(wentTitle);

            ui.Show(new RunResult { cleared = true }, null, null);
            Assert.AreEqual("스테이지 클리어!", title.text);
            Assert.DoesNotThrow(() => retry.onClick.Invoke(), "콜백이 null이어도 안전");

            ui.Hide();
            Assert.IsFalse(ui.IsVisible);
        }

        // ───────────────────────── 일시정지 ─────────────────────────

        class FakePauseView : IPauseView
        {
            public int ShowCount, HideCount;
            public Action Resume;
            public bool IsVisible { get; private set; }
            public void Show(Action onResume, Action onRestart, Action onTitle) { ShowCount++; Resume = onResume; IsVisible = true; }
            public void Hide() { HideCount++; IsVisible = false; }
        }

        PauseController MakePause(FakePauseView view, GameManager gm = null,
            HeroSelectController hero = null, LevelUpController levelUp = null)
        {
            var go = new GameObject("TestPause");
            toDestroy.Add(go);
            var pc = go.AddComponent<PauseController>();
            pc.Game = gm; pc.Hero = hero; pc.LevelUp = levelUp; pc.View = view;
            return pc;
        }

        [Test]
        public void Pause_StopsTime_AndResumeRestoresIt()
        {
            var view = new FakePauseView();
            var pc = MakePause(view);

            Assert.IsTrue(pc.CanPause);
            pc.Toggle();

            Assert.IsTrue(pc.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(view.IsVisible);
            Assert.IsFalse(pc.CanPause, "이미 정지 중");

            view.Resume(); // 화면의 '계속하기' 버튼
            Assert.IsFalse(pc.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(view.IsVisible);

            pc.Toggle();
            pc.Toggle();
            Assert.IsFalse(pc.IsPaused, "토글로도 계속하기");
            Assert.AreEqual(2, view.ShowCount);
        }

        [Test]
        public void Pause_NotAllowed_AfterGameEnds()
        {
            var gm = MakeGameManager();
            var view = new FakePauseView();
            var pc = MakePause(view, gm);

            health.TakeDamage(1000f);
            pc.Toggle();

            Assert.IsFalse(pc.IsPaused);
            Assert.AreEqual(0, view.ShowCount);
            Assert.AreEqual(0f, Time.timeScale, "게임 종료 상태의 정지는 그대로");
        }

        [Test]
        public void Pause_NotAllowed_DuringHeroSelectOrLevelUp()
        {
            // 장수 선택 중
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.displayName = "무기"; toDestroy.Add(weapon);
            var heroData = ScriptableObject.CreateInstance<HeroData>();
            heroData.startingWeapon = weapon; toDestroy.Add(heroData);
            var heroCatalog = ScriptableObject.CreateInstance<HeroCatalog>();
            heroCatalog.heroes.Add(heroData); toDestroy.Add(heroCatalog);

            var heroGo = new GameObject("TestHero");
            toDestroy.Add(heroGo);
            var hero = heroGo.AddComponent<HeroSelectController>();
            hero.Catalog = heroCatalog;
            hero.View = new FakeHeroView();
            hero.Begin();
            Assert.IsTrue(hero.IsSelecting);

            var view = new FakePauseView();
            var pc = MakePause(view, null, hero, null);
            Assert.IsFalse(pc.CanPause, "장수 선택 중에는 일시정지 불가");
            pc.Toggle();
            Assert.AreEqual(0, view.ShowCount);

            hero.Choose(0);
            Assert.IsTrue(pc.CanPause, "선택이 끝나면 가능");
        }

        class FakeHeroView : IHeroSelectView
        {
            public bool IsVisible { get; private set; }
            public void Show(IReadOnlyList<HeroData> heroes, Action<int> onChosen) { IsVisible = true; }
            public void Hide() { IsVisible = false; }
        }

        // ───────────────────────── 실제 에셋 ─────────────────────────

        [Test]
        public void MetaCatalogAsset_IsWellFormed()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MetaCatalog>(MetaCatalogPath);
            Assert.IsNotNull(catalog, "MetaCatalog 가 없습니다. Samkuk > Step 9-1 를 먼저 실행하세요.");
            Assert.GreaterOrEqual(catalog.upgrades.Count, 5, "영구 강화 5종 이상");

            var keys = new HashSet<string>();
            var stats = new HashSet<MetaStat>();
            foreach (var u in catalog.upgrades)
            {
                Assert.IsNotNull(u);
                Assert.IsTrue(keys.Add(MetaProgression.Key(u)), $"저장 키 중복: {u.name}");
                stats.Add(u.stat);
                Assert.IsFalse(string.IsNullOrEmpty(u.displayName));
                Assert.IsFalse(string.IsNullOrEmpty(u.description), $"{u.displayName}: 설명");
                Assert.Greater(u.valuePerLevel, 0f, $"{u.displayName}: 효과량");
                Assert.Greater(u.maxLevel, 0);
                Assert.Greater(u.baseCost, 0);
                Assert.GreaterOrEqual(u.costGrowth, 1f, $"{u.displayName}: 비용이 줄어들면 안 됨");
                Assert.LessOrEqual(u.baseCost, 300, $"{u.displayName}: 첫 구매가 한두 판 안에 가능해야 함");
            }
            Assert.GreaterOrEqual(stats.Count, 5, "서로 다른 스탯 5종 이상");
        }
    }
}
