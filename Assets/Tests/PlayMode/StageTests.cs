using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Stages;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class StageTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string StageAssetPath = "Assets/ScriptableObjects/Stage/Stage_YellowTurban.asset";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        StageController stage;
        PlayerHealth health;
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            health = playerGo.AddComponent<PlayerHealth>();

            camGo = new GameObject("TestCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.aspect = 16f / 9f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            root = new GameObject("TestSystems");
            root.SetActive(false);
            manager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            stage = root.AddComponent<StageController>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = enemyPrefab;
            spawner.autoSpawn = false;
            stage.Spawner = spawner;
            stage.Health = health;
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (root != null) Object.Destroy(root);
            if (playerGo != null) Object.Destroy(playerGo);
            if (camGo != null) Object.Destroy(camGo);
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
        }

        EnemyData MakeEnemy(string name, int hp = 10, float speed = 0f, float scale = 1f)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.displayName = name;
            d.maxHp = hp; d.moveSpeed = speed; d.contactDamage = 0; d.scale = scale; d.colliderRadius = 0.4f;
            toDestroy.Add(d);
            return d;
        }

        StageData MakeStage(float duration, EnemyData a, EnemyData b, EnemyData elite = null, EnemyData boss = null)
        {
            var s = ScriptableObject.CreateInstance<StageData>();
            s.duration = duration;
            s.waves.Add(new Wave
            {
                name = "W1", startTime = 0f, spawnPerSecond = 2f, maxAlive = 50,
                enemies = new List<WaveEnemy> { new WaveEnemy { data = a, weight = 1f } }
            });
            s.waves.Add(new Wave
            {
                name = "W2", startTime = 10f, spawnPerSecond = 7f, maxAlive = 99,
                enemies = new List<WaveEnemy> { new WaveEnemy { data = b, weight = 2f }, new WaveEnemy { data = a, weight = 1f } }
            });
            if (elite != null)
                s.events.Add(new StageEvent { time = 5f, enemy = elite, count = 2, isBoss = false, message = "엘리트!" });
            if (boss != null)
                s.events.Add(new StageEvent { time = 12f, enemy = boss, count = 1, isBoss = true, message = "보스!" });
            toDestroy.Add(s);
            return s;
        }

        // ───────────────────────── 웨이브 ─────────────────────────

        [Test]
        public void Wave_AppliesFirstWaveAtStart()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(60f, a, b);

            stage.Tick(0f);

            Assert.AreEqual(1, stage.WaveNumber);
            Assert.AreEqual("W1", stage.WaveName);
            Assert.AreEqual(2f, spawner.SpawnPerSecond, 0.001f);
            Assert.AreEqual(50, spawner.MaxAlive);
            Assert.AreEqual(1, spawner.SpawnTable.Count);
            Assert.AreSame(a, spawner.SpawnTable[0].data);
        }

        [Test]
        public void Wave_ChangesAtStartTime_AndFiresEventOnce()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(60f, a, b);
            var changes = new List<int>();
            stage.WaveChanged += n => changes.Add(n);

            stage.Tick(0f);
            stage.Tick(9.9f);
            Assert.AreEqual(1, stage.WaveNumber, "10초 전에는 웨이브 1");

            stage.Tick(0.2f);
            stage.Tick(5f);

            Assert.AreEqual(2, stage.WaveNumber);
            Assert.AreEqual(7f, spawner.SpawnPerSecond, 0.001f);
            Assert.AreEqual(99, spawner.MaxAlive);
            Assert.AreEqual(2, spawner.SpawnTable.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, changes, "웨이브 전환마다 한 번씩만 알림");
        }

        [Test]
        public void Wave_BigTimeJump_SkipsToLatestWave()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(60f, a, b);

            stage.Tick(30f);

            Assert.AreEqual(2, stage.WaveNumber);
        }

        // ───────────────────────── 이벤트 ─────────────────────────

        [Test]
        public void Event_Elite_SpawnsCountOutsideView_WithoutBossSignal()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B"); var elite = MakeEnemy("E");
            stage.Stage = MakeStage(60f, a, b, elite);
            var messages = new List<string>();
            int bossSignals = 0;
            stage.EventTriggered += e => messages.Add(e.message);
            stage.BossSpawned += _ => bossSignals++;

            stage.Tick(4.9f);
            Assert.AreEqual(0, manager.Count);

            stage.Tick(0.2f);

            Assert.AreEqual(2, manager.Count, "엘리트 2기 출현");
            CollectionAssert.AreEqual(new[] { "엘리트!" }, messages);
            Assert.AreEqual(0, bossSignals);
            Assert.IsNull(stage.Boss);
        }

        [Test]
        public void Event_Boss_SpawnsOnce_AndIsReported()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B"); var boss = MakeEnemy("보스", hp: 500, scale: 2.4f);
            stage.Stage = MakeStage(60f, a, b, null, boss);
            Enemy reported = null;
            stage.BossSpawned += e => reported = e;

            stage.Tick(12.1f);
            stage.Tick(1f);
            stage.Tick(1f);

            Assert.IsNotNull(reported);
            Assert.AreSame(reported, stage.Boss);
            Assert.AreSame(boss, reported.Data);
            Assert.AreEqual(1, manager.Count, "보스 이벤트는 한 번만");
        }

        [Test]
        public void Events_AreProcessedInTimeOrder_OnBigJump()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B"); var elite = MakeEnemy("E"); var boss = MakeEnemy("B");
            stage.Stage = MakeStage(60f, a, b, elite, boss);
            var order = new List<string>();
            stage.EventTriggered += e => order.Add(e.message);

            stage.Tick(20f);

            CollectionAssert.AreEqual(new[] { "엘리트!", "보스!" }, order);
        }

        // ───────────────────────── 클리어/정지 ─────────────────────────

        [Test]
        public void Clear_FiresOnce_AtDuration_AndStopsSpawning()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(20f, a, b);
            spawner.autoSpawn = true;
            int cleared = 0;
            stage.Cleared += () => cleared++;

            stage.Tick(19f);
            Assert.IsFalse(stage.IsCleared);
            Assert.AreEqual(1f, stage.Remaining, 0.001f);

            stage.Tick(1f);
            stage.Tick(5f);

            Assert.IsTrue(stage.IsCleared);
            Assert.AreEqual(1, cleared);
            Assert.IsFalse(spawner.autoSpawn, "클리어 후에는 스폰 중지");
            Assert.AreEqual(0f, stage.Remaining);
        }

        [Test]
        public void Timer_StopsWhenPlayerIsDead()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(60f, a, b);
            stage.Tick(5f);

            health.TakeDamage(1000f);
            stage.Tick(30f);

            Assert.AreEqual(5f, stage.Elapsed, 0.001f);
            Assert.IsFalse(stage.IsCleared);
        }

        [Test]
        public void NoStage_DoesNothing()
        {
            stage.Stage = null;
            Assert.DoesNotThrow(() => stage.Tick(10f));
            Assert.AreEqual(0, stage.WaveNumber);
        }

        [UnityTest]
        public IEnumerator Timer_AdvancesWithGameTime_NotWhilePaused()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(60f, a, b);
            yield return null;

            yield return new WaitForSeconds(0.5f);
            float running = stage.Elapsed;
            Assert.Greater(running, 0.3f);

            Time.timeScale = 0f; // 레벨업 선택 중 등
            float frozen = stage.Elapsed;
            yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = 1f;

            Assert.AreEqual(frozen, stage.Elapsed, 0.05f, "일시정지 중에는 시간이 흐르지 않음");
        }

        // ───────────────────────── 게임 매니저 ─────────────────────────

        [Test]
        public void GameManager_ShowsClearPanel_AndPauses_OnStageCleared()
        {
            var a = MakeEnemy("A"); var b = MakeEnemy("B");
            stage.Stage = MakeStage(5f, a, b);

            var panel = new GameObject("ClearPanel");
            toDestroy.Add(panel);
            var gmGo = new GameObject("TestGM");
            toDestroy.Add(gmGo);
            gmGo.SetActive(false);
            var gm = gmGo.AddComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("clearPanel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
            gmGo.SetActive(true);

            Assert.IsFalse(panel.activeSelf, "시작 시에는 숨김");

            stage.Tick(6f);

            Assert.IsTrue(gm.IsCleared);
            Assert.IsTrue(panel.activeSelf);
            Assert.AreEqual(0f, Time.timeScale);
        }

        // ───────────────────────── 돌진 패턴/큰 적 ─────────────────────────

        [UnityTest]
        public IEnumerator Charge_Pattern_WindsUpThenDashesFasterThanNormal()
        {
            yield return null;
            var d = MakeEnemy("돌진", hp: 100000, speed: 2f);
            d.chargeInterval = 1f; d.chargeWindup = 0.3f; d.chargeDuration = 0.5f; d.chargeSpeedMultiplier = 4f;
            var e = spawner.SpawnAt(d, new Vector2(15f, 0f));

            bool sawWindup = false, sawCharge = false;
            float maxSpeed = 0f, speedDuringWindup = float.MaxValue;
            float t = 0f;
            while (t < 3f)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                float speed = e.Body.linearVelocity.magnitude;
                if (e.IsWindingUp) { sawWindup = true; speedDuringWindup = Mathf.Min(speedDuringWindup, speed); }
                if (e.IsCharging) { sawCharge = true; maxSpeed = Mathf.Max(maxSpeed, speed); }
            }

            Assert.IsTrue(sawWindup, "돌진 전 예고 단계가 있어야 함");
            Assert.IsTrue(sawCharge, "돌진 단계가 있어야 함");
            Assert.Less(speedDuringWindup, 0.5f, "예고 중에는 멈춰 있음");
            Assert.Greater(maxSpeed, 6f, $"돌진 속도 = 2 × 4 = 8 (관측 {maxSpeed})");
        }

        [UnityTest]
        public IEnumerator NoChargeInterval_NeverCharges()
        {
            yield return null;
            var d = MakeEnemy("일반", hp: 100000, speed: 2f);
            var e = spawner.SpawnAt(d, new Vector2(15f, 0f));

            bool charged = false;
            float t = 0f;
            while (t < 2f)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                if (e.IsCharging || e.IsWindingUp) charged = true;
            }
            Assert.IsFalse(charged);
        }

        [UnityTest]
        public IEnumerator BigEnemy_IsFoundByOverlapCircle_NearItsEdge()
        {
            yield return null;
            var boss = MakeEnemy("보스", hp: 500, scale: 2.5f); // 반지름 1.0
            spawner.SpawnAt(boss, new Vector2(4.4f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var results = new List<Enemy>();
            manager.OverlapCircle(new Vector2(5.4f, 0f), 0.05f, results); // 중심 거리 1.0 ≤ 반지름 + 0.05

            Assert.AreEqual(1, results.Count, "큰 적의 가장자리에 닿은 투사체도 명중해야 함");
        }

        // ───────────────────────── 실제 스테이지 에셋 ─────────────────────────

        [Test]
        public void StageAsset_IsWellFormed()
        {
            var data = AssetDatabase.LoadAssetAtPath<StageData>(StageAssetPath);
            Assert.IsNotNull(data, "스테이지 에셋이 없습니다. Samkuk > Step 7 를 먼저 실행하세요.");

            Assert.AreEqual(60f, data.duration, 0.001f, "클리어 시간은 1분");
            Assert.GreaterOrEqual(data.waves.Count, 3);
            Assert.AreEqual(0f, data.waves[0].startTime, "첫 웨이브는 시작과 동시에");

            float prev = -1f;
            foreach (var w in data.waves)
            {
                Assert.Greater(w.startTime, prev, "웨이브 시작 시간은 증가해야 함");
                prev = w.startTime;
                Assert.Less(w.startTime, data.duration);
                Assert.Greater(w.enemies.Count, 0);
                foreach (var e in w.enemies)
                {
                    Assert.IsNotNull(e.data, $"{w.name}: 적 데이터 누락");
                    Assert.Greater(e.weight, 0f);
                }
            }

            int bosses = 0;
            foreach (var ev in data.events)
            {
                Assert.IsNotNull(ev.enemy, "이벤트 적 데이터 누락");
                Assert.Less(ev.time, data.duration, "이벤트는 클리어 전에 발생해야 함");
                if (ev.isBoss) bosses++;
            }
            Assert.AreEqual(1, bosses, "보스 이벤트는 정확히 하나");
        }
    }
}
