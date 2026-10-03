using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class CombatTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        PlayerHealth health;
        EnemyData chaser;   // 플레이어를 쫓는 적
        EnemyData still;    // 제자리에 서 있는 적 (명중 테스트용)
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Samkuk > Step 3 를 먼저 실행하세요.");

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
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = enemyPrefab;
            spawner.autoSpawn = false;
            root.SetActive(true);

            chaser = MakeEnemyData(hp: 10, speed: 2f, contactDamage: 10);
            still = MakeEnemyData(hp: 10, speed: 0f, contactDamage: 0);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (playerGo != null) Object.Destroy(playerGo);
            if (camGo != null) Object.Destroy(camGo);
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
        }

        EnemyData MakeEnemyData(int hp, float speed, int contactDamage)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp;
            d.moveSpeed = speed;
            d.contactDamage = contactDamage;
            d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        WeaponData MakeWeaponData(WeaponType type)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.type = type;
            w.displayName = type.ToString();
            toDestroy.Add(w);
            return w;
        }

        WeaponController SetupWeaponController()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<Projectile>();
            Assert.IsNotNull(projectile, "Projectile 프리팹이 없습니다. Samkuk > Step 5 를 먼저 실행하세요.");

            var wc = playerGo.AddComponent<WeaponController>();
            wc.ProjectilePrefab = projectile;
            wc.EnemyManager = manager;
            return wc;
        }

        // ───────────────────────── 적 체력 ─────────────────────────

        [UnityTest]
        public IEnumerator Enemy_TakesDamage_ThenDiesAndLeavesManager()
        {
            yield return null;
            int damagedEvents = 0, diedEvents = 0;
            void OnDamaged(Enemy e, float a) => damagedEvents++;
            void OnDied(Enemy e) => diedEvents++;
            Enemy.Damaged += OnDamaged;
            Enemy.Died += OnDied;

            try
            {
                var e = spawner.SpawnAt(still, new Vector2(5f, 0f));
                e.TakeDamage(4f);
                Assert.IsTrue(e.Alive);
                Assert.AreEqual(6f, e.Hp, 0.001f);
                Assert.AreEqual(1, manager.Count);

                e.TakeDamage(6f);
                Assert.IsFalse(e.Alive);
                Assert.AreEqual(0, manager.Count, "사망하면 매니저에서 제거");
                Assert.AreEqual(2, damagedEvents);
                Assert.AreEqual(1, diedEvents);

                e.TakeDamage(5f); // 이미 죽은 적에게는 무시
                Assert.AreEqual(2, damagedEvents);
            }
            finally
            {
                Enemy.Damaged -= OnDamaged;
                Enemy.Died -= OnDied;
            }
        }

        [UnityTest]
        public IEnumerator Enemy_Respawn_ResetsHp()
        {
            yield return null;
            var e = spawner.SpawnAt(still, new Vector2(5f, 0f));
            e.TakeDamage(100f);
            Assert.AreEqual(0, manager.Count);

            var again = spawner.SpawnAt(still, new Vector2(5f, 0f));
            Assert.AreSame(e, again, "풀에서 같은 인스턴스를 재사용");
            Assert.IsTrue(again.Alive);
            Assert.AreEqual(10f, again.Hp, 0.001f);
        }

        [UnityTest]
        public IEnumerator Enemy_HitFlash_RestoresScale()
        {
            yield return null;
            var e = spawner.SpawnAt(still, new Vector2(5f, 0f));
            e.TakeDamage(1f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1f, e.transform.localScale.x, 0.001f);
        }

        // ───────────────────────── 플레이어 체력 ─────────────────────────

        [UnityTest]
        public IEnumerator ContactDamage_AppliesOncePerInvulnerabilityWindow()
        {
            yield return null;
            spawner.SpawnAt(chaser, new Vector2(0.5f, 0f)); // 접촉 범위 안

            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(90f, health.Current, 0.001f, "첫 접촉에서 한 번만 피해");

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(90f, health.Current, 0.001f, "무적 시간(0.5초) 동안은 추가 피해 없음");

            yield return new WaitForSeconds(0.6f);
            Assert.LessOrEqual(health.Current, 80.001f, "무적이 끝나면 다시 피해");
            Assert.GreaterOrEqual(health.Current, 70f);
        }

        [UnityTest]
        public IEnumerator Player_Dies_AtZeroHp()
        {
            yield return null;
            int died = 0;
            health.Died += () => died++;

            health.TakeDamage(1000f);

            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Current);
            Assert.AreEqual(1, died);
            Assert.IsFalse(health.TryContactDamage(5f), "죽은 뒤에는 접촉 피해 불가");

            health.TakeDamage(5f);
            Assert.AreEqual(1, died, "사망 이벤트는 한 번만");
        }

        [UnityTest]
        public IEnumerator Player_Heal_ClampsToMax()
        {
            yield return null;
            health.TakeDamage(30f);
            health.Heal(100f);
            Assert.AreEqual(health.Max, health.Current, 0.001f);
        }

        // ───────────────────────── 매니저 질의 ─────────────────────────

        [UnityTest]
        public IEnumerator OverlapCircle_FindsOnlyEnemiesInside_WithoutDuplicates()
        {
            yield return null;
            spawner.SpawnAt(still, new Vector2(3f, 0f));
            spawner.SpawnAt(still, new Vector2(3.4f, 0f));
            spawner.SpawnAt(still, new Vector2(10f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var results = new List<Enemy>();
            manager.OverlapCircle(new Vector2(3f, 0f), 1f, results);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(results.Count, new HashSet<Enemy>(results).Count, "중복 없음");
        }

        [UnityTest]
        public IEnumerator FindNearest_ReturnsSortedByDistance_WithinRange()
        {
            yield return null;
            spawner.SpawnAt(still, new Vector2(5f, 0f));
            spawner.SpawnAt(still, new Vector2(3f, 0f));
            spawner.SpawnAt(still, new Vector2(8f, 0f));
            spawner.SpawnAt(still, new Vector2(30f, 0f)); // 범위 밖

            var results = new Enemy[2];
            int n = manager.FindNearest(Vector2.zero, 10f, results);

            Assert.AreEqual(2, n);
            Assert.AreEqual(3f, results[0].Position.x, 0.01f);
            Assert.AreEqual(5f, results[1].Position.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator FindNearest_NoEnemies_ReturnsZero()
        {
            yield return null;
            Assert.AreEqual(0, manager.FindNearest(Vector2.zero, 10f, new Enemy[3]));
        }

        // ───────────────────────── 무기 ─────────────────────────

        [UnityTest]
        public IEnumerator ArrowWeapon_KillsNearestEnemy()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Arrow);
            data.damage = 10f; data.cooldown = 0.5f; data.range = 9f;
            data.projectileSpeed = 12f; data.pierce = 1; data.duration = 1.5f; data.size = 0.3f;
            wc.AddWeapon(data);

            yield return null;
            var e = spawner.SpawnAt(still, new Vector2(4f, 0f));

            yield return new WaitForSeconds(1.2f);

            Assert.IsFalse(e.Alive, "화살이 가장 가까운 적을 맞혀 죽여야 함");
            Assert.AreEqual(0, manager.Count);
        }

        [UnityTest]
        public IEnumerator ArrowWeapon_DoesNotFire_WhenNoTarget_ThenFiresImmediatelyWhenEnemyAppears()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Arrow);
            data.damage = 10f; data.cooldown = 5f; data.range = 9f;
            data.projectileSpeed = 12f; data.pierce = 1; data.duration = 1.5f; data.size = 0.3f;
            wc.AddWeapon(data);

            yield return new WaitForSeconds(0.5f); // 대상 없이 대기 (쿨다운 5초지만 준비 상태 유지)
            var e = spawner.SpawnAt(still, new Vector2(4f, 0f));

            yield return new WaitForSeconds(0.8f);

            Assert.IsFalse(e.Alive, "대상이 생기면 즉시 발사해야 함");
        }

        [UnityTest]
        public IEnumerator ArrowWeapon_Pierce_HitsMultipleEnemiesInLine()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Arrow);
            data.damage = 10f; data.cooldown = 10f; data.range = 12f;
            data.projectileSpeed = 12f; data.pierce = 3; data.duration = 2f; data.size = 0.3f;
            wc.AddWeapon(data);

            yield return null;
            var a = spawner.SpawnAt(still, new Vector2(3f, 0f));
            var b = spawner.SpawnAt(still, new Vector2(4.5f, 0f));
            var c = spawner.SpawnAt(still, new Vector2(6f, 0f));

            yield return new WaitForSeconds(1.5f);

            Assert.IsFalse(a.Alive);
            Assert.IsFalse(b.Alive);
            Assert.IsFalse(c.Alive, "관통 3: 일렬의 세 적 모두 명중");
        }

        [UnityTest]
        public IEnumerator ArrowWeapon_ProjectilesReturnToPool()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Arrow);
            data.damage = 1f; data.cooldown = 0.2f; data.range = 20f;
            data.projectileSpeed = 12f; data.pierce = 1; data.duration = 0.3f; data.size = 0.1f;
            wc.AddWeapon(data);

            yield return null;
            var e = spawner.SpawnAt(MakeEnemyData(hp: 100000, speed: 0f, contactDamage: 0), new Vector2(15f, 5f));

            yield return new WaitForSeconds(2f);

            int active = 0;
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude)) active++;
            Assert.LessOrEqual(active, 3, $"수명이 짧은 투사체는 풀로 돌아가야 함 (활성 {active})");
            Assert.IsTrue(e.Alive);
        }

        [UnityTest]
        public IEnumerator SlashWeapon_HitsEnemyInFront()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Slash);
            data.damage = 20f; data.cooldown = 0.5f; data.range = 2.4f; data.count = 2; data.duration = 0.1f;
            wc.AddWeapon(data);

            yield return null;
            var right = spawner.SpawnAt(still, new Vector2(1.5f, 0f));
            var left = spawner.SpawnAt(still, new Vector2(-1.5f, 0f));

            yield return new WaitForSeconds(1.2f);

            Assert.IsFalse(right.Alive, "오른쪽 베기");
            Assert.IsFalse(left.Alive, "count 2: 왼쪽도 베기");
        }

        [UnityTest]
        public IEnumerator SlashWeapon_DoesNotHitFarEnemy()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Slash);
            data.damage = 20f; data.cooldown = 0.3f; data.range = 2.4f; data.count = 2; data.duration = 0.1f;
            wc.AddWeapon(data);

            yield return null;
            var far = spawner.SpawnAt(still, new Vector2(6f, 0f));

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(far.Alive);
            Assert.AreEqual(10f, far.Hp, 0.001f);
        }

        [UnityTest]
        public IEnumerator OrbitWeapon_DamagesEnemyOnOrbit()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Orbit);
            data.damage = 20f; data.range = 2f; data.count = 1; data.size = 0.5f;
            data.rotateSpeed = 200f; data.tickInterval = 0.1f;
            wc.AddWeapon(data);

            yield return null;
            var e = spawner.SpawnAt(still, new Vector2(0f, 2f));

            yield return new WaitForSeconds(2.2f); // 한 바퀴(1.8초) 이내에 지나감

            Assert.IsFalse(e.Alive, "공전하는 칼날에 맞아야 함");
        }

        [UnityTest]
        public IEnumerator OrbitWeapon_BladesStayAtOrbitRadiusAroundPlayer()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Orbit);
            data.damage = 1f; data.range = 2f; data.count = 3; data.size = 0.3f; data.rotateSpeed = 90f;
            var w = wc.AddWeapon(data);

            yield return new WaitForSeconds(0.3f);
            playerGo.transform.position = new Vector3(5f, 5f, 0f);
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual(3, w.transform.childCount);
            foreach (Transform blade in w.transform)
                Assert.AreEqual(2f, Vector2.Distance(blade.position, playerGo.transform.position), 0.05f);
        }

        // ───────────────────────── 무기 레벨 ─────────────────────────

        [UnityTest]
        public IEnumerator Weapon_LevelUp_ScalesStats_AndStopsAtMax()
        {
            var wc = SetupWeaponController();
            var data = MakeWeaponData(WeaponType.Arrow);
            data.damage = 10f; data.cooldown = 1f; data.count = 1;
            data.maxLevel = 3; data.damagePerLevel = 0.5f; data.cooldownReductionPerLevel = 0.1f; data.levelsPerExtraCount = 2;
            var w = wc.AddWeapon(data);
            yield return null;

            Assert.AreEqual(1, w.Level);
            Assert.AreEqual(10f, w.Damage, 0.001f);
            Assert.AreEqual(1, w.Count);

            Assert.AreSame(w, wc.AddWeapon(data), "같은 무기를 다시 추가하면 레벨업");
            Assert.AreEqual(2, w.Level);
            Assert.AreEqual(15f, w.Damage, 0.001f);
            Assert.AreEqual(0.9f, w.Cooldown, 0.001f);

            wc.LevelUpAll();
            Assert.AreEqual(3, w.Level);
            Assert.AreEqual(2, w.Count, "3레벨: 수량 +1");
            Assert.IsTrue(w.IsMaxLevel);

            Assert.IsFalse(w.LevelUp());
            Assert.AreEqual(3, w.Level);
            Assert.AreEqual(1, wc.Weapons.Count);
        }

        // ───────────────────────── UI ─────────────────────────

        [UnityTest]
        public IEnumerator DamageNumber_AppearsOnHit_AndDisappears()
        {
            var go = new GameObject("TestDamageNumbers");
            toDestroy.Add(go);
            var dn = go.AddComponent<DamageNumberSpawner>();
            yield return null;

            var e = spawner.SpawnAt(still, new Vector2(5f, 0f));
            e.TakeDamage(3f);

            int Active()
            {
                int n = 0;
                foreach (var t in dn.GetComponentsInChildren<DamageText>(false)) n++;
                return n;
            }

            Assert.AreEqual(1, Active(), "피격 시 숫자 하나 표시");
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(0, Active(), "잠시 뒤 사라짐");
        }
    }
}
