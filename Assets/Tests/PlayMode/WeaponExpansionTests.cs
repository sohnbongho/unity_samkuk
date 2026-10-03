using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class WeaponExpansionTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string CatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        WeaponController wc;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            playerGo.AddComponent<PlayerStats>();
            playerGo.AddComponent<PlayerHealth>();

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

            wc = playerGo.AddComponent<WeaponController>(); // PlayerController 자동 추가
            wc.EnemyManager = manager;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (root != null) UnityEngine.Object.Destroy(root);
            if (playerGo != null) UnityEngine.Object.Destroy(playerGo);
            if (camGo != null) UnityEngine.Object.Destroy(camGo);
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        EnemyData Still(int hp = 10)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp; d.moveSpeed = 0f; d.contactDamage = 0; d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        WeaponData MakeWeapon(WeaponType type, Action<WeaponData> configure)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.type = type;
            w.displayName = type.ToString();
            w.cooldown = 10f; // 한 번만 발동하도록 (타이머가 가득 찬 채 시작하므로 첫 프레임에 즉시 발동)
            w.levelsPerExtraCount = 0;
            configure(w);
            toDestroy.Add(w);
            return w;
        }

        Sprite MakeSprite()
        {
            var tex = new Texture2D(8, 8);
            toDestroy.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            toDestroy.Add(sprite);
            return sprite;
        }

        /// <summary>그리드(FixedUpdate)가 적을 인식하도록 물리 프레임을 두 번 기다린다.</summary>
        IEnumerator WaitForGrid()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        // ───────────────────────── 창 찌르기 ─────────────────────────

        [UnityTest]
        public IEnumerator Thrust_PiercesLine_ButNotSidewaysBehindOrBeyondReach()
        {
            yield return null;
            var front = spawner.SpawnAt(Still(), new Vector2(2f, 0f));
            var frontFar = spawner.SpawnAt(Still(), new Vector2(3.5f, 0.2f));
            var side = spawner.SpawnAt(Still(), new Vector2(2f, 2.2f));
            var behind = spawner.SpawnAt(Still(), new Vector2(-2.5f, 0f));
            var beyond = spawner.SpawnAt(Still(), new Vector2(6f, 0f));
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Thrust, w =>
            {
                w.damage = 20f; w.range = 4f; w.size = 0.4f; w.count = 1;
            }));
            yield return new WaitForSeconds(0.3f);

            Assert.IsFalse(front.Alive, "정면의 적");
            Assert.IsFalse(frontFar.Alive, "정면 일직선상의 먼 적도 관통");
            Assert.IsTrue(side.Alive, "옆으로 벗어난 적은 안 맞음");
            Assert.IsTrue(behind.Alive, "뒤쪽 적은 안 맞음");
            Assert.IsTrue(beyond.Alive, "창 길이 밖의 적은 안 맞음");
        }

        [UnityTest]
        public IEnumerator Thrust_Count2_AlsoThrustsOppositeDirection()
        {
            yield return null;
            var front = spawner.SpawnAt(Still(), new Vector2(2f, 0f));
            var behind = spawner.SpawnAt(Still(), new Vector2(-2.5f, 0f));
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Thrust, w =>
            {
                w.damage = 20f; w.range = 4f; w.size = 0.4f; w.count = 2;
            }));
            yield return new WaitForSeconds(0.3f);

            Assert.IsFalse(front.Alive);
            Assert.IsFalse(behind.Alive, "count 2: 반대 방향으로도 찌름");
        }

        [UnityTest]
        public IEnumerator Thrust_WaitsForTarget_WithoutWastingCooldown()
        {
            wc.AddWeapon(MakeWeapon(WeaponType.Thrust, w =>
            {
                w.damage = 20f; w.range = 4f; w.size = 0.4f; w.count = 1;
            }));
            yield return new WaitForSeconds(0.4f); // 대상 없음

            var e = spawner.SpawnAt(Still(), new Vector2(2f, 0f));
            yield return new WaitForSeconds(0.4f);

            Assert.IsFalse(e.Alive, "대상이 생기면 곧바로 찌름 (쿨다운 10초지만 낭비하지 않았음)");
        }

        [UnityTest]
        public IEnumerator Thrust_KnocksEnemiesAway()
        {
            yield return null;
            var e = spawner.SpawnAt(Still(1000), new Vector2(2f, 0f));
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Thrust, w =>
            {
                w.damage = 1f; w.range = 4f; w.size = 0.4f; w.knockback = 8f;
            }));
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(e.Position.x, 2.5f, $"넉백으로 밀려남 (x={e.Position.x})");
            Assert.AreEqual(0f, e.KnockbackVelocity.magnitude, 0.2f, "넉백은 시간이 지나면 감쇠");
        }

        // ───────────────────────── 화계 ─────────────────────────

        [UnityTest]
        public IEnumerator FireZone_BurnsOverTime_ThenExpires()
        {
            yield return null;
            var e = spawner.SpawnAt(Still(1000), new Vector2(4f, 0f));
            yield return WaitForGrid();

            var weapon = (FireZoneWeapon)wc.AddWeapon(MakeWeapon(WeaponType.FireZone, w =>
            {
                w.damage = 5f; w.range = 8f; w.size = 1.2f; w.count = 1; w.duration = 1f; w.tickInterval = 0.2f;
            }));
            yield return new WaitForSeconds(0.55f);

            Assert.Less(e.Hp, 1000f - 10f, $"장판이 여러 번 피해를 줌 (HP {e.Hp})");
            Assert.AreEqual(1, weapon.ActiveZones);

            yield return new WaitForSeconds(1.0f); // 장판 소멸
            float afterExpire = e.Hp;
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(0, weapon.ActiveZones, "지속 시간이 지나면 장판 제거");
            Assert.AreEqual(afterExpire, e.Hp, 0.001f, "소멸 후에는 피해 없음");
        }

        [UnityTest]
        public IEnumerator FireZone_Count2_PlacesZonesOnDifferentEnemies()
        {
            yield return null;
            var left = spawner.SpawnAt(Still(1000), new Vector2(-5f, 0f));
            var right = spawner.SpawnAt(Still(1000), new Vector2(5f, 0f));
            yield return WaitForGrid();

            var weapon = (FireZoneWeapon)wc.AddWeapon(MakeWeapon(WeaponType.FireZone, w =>
            {
                w.damage = 5f; w.range = 8f; w.size = 1f; w.count = 2; w.duration = 1f; w.tickInterval = 0.2f;
            }));
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(2, weapon.ActiveZones);
            Assert.Less(left.Hp, 1000f);
            Assert.Less(right.Hp, 1000f);
        }

        [UnityTest]
        public IEnumerator FireZone_NoEnemiesInRange_PlacesNothing()
        {
            yield return null;
            spawner.SpawnAt(Still(), new Vector2(20f, 0f)); // 범위(8) 밖
            yield return WaitForGrid();

            var weapon = (FireZoneWeapon)wc.AddWeapon(MakeWeapon(WeaponType.FireZone, w =>
            {
                w.damage = 5f; w.range = 8f; w.size = 1f; w.duration = 1f;
            }));
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(0, weapon.ActiveZones);
        }

        // ───────────────────────── 뇌격 ─────────────────────────

        [UnityTest]
        public IEnumerator Lightning_StrikesOneTarget_AndSplashesNeighbors_NotFarOnes()
        {
            yield return null;
            // 가까이 붙은 두 적(한 번에 맞음) + 멀리 떨어진 적 + 사거리 밖의 적
            var a = spawner.SpawnAt(Still(1000), new Vector2(4f, 0f));
            var b = spawner.SpawnAt(Still(1000), new Vector2(4.4f, 0f));
            var apart = spawner.SpawnAt(Still(1000), new Vector2(-6f, 0f));
            var outOfRange = spawner.SpawnAt(Still(1000), new Vector2(0f, 14f));
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Lightning, w =>
            {
                w.damage = 30f; w.range = 9f; w.size = 0.9f; w.count = 1;
            }));
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(1000f, outOfRange.Hp, 0.001f, "사거리 밖의 적은 대상이 아님");

            bool abHit = a.Hp < 1000f;
            if (abHit)
            {
                Assert.Less(b.Hp, 1000f, "벼락 범위 안의 이웃도 함께 피해");
                Assert.AreEqual(1000f, apart.Hp, 0.001f, "멀리 떨어진 적은 안 맞음");
            }
            else
            {
                Assert.Less(apart.Hp, 1000f, "다른 쪽이 대상이었어야 함");
                Assert.AreEqual(1000f, a.Hp, 0.001f);
                Assert.AreEqual(1000f, b.Hp, 0.001f);
            }
        }

        [UnityTest]
        public IEnumerator Lightning_Count3_StrikesThreeDifferentEnemies()
        {
            yield return null;
            var enemies = new[]
            {
                spawner.SpawnAt(Still(1000), new Vector2(4f, 0f)),
                spawner.SpawnAt(Still(1000), new Vector2(-4f, 0f)),
                spawner.SpawnAt(Still(1000), new Vector2(0f, 5f)),
            };
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Lightning, w =>
            {
                w.damage = 30f; w.range = 9f; w.size = 0.9f; w.count = 3;
            }));
            yield return new WaitForSeconds(0.3f);

            foreach (var e in enemies) Assert.AreEqual(970f, e.Hp, 0.001f, "서로 다른 세 적이 각각 한 번씩 피해");
        }

        // ───────────────────────── 화살비 ─────────────────────────

        [UnityTest]
        public IEnumerator Rain_WarnsFirst_ThenExplodesAfterDelay()
        {
            yield return null;
            var e = spawner.SpawnAt(Still(1000), new Vector2(3f, 0f));
            yield return WaitForGrid();

            var weapon = (RainWeapon)wc.AddWeapon(MakeWeapon(WeaponType.Rain, w =>
            {
                w.damage = 10f; w.range = 7f; w.size = 1.0f; w.count = 3; w.duration = 0.5f;
            }));
            yield return new WaitForSeconds(0.25f);

            Assert.AreEqual(3, weapon.PendingImpacts, "낙하 예고 중");
            Assert.AreEqual(1000f, e.Hp, 0.001f, "착탄 전에는 피해 없음");

            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(0, weapon.PendingImpacts);
            Assert.AreEqual(970f, e.Hp, 0.001f, "화살 3발이 모두 명중 (3 × 10)");
        }

        [UnityTest]
        public IEnumerator Rain_FarEnemiesOutsideImpactRadius_AreNotHit()
        {
            yield return null;
            var target = spawner.SpawnAt(Still(1000), new Vector2(3f, 0f));
            var bystander = spawner.SpawnAt(Still(1000), new Vector2(3f, 6f)); // 사거리 7 밖은 아님(6.7)이지만 폭발 반경 밖
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Rain, w =>
            {
                w.damage = 10f; w.range = 7f; w.size = 0.8f; w.count = 1; w.duration = 0.3f;
            }));
            yield return new WaitForSeconds(0.8f);

            // 대상은 두 적 중 하나. 대상이 아닌 쪽은 폭발 반경(0.8 + 산포 0.6) 안에 들지 않으므로 무사해야 한다.
            bool targetHit = target.Hp < 1000f;
            bool bystanderHit = bystander.Hp < 1000f;
            Assert.IsTrue(targetHit ^ bystanderHit, "한 발은 한 지점만 폭격");
        }

        // ───────────────────────── 전고(충격파) ─────────────────────────

        [UnityTest]
        public IEnumerator Nova_HitsEachEnemyOnce_AndPushesThemAway()
        {
            yield return null;
            var near = spawner.SpawnAt(Still(1000), new Vector2(2f, 0f));
            var mid = spawner.SpawnAt(Still(1000), new Vector2(0f, -3.5f));
            var far = spawner.SpawnAt(Still(1000), new Vector2(7f, 0f)); // 최대 반지름(4.5) 밖
            yield return WaitForGrid();

            var weapon = (NovaWeapon)wc.AddWeapon(MakeWeapon(WeaponType.Nova, w =>
            {
                w.damage = 10f; w.range = 4.5f; w.duration = 0.5f; w.knockback = 9f;
            }));
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(weapon.IsExpanding, "충격파가 퍼지는 중");

            yield return new WaitForSeconds(0.9f);

            Assert.IsFalse(weapon.IsExpanding);
            Assert.AreEqual(990f, near.Hp, 0.001f, "한 번의 충격파에는 한 번만 피해");
            Assert.AreEqual(990f, mid.Hp, 0.001f);
            Assert.AreEqual(1000f, far.Hp, 0.001f, "반경 밖은 영향 없음");
            Assert.Greater(near.Position.x, 2.4f, "중심에서 바깥쪽으로 밀려남");
            Assert.Less(mid.Position.y, -3.7f);
        }

        [UnityTest]
        public IEnumerator Nova_DoesNotFire_WithoutEnemiesNearby()
        {
            yield return null;
            spawner.SpawnAt(Still(), new Vector2(12f, 0f));
            yield return WaitForGrid();

            var weapon = (NovaWeapon)wc.AddWeapon(MakeWeapon(WeaponType.Nova, w =>
            {
                w.damage = 10f; w.range = 4.5f; w.duration = 0.5f;
            }));
            yield return new WaitForSeconds(0.3f);

            Assert.IsFalse(weapon.IsExpanding);
        }

        // ───────────────────────── 넉백 / 검 베기 ─────────────────────────

        [UnityTest]
        public IEnumerator Enemy_Knockback_MovesThenDecaysAndResetsOnReuse()
        {
            yield return null;
            var data = Still(5);
            var e = spawner.SpawnAt(data, new Vector2(6f, 0f));

            e.Knockback(new Vector2(6f, 0f));
            yield return new WaitForSeconds(0.6f);

            float moved = e.Position.x - 6f;
            Assert.Greater(moved, 0.5f, "넉백 방향으로 이동");
            Assert.Less(moved, 1.2f, "감쇠하므로 무한히 밀리지 않음 (≈ 6 / 8 = 0.75)");
            Assert.AreEqual(0f, e.KnockbackVelocity.magnitude, 0.1f);

            e.Knockback(new Vector2(0f, 5f));
            e.TakeDamage(100f);
            var again = spawner.SpawnAt(data, new Vector2(6f, 0f));
            Assert.AreSame(e, again);
            Assert.AreEqual(Vector2.zero, again.KnockbackVelocity, "재사용 시 넉백 초기화");
        }

        [Test]
        public void Enemy_Knockback_IsClampedAndIgnoredWhenDead()
        {
            var e = spawner.SpawnAt(Still(5), new Vector2(6f, 0f));
            e.Knockback(new Vector2(1000f, 0f));
            Assert.LessOrEqual(e.KnockbackVelocity.magnitude, 14.01f, "넉백 최대 속도 제한");

            e.TakeDamage(100f);
            e.Knockback(new Vector2(5f, 0f));
            Assert.IsFalse(e.Alive);
        }

        [UnityTest]
        public IEnumerator Slash_WithKnockback_PushesEnemiesOutward()
        {
            yield return null;
            var e = spawner.SpawnAt(Still(1000), new Vector2(1.3f, 0f));
            yield return WaitForGrid();

            wc.AddWeapon(MakeWeapon(WeaponType.Slash, w =>
            {
                w.damage = 1f; w.range = 2.4f; w.count = 2; w.duration = 0.1f; w.knockback = 8f;
            }));
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(e.Position.x, 1.7f, $"검 베기에 밀려남 (x={e.Position.x})");
        }

        // ───────────────────────── 이펙트 풀 ─────────────────────────

        [UnityTest]
        public IEnumerator WeaponFx_PlaysFades_AndReusesInstances()
        {
            yield return null;
            var fx = wc.Fx;
            Assert.IsNotNull(fx);
            var sprite = MakeSprite();

            fx.Play(sprite, Vector2.zero, 0f, Vector2.one, Color.white, 0.2f);
            Assert.AreEqual(1, fx.ActiveCount);
            Assert.AreEqual(1, fx.CreatedCount);

            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0, fx.ActiveCount, "재생이 끝나면 숨김");

            fx.Play(sprite, Vector2.zero, 0f, Vector2.one, Color.white, 0.2f);
            Assert.AreEqual(1, fx.CreatedCount, "끝난 이펙트 오브젝트를 재사용");
        }

        [Test]
        public void WeaponFx_NullSprite_IsIgnored()
        {
            wc.Fx.Play(null, Vector2.zero, 0f, Vector2.one, Color.white, 0.2f);
            wc.Fx.PlayDisc(null, Vector2.zero, 1f, Color.white, 0.2f);
            Assert.AreEqual(0, wc.Fx.ActiveCount);
        }

        [Test]
        public void WeaponFx_IsNotParentedToPlayer()
        {
            Assert.IsNull(wc.Fx.transform.parent, "이펙트는 월드 좌표에 고정되어야 하므로 플레이어의 자식이 아님");
        }

        // ───────────────────────── 구조 ─────────────────────────

        [Test]
        public void AddWeapon_CreatesMatchingBehaviour_ForEveryWeaponType()
        {
            var expected = new Dictionary<WeaponType, Type>
            {
                { WeaponType.Arrow, typeof(ArrowWeapon) },
                { WeaponType.Slash, typeof(SlashWeapon) },
                { WeaponType.Orbit, typeof(OrbitWeapon) },
                { WeaponType.Thrust, typeof(ThrustWeapon) },
                { WeaponType.FireZone, typeof(FireZoneWeapon) },
                { WeaponType.Lightning, typeof(LightningWeapon) },
                { WeaponType.Rain, typeof(RainWeapon) },
                { WeaponType.Nova, typeof(NovaWeapon) },
            };

            foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
            {
                Assert.IsTrue(expected.ContainsKey(type), $"{type} 에 대한 기대 타입이 테스트에 없음");
                var data = MakeWeapon(type, w => { w.damage = 1f; w.range = 3f; w.size = 0.5f; w.duration = 0.5f; });
                var weapon = wc.AddWeapon(data);

                Assert.IsNotNull(weapon, $"{type}: 무기 생성 실패 (WeaponController.AddWeapon 에 case 누락?)");
                Assert.AreEqual(expected[type], weapon.GetType(), type.ToString());
            }
        }

        [Test]
        public void UpgradeCatalogAsset_HasAtLeastTenWeapons_CoveringEveryWeaponType()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "UpgradeCatalog 가 없습니다. Samkuk > Step 6 / Step 8-2 를 먼저 실행하세요.");
            Assert.GreaterOrEqual(catalog.weapons.Count, 10, "레벨업으로 얻을 수 있는 무기 10종 이상");

            var names = new HashSet<string>();
            var types = new HashSet<WeaponType>();
            foreach (var w in catalog.weapons)
            {
                Assert.IsNotNull(w, "카탈로그에 빈 무기 항목");
                Assert.IsTrue(names.Add(w.displayName), $"무기 이름 중복: {w.displayName}");
                Assert.IsFalse(string.IsNullOrEmpty(w.description), $"{w.displayName}: 설명 누락");
                Assert.Greater(w.damage, 0f, $"{w.displayName}: 피해량");
                Assert.Greater(w.maxLevel, 1, $"{w.displayName}: 최대 레벨");
                if (w.type != WeaponType.Orbit && w.type != WeaponType.Nova)
                    Assert.Greater(w.cooldown, 0f, $"{w.displayName}: 쿨다운");
                if (w.type != WeaponType.Slash && w.type != WeaponType.Orbit && w.type != WeaponType.Arrow)
                    Assert.IsNotNull(w.sprite, $"{w.displayName}: 이펙트 스프라이트");
                types.Add(w.type);
            }

            foreach (WeaponType t in Enum.GetValues(typeof(WeaponType)))
                Assert.IsTrue(types.Contains(t), $"카탈로그에 {t} 타입 무기가 없음");
        }
    }
}
