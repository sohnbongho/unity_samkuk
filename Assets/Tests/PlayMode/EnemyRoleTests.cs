using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class EnemyRoleTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/EnemyProjectile.prefab";
        const string EnemyDir = "Assets/ScriptableObjects/Enemies";
        const string StagePath = "Assets/ScriptableObjects/Stage/Stage_YellowTurban.asset";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        EnemyProjectileSystem projectiles;
        PlayerHealth health;
        PlayerStats stats;
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<EnemyProjectile>();
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");
            Assert.IsNotNull(projectilePrefab, "EnemyProjectile 프리팹이 없습니다. Samkuk > Step 8-3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            stats = playerGo.AddComponent<PlayerStats>();
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
            projectiles = root.AddComponent<EnemyProjectileSystem>();
            manager.Target = playerGo.transform;
            manager.Projectiles = projectiles;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = enemyPrefab;
            spawner.autoSpawn = false;
            projectiles.Prefab = projectilePrefab;
            projectiles.Target = playerGo.transform;
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

        // ───────────────────────── 헬퍼 ─────────────────────────

        EnemyData Archer(float speed = 3f, float range = 5f, float fireInterval = 0.6f,
            int damage = 5, float projSpeed = 12f, float lifetime = 3f, int hp = 1000)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.role = EnemyRole.Archer;
            d.maxHp = hp; d.moveSpeed = speed; d.contactDamage = 0; d.colliderRadius = 0.3f;
            d.attackRange = range; d.fireInterval = fireInterval;
            d.projectileSpeed = projSpeed; d.projectileDamage = damage; d.projectileLifetime = lifetime;
            d.projectileSize = 0.2f;
            toDestroy.Add(d);
            return d;
        }

        EnemyData Melee(float speed = 2f)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = 1000; d.moveSpeed = speed; d.contactDamage = 0; d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        float DistToPlayer(Enemy e) => Vector2.Distance(e.Position, playerGo.transform.position);

        // ───────────────────────── 궁병 이동 ─────────────────────────

        [UnityTest]
        public IEnumerator Archer_ApproachesToPreferredRange_ThenHoldsPosition()
        {
            yield return null;
            var e = spawner.SpawnAt(Archer(speed: 3f, range: 5f, fireInterval: 100f), new Vector2(12f, 0f));

            yield return new WaitForSeconds(3.5f);
            float d1 = DistToPlayer(e);
            yield return new WaitForSeconds(0.6f);
            float d2 = DistToPlayer(e);

            Assert.AreEqual(5f, d1, 0.8f, $"사정거리 근처까지 접근 (거리 {d1})");
            Assert.AreEqual(d1, d2, 0.15f, "사정거리에 도달하면 멈춤");
            Assert.Greater(d1, 3.2f, "근접 적처럼 달라붙지 않음");
        }

        [UnityTest]
        public IEnumerator Archer_RetreatsWhenPlayerIsTooClose()
        {
            yield return null;
            var e = spawner.SpawnAt(Archer(speed: 3f, range: 5f, fireInterval: 100f), new Vector2(1.5f, 0f));
            float before = DistToPlayer(e);

            yield return new WaitForSeconds(1f);

            Assert.Greater(DistToPlayer(e), before + 1.2f, "너무 가까우면 거리를 벌림");
        }

        [UnityTest]
        public IEnumerator MeleeEnemy_StillChasesToContact_NeverShoots()
        {
            yield return null;
            var e = spawner.SpawnAt(Melee(speed: 3f), new Vector2(6f, 0f));

            yield return new WaitForSeconds(3f);

            Assert.Less(DistToPlayer(e), 1.1f, "근접 적은 붙을 때까지 접근");
            Assert.AreEqual(0, projectiles.FiredCount);
        }

        // ───────────────────────── 궁병 사격 ─────────────────────────

        [UnityTest]
        public IEnumerator Archer_FiresRepeatedly_AtItsInterval()
        {
            yield return null;
            spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 0.5f, projSpeed: 1f, lifetime: 0.1f), new Vector2(4f, 0f));

            yield return new WaitForSeconds(2.3f);

            Assert.GreaterOrEqual(projectiles.FiredCount, 3, $"0.5초 간격으로 반복 발사 (발사 {projectiles.FiredCount})");
            Assert.LessOrEqual(projectiles.FiredCount, 5);
        }

        [UnityTest]
        public IEnumerator Archer_OutOfRange_DoesNotFire()
        {
            yield return null;
            spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 0.3f), new Vector2(20f, 0f));

            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(0, projectiles.FiredCount, "사정거리 밖에서는 쏘지 않음");
        }

        [UnityTest]
        public IEnumerator Archer_TelegraphsShot_ThenResetsColor()
        {
            yield return null;
            var data = Archer(speed: 0f, range: 5f, fireInterval: 1.0f, projSpeed: 1f, lifetime: 0.1f);
            data.tint = new Color(0.9f, 0.5f, 0.2f);
            var e = spawner.SpawnAt(data, new Vector2(4f, 0f));
            var sr = e.GetComponent<SpriteRenderer>();

            bool sawTelegraph = false;
            float t = 0f;
            while (t < 1.4f)
            {
                yield return null;
                t += Time.deltaTime;
                if (e.IsTelegraphingShot) sawTelegraph = true;
            }

            Assert.IsTrue(sawTelegraph, "발사 직전에 예고 상태가 있어야 함");
            Assert.GreaterOrEqual(projectiles.FiredCount, 1);
            yield return new WaitForSeconds(0.1f);
            if (!e.IsTelegraphingShot)
                Assert.AreEqual(data.tint, sr.color, "예고가 끝나면 원래 색");
        }

        [UnityTest]
        public IEnumerator Archer_Stunned_DoesNotFire_UntilStunEnds()
        {
            yield return null;
            var e = spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 0.3f, projSpeed: 1f, lifetime: 0.1f), new Vector2(4f, 0f));
            e.Stun(1.0f);

            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0, projectiles.FiredCount, "기절 중에는 쏘지 못함");

            yield return new WaitForSeconds(1.0f);
            Assert.GreaterOrEqual(projectiles.FiredCount, 1, "기절이 풀리면 다시 쏨");
        }

        // ───────────────────────── 투사체 ─────────────────────────

        [UnityTest]
        public IEnumerator Projectile_HitsPlayer_AndRespectsInvulnerabilityWindow()
        {
            yield return null;
            spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 0.3f, damage: 5, projSpeed: 14f), new Vector2(4f, 0f));

            yield return new WaitForSeconds(1.2f);

            Assert.Less(health.Current, 100f, "투사체에 맞아 피해");
            Assert.AreEqual(0f, (100f - health.Current) % 5f, 0.001f, "피해는 투사체 피해량(5)의 배수");
            Assert.GreaterOrEqual(projectiles.HitCount, 2, "여러 발이 명중");
            // 0.3초 간격으로 쏘지만 피격 후 무적(0.5초) 때문에 명중 횟수보다 적은 횟수만 피해
            int damageEvents = Mathf.RoundToInt((100f - health.Current) / 5f);
            Assert.Less(damageEvents, projectiles.HitCount, "무적 시간 중의 명중은 피해가 무시됨");
        }

        [UnityTest]
        public IEnumerator Projectile_Expires_WhenLifetimeEnds_WithoutHitting()
        {
            yield return null;
            var e = spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 100f, projSpeed: 2f, lifetime: 0.3f), new Vector2(4f, 0f));
            // 발사 간격이 길어 자동 발사는 없다 → 시스템을 직접 사용
            var p = projectiles.Fire(e.Position, Vector2.left, e.Data);
            Assert.IsNotNull(p);
            Assert.AreEqual(1, projectiles.ActiveCount);

            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(0, projectiles.ActiveCount, "수명이 다하면 사라짐");
            Assert.AreEqual(100f, health.Current, 0.001f, "도달 전에 사라졌으므로 피해 없음");
            Assert.AreEqual(0, projectiles.HitCount);
        }

        [UnityTest]
        public IEnumerator Projectile_FlyingStraight_MissesIfPlayerSidesteps()
        {
            yield return null;
            var e = spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 100f, projSpeed: 4f, lifetime: 2f), new Vector2(5f, 0f));
            projectiles.Fire(e.Position, Vector2.left, e.Data); // 발사 시점의 방향으로 직진

            playerGo.transform.position = new Vector3(0f, 3f, 0f); // 옆으로 비킴
            yield return new WaitForSeconds(2.2f);

            Assert.AreEqual(100f, health.Current, 0.001f, "직선으로 날아가므로 피하면 맞지 않음");
            Assert.AreEqual(0, projectiles.HitCount);
        }

        [UnityTest]
        public IEnumerator Projectile_IsConsumed_ButDealsNoDamage_WhenSkillInvulnerable()
        {
            yield return null;
            health.SetInvulnerable(5f);
            var e = spawner.SpawnAt(Archer(speed: 0f, range: 5f, fireInterval: 100f, projSpeed: 10f), new Vector2(3f, 0f));
            projectiles.Fire(e.Position, Vector2.left, e.Data);

            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(100f, health.Current, 0.001f);
            Assert.AreEqual(1, projectiles.HitCount, "명중은 했지만 피해 없음");
            Assert.AreEqual(0, projectiles.ActiveCount, "투사체는 소멸");
        }

        [UnityTest]
        public IEnumerator Projectile_PoolReusesInstances()
        {
            yield return null;
            var data = Archer(speed: 0f, range: 5f, fireInterval: 100f, projSpeed: 20f, lifetime: 0.2f);
            var e = spawner.SpawnAt(data, new Vector2(4f, 0f));

            projectiles.Fire(e.Position, Vector2.right, data);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0, projectiles.ActiveCount);
            int created = projectiles.PooledTotal;

            projectiles.Fire(e.Position, Vector2.right, data);
            Assert.AreEqual(created, projectiles.PooledTotal, "소멸한 투사체를 재사용");
        }

        [UnityTest]
        public IEnumerator Projectile_ScaleMatchesConfiguredRadius()
        {
            yield return null;
            var data = Archer(speed: 0f, fireInterval: 100f);
            data.projectileSize = 0.5f;
            var e = spawner.SpawnAt(data, new Vector2(4f, 0f));

            var p = projectiles.Fire(e.Position, Vector2.right, data);
            var sr = p.GetComponent<SpriteRenderer>();

            Assert.AreEqual(1.0f, sr.bounds.size.x, 0.05f, "지름 = 반지름 × 2");
        }

        // ───────────────────────── 실제 에셋 ─────────────────────────

        [Test]
        public void EnemyAssets_ArchersAreConfiguredToShoot_AndRolesAreConsistent()
        {
            var guids = AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyDir });
            Assert.Greater(guids.Length, 0, "적 데이터가 없습니다. Step 3/7 을 먼저 실행하세요.");

            int archers = 0;
            foreach (var guid in guids)
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d.attackRange > 0f)
                {
                    archers++;
                    Assert.AreEqual(EnemyRole.Archer, d.role, $"{d.displayName}: 원거리 공격 적은 궁병 병과여야 함");
                    Assert.Greater(d.fireInterval, 0.5f, $"{d.displayName}: 발사 간격");
                    Assert.Greater(d.projectileSpeed, 0f, $"{d.displayName}: 투사체 속도");
                    Assert.Greater(d.projectileDamage, 0, $"{d.displayName}: 투사체 피해");
                    Assert.Greater(d.projectileLifetime * d.projectileSpeed, d.attackRange,
                        $"{d.displayName}: 투사체가 사정거리 끝까지는 날아가야 함");
                    Assert.IsNotNull(d.sprite, $"{d.displayName}: 병과 스프라이트");
                }
                else
                {
                    Assert.AreNotEqual(EnemyRole.Archer, d.role, $"{d.displayName}: 원거리 공격이 없는데 궁병 병과");
                }

                if (d.chargeInterval > 0f && d.displayName.Contains("기병"))
                    Assert.AreEqual(EnemyRole.Cavalry, d.role, $"{d.displayName}: 기병 병과");
            }
            Assert.GreaterOrEqual(archers, 3, "궁병 3종 이상");
        }

        [Test]
        public void Stage_IncludesArchersInLaterWaves_ButNotInTheFirst()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
            Assert.IsNotNull(stage, "스테이지 에셋이 없습니다. Step 7 을 먼저 실행하세요.");

            bool IsArcherWave(Wave w) => w.enemies.Exists(e => e.data != null && e.data.role == EnemyRole.Archer);

            Assert.IsFalse(IsArcherWave(stage.waves[0]), "첫 웨이브에는 궁병이 없어 초반 난이도가 낮아야 함");
            for (int i = 1; i < stage.waves.Count; i++)
                Assert.IsTrue(IsArcherWave(stage.waves[i]), $"웨이브 {i + 1}에 궁병이 편성되어야 함 (Step 8-3 실행)");
        }
    }
}
