using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Feedback;
using Samkuk.Meta;
using Samkuk.Player;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    public class FeedbackTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

        string savePath;
        GameObject playerGo, camGo, root;
        PlayerHealth health;
        PlayerExperience exp;
        WeaponController wc;
        EnemySpawner spawner;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            // 실제 저장 파일을 건드리지 않도록 임시 경로를 쓴다
            savePath = Path.Combine(Application.temporaryCachePath, $"test_fb_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            AudioManager.DestroyInstance();
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");

            if (playerGo != null) UnityEngine.Object.Destroy(playerGo);
            if (camGo != null) UnityEngine.Object.Destroy(camGo);
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            toDestroy.Add(go);
            return go.AddComponent<T>();
        }

        EnemyData Dummy(int hp, Color? tint = null)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp; d.moveSpeed = 0f; d.contactDamage = 0; d.colliderRadius = 0.3f;
            d.tint = tint ?? Color.white;
            toDestroy.Add(d);
            return d;
        }

        /// <summary>플레이어, 카메라, 적 시스템, 무기 컨트롤러를 만든다.</summary>
        void BuildWorld()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            playerGo.AddComponent<PlayerStats>();
            health = playerGo.AddComponent<PlayerHealth>();
            exp = playerGo.AddComponent<PlayerExperience>();

            camGo = new GameObject("TestCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 6f; cam.aspect = 16f / 9f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            root = new GameObject("TestSystems");
            root.SetActive(false);
            var manager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager; spawner.Cam = cam; spawner.EnemyPrefab = enemyPrefab; spawner.autoSpawn = false;
            root.SetActive(true);

            wc = playerGo.AddComponent<WeaponController>();
            wc.EnemyManager = manager;
        }

        struct Rig
        {
            public FeedbackHooks hooks;
            public ScreenShake shake;
            public BurstFx burst;
            public DamageFlashView flash;
        }

        Rig MakeRig()
        {
            var rig = new Rig
            {
                hooks = Make<FeedbackHooks>("TestFeedback"),
                shake = Make<ScreenShake>("TestShake"),
                burst = Make<BurstFx>("TestBurst"),
                flash = MakeFlash()
            };
            rig.hooks.Shake = rig.shake;
            rig.hooks.Burst = rig.burst;
            rig.hooks.Flash = rig.flash;
            rig.hooks.PlayerHealth = health;
            rig.hooks.Experience = exp;
            rig.hooks.Weapons = wc;
            return rig;
        }

        DamageFlashView MakeFlash()
        {
            var go = new GameObject("TestFlash", typeof(RectTransform), typeof(Image));
            toDestroy.Add(go);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 0f, 0f, 0f);
            var flash = go.AddComponent<DamageFlashView>();
            flash.Image = img;
            return flash;
        }

        // ───────────────────────── 화면 흔들림 ─────────────────────────

        [Test]
        public void Shake_TraumaIsClamped_DecaysToZero_AndOffsetIsBounded()
        {
            var shake = Make<ScreenShake>("Shake");
            shake.AddTrauma(0.7f);
            shake.AddTrauma(0.7f);
            Assert.AreEqual(1f, shake.Trauma, 0.0001f, "충격량은 1을 넘지 않음");

            float maxSeen = 0f;
            for (int i = 0; i < 200 && shake.Trauma > 0f; i++)
            {
                shake.Tick(0.016f);
                maxSeen = Mathf.Max(maxSeen, shake.Offset.magnitude);
                Assert.LessOrEqual(Mathf.Abs(shake.Offset.x), shake.MaxOffset + 0.0001f);
                Assert.LessOrEqual(Mathf.Abs(shake.Offset.y), shake.MaxOffset + 0.0001f);
            }
            Assert.Greater(maxSeen, 0.05f, "강한 충격은 실제로 화면을 움직임");

            for (int i = 0; i < 400; i++) shake.Tick(0.016f);
            Assert.AreEqual(0f, shake.Trauma, 0.0001f);
            Assert.AreEqual(Vector2.zero, shake.Offset, "충격이 사라지면 오프셋도 0");
        }

        [Test]
        public void Shake_StrongImpactMovesMuchMoreThanWeak()
        {
            // 같은 시드/같은 시각이면 잡음값이 같으므로 흔들림 폭은 충격량의 제곱 비율로 정확히 나뉜다
            // (dt = 0 이면 충격량이 줄지 않아 비교가 정확하다)
            Vector2 OffsetAt(float trauma)
            {
                UnityEngine.Random.InitState(4242);
                var s = Make<ScreenShake>("S");
                s.Tick(0.3f);          // 같은 시각으로 맞춤 (충격량 0 이라 영향 없음)
                s.AddTrauma(trauma);
                s.Tick(0f);
                return s.Offset;
            }

            Vector2 strong = OffsetAt(1f);
            Vector2 weak = OffsetAt(0.3f);

            Assert.AreEqual(strong.x, weak.x / 0.09f, 0.001f, "폭 = 충격량² × 잡음");
            Assert.AreEqual(strong.y, weak.y / 0.09f, 0.001f);
            Assert.GreaterOrEqual(strong.magnitude, weak.magnitude, "강한 충격이 더 크게 흔든다");
        }

        [Test]
        public void Shake_Disabled_IgnoresTrauma_AndStopsImmediately_AndIsSaved()
        {
            var shake = Make<ScreenShake>("Shake");
            shake.AddTrauma(1f);
            shake.Tick(0.016f);
            Assert.AreNotEqual(Vector2.zero, shake.Offset);

            shake.SetEnabled(false);
            Assert.IsFalse(shake.Enabled);
            Assert.AreEqual(0f, shake.Trauma, "끄면 남은 충격량도 사라짐");
            Assert.AreEqual(Vector2.zero, shake.Offset);

            shake.AddTrauma(1f);
            Assert.AreEqual(0f, shake.Trauma, "꺼져 있으면 충격을 받지 않음");

            SaveSystem.ResetCache();
            Assert.IsFalse(SaveSystem.Load().screenShake, "설정이 저장됨");

            shake.SetEnabled(true);
            shake.AddTrauma(0.5f);
            Assert.AreEqual(0.5f, shake.Trauma, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Shake_LoadsDisabledSettingFromSave()
        {
            SaveSystem.Current.screenShake = false;
            var go = new GameObject("Shake");
            toDestroy.Add(go);
            var shake = go.AddComponent<ScreenShake>(); // Awake 에서 설정을 읽음
            yield return null;

            Assert.IsFalse(shake.Enabled);
            shake.AddTrauma(1f);
            Assert.AreEqual(0f, shake.Trauma);
        }

        [Test]
        public void Save_ScreenShake_DefaultsOn_ForOldSaveFiles()
        {
            Assert.IsTrue(new SaveData().screenShake);
            File.WriteAllText(savePath, "{\"gold\":5}");
            Assert.IsTrue(SaveSystem.Load().screenShake, "옛 저장 파일은 기본값(켬)");
        }

        // ───────────────────────── 카메라 ─────────────────────────

        [UnityTest]
        public IEnumerator Camera_ShakesAroundFollowPosition_AndReturnsExactly_WithoutDrift()
        {
            var target = new GameObject("Target");
            toDestroy.Add(target);
            target.transform.position = new Vector3(3f, 2f, 0f);

            camGo = new GameObject("TestCam", typeof(Camera), typeof(ScreenShake), typeof(CameraFollow));
            var follow = camGo.GetComponent<CameraFollow>();
            var shake = camGo.GetComponent<ScreenShake>();
            follow.Target = target.transform;
            follow.SnapToTarget();
            yield return null;
            Assert.AreEqual(3f, camGo.transform.position.x, 0.001f);

            shake.AddTrauma(1f);
            float maxDeviation = 0f;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                Vector3 p = camGo.transform.position;
                maxDeviation = Mathf.Max(maxDeviation, Mathf.Abs(p.x - 3f), Mathf.Abs(p.y - 2f));
                Assert.AreEqual(-10f, p.z, 0.0001f, "Z 는 흔들리지 않음");
            }
            Assert.Greater(maxDeviation, 0.02f, "충격을 받으면 추적 위치에서 벗어남");
            Assert.LessOrEqual(maxDeviation, shake.MaxOffset + 0.05f);

            yield return new WaitForSeconds(1.2f); // 충격 소멸
            Assert.AreEqual(0f, shake.Trauma, 0.0001f);
            Vector3 end = camGo.transform.position;
            Assert.AreEqual(3f, end.x, 0.01f, "흔들림이 끝나면 정확히 추적 위치로 돌아옴 (누적 어긋남 없음)");
            Assert.AreEqual(2f, end.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator Camera_RespectsExternalTeleport()
        {
            var target = new GameObject("Target");
            toDestroy.Add(target);
            camGo = new GameObject("TestCam", typeof(Camera), typeof(CameraFollow));
            var follow = camGo.GetComponent<CameraFollow>();
            follow.Target = target.transform;
            follow.SnapToTarget();
            yield return null;

            camGo.transform.position = new Vector3(50f, 40f, -10f); // 다른 코드가 직접 옮김
            target.transform.position = new Vector3(50f, 40f, 0f);
            yield return null;
            yield return null;

            Assert.AreEqual(50f, camGo.transform.position.x, 0.5f, "예전 추적 위치로 되돌아가지 않음");
            Assert.AreEqual(40f, camGo.transform.position.y, 0.5f);
        }

        // ───────────────────────── 입자 ─────────────────────────

        static int ActiveChildren(Transform t)
        {
            int n = 0;
            foreach (Transform c in t) if (c.gameObject.activeSelf) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator Burst_SpawnsParticles_FadeAway_AndReusesInstances()
        {
            var burst = Make<BurstFx>("Burst");
            yield return null;

            burst.Burst(Vector2.zero, 10, Color.red, 4f, 0.2f, 1f);
            Assert.AreEqual(10, burst.ActiveCount);
            Assert.AreEqual(10, ActiveChildren(burst.transform));
            Assert.AreEqual(10, burst.CreatedCount);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, burst.ActiveCount, "지속 시간이 지나면 모두 사라짐");
            Assert.AreEqual(0, ActiveChildren(burst.transform));

            burst.Burst(Vector2.zero, 10, Color.red, 4f, 0.2f, 1f);
            Assert.AreEqual(10, burst.CreatedCount, "끝난 입자 오브젝트를 재사용");
        }

        [UnityTest]
        public IEnumerator Burst_ParticlesMoveOutward_AndAreCappedAtMax()
        {
            var burst = Make<BurstFx>("Burst");
            yield return null;

            burst.Burst(new Vector2(5f, 5f), 20, Color.white, 6f, 1f, 1f);
            yield return new WaitForSeconds(0.15f);
            float avg = 0f; int n = 0;
            foreach (Transform c in burst.transform)
            {
                if (!c.gameObject.activeSelf) continue;
                avg += Vector2.Distance(c.position, new Vector2(5f, 5f)); n++;
            }
            Assert.Greater(avg / n, 0.1f, "입자가 중심에서 퍼져 나감");

            burst.Burst(Vector2.zero, 5000, Color.white, 1f, 1f, 1f);
            Assert.LessOrEqual(burst.ActiveCount, BurstFx.MaxParticles, "상한을 넘지 않음");
            Assert.LessOrEqual(burst.CreatedCount, BurstFx.MaxParticles);
            Assert.DoesNotThrow(() => burst.Burst(Vector2.zero, 5, Color.white, 1f, 1f, 1f), "상한 도달 후에도 안전");
        }

        [UnityTest]
        public IEnumerator Ring_SpreadsEvenly_SoCentroidStaysAtCenter()
        {
            var burst = Make<BurstFx>("Burst");
            yield return null;

            burst.Ring(new Vector2(2f, -3f), 12, Color.yellow, 6f, 1f, 1f);
            yield return null;
            yield return new WaitForSeconds(0.1f);

            Vector2 sum = Vector2.zero; int n = 0; float minDist = float.MaxValue;
            foreach (Transform c in burst.transform)
            {
                if (!c.gameObject.activeSelf) continue;
                sum += (Vector2)c.position; n++;
                minDist = Mathf.Min(minDist, Vector2.Distance(c.position, new Vector2(2f, -3f)));
            }
            Vector2 centroid = sum / n;

            Assert.AreEqual(12, n);
            Assert.AreEqual(2f, centroid.x, 0.05f, "고르게 퍼지므로 무게중심은 중심");
            Assert.AreEqual(-3f, centroid.y, 0.05f);
            Assert.Greater(minDist, 0.1f, "모든 입자가 바깥으로 이동");
        }

        // ───────────────────────── 피격 번쩍임 ─────────────────────────

        [UnityTest]
        public IEnumerator Flash_SetsAlpha_DecaysToZero_AndKeepsStrongerValue()
        {
            var flash = MakeFlash();
            yield return null;

            flash.Flash(1f);
            Assert.AreEqual(1f, flash.Strength, 0.0001f);
            Assert.Greater(flash.Image.color.a, 0.2f);

            flash.Flash(0.2f);
            Assert.AreEqual(1f, flash.Strength, 0.0001f, "더 약한 번쩍임이 강한 것을 덮어쓰지 않음");

            flash.Flash(5f);
            Assert.AreEqual(1f, flash.Strength, 0.0001f, "범위 보정");

            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0f, flash.Strength, 0.0001f);
            Assert.AreEqual(0f, flash.Image.color.a, 0.0001f);
        }

        // ───────────────────────── 데미지 숫자 ─────────────────────────

        [Test]
        public void DamageNumbers_BigHitsAreLargerAndRedder()
        {
            var baseColor = new Color(1f, 0.92f, 0.3f);
            DamageNumberSpawner.GetStyle(5f, baseColor, out var c1, out float s1);
            DamageNumberSpawner.GetStyle(20f, baseColor, out var c2, out float s2);
            DamageNumberSpawner.GetStyle(60f, baseColor, out var c3, out float s3);

            Assert.AreEqual(baseColor, c1, "작은 피해는 기본 색");
            Assert.AreEqual(1f, s1);
            Assert.Greater(s2, s1);
            Assert.Greater(s3, s2);
            Assert.Less(c3.g, c2.g, "클수록 붉어짐 (녹색 성분이 줄어듦)");
            Assert.AreNotEqual(c1, c2);

            DamageNumberSpawner.GetStyle(12f, baseColor, out _, out float edge);
            Assert.Greater(edge, 1f, "12 이상부터 강조");
            DamageNumberSpawner.GetStyle(11.9f, baseColor, out _, out float below);
            Assert.AreEqual(1f, below);
        }

        // ───────────────────────── 이벤트 → 연출 ─────────────────────────

        [Test]
        public void KillWeight_ByEnemyHealth()
        {
            Assert.AreEqual(KillWeight.Normal, FeedbackHooks.GetKillWeight(null));
            Assert.AreEqual(KillWeight.Normal, FeedbackHooks.GetKillWeight(Dummy(10)));
            Assert.AreEqual(KillWeight.Normal, FeedbackHooks.GetKillWeight(Dummy(59)));
            Assert.AreEqual(KillWeight.Heavy, FeedbackHooks.GetKillWeight(Dummy(60)), "정예");
            Assert.AreEqual(KillWeight.Heavy, FeedbackHooks.GetKillWeight(Dummy(120)), "장수");
            Assert.AreEqual(KillWeight.Boss, FeedbackHooks.GetKillWeight(Dummy(500)), "보스");
        }

        [UnityTest]
        public IEnumerator Hooks_Hit_SpawnsSparks_KillSpawnsDebris_WithoutShakeForNormalEnemies()
        {
            BuildWorld();
            var rig = MakeRig();
            yield return null;

            var survivor = spawner.SpawnAt(Dummy(100), new Vector2(8f, 0f));
            survivor.TakeDamage(5f);
            Assert.AreEqual(2, rig.burst.ActiveCount, "타격 불꽃 2개");

            var victim = spawner.SpawnAt(Dummy(5, Color.green), new Vector2(8f, 3f));
            victim.TakeDamage(100f);
            Assert.AreEqual(2 + 6, rig.burst.ActiveCount, "처치 파편 6개 (처치 타격은 불꽃을 따로 만들지 않음)");
            Assert.AreEqual(0f, rig.shake.Trauma, "일반 적 처치로는 화면이 흔들리지 않음");
        }

        [UnityTest]
        public IEnumerator Hooks_HeavyAndBossKills_BiggerBurstAndShake()
        {
            BuildWorld();
            var rig = MakeRig();
            yield return null;

            spawner.SpawnAt(Dummy(60), new Vector2(8f, 0f)).TakeDamage(1000f);
            Assert.AreEqual(16, rig.burst.ActiveCount);
            Assert.AreEqual(0.3f, rig.shake.Trauma, 0.0001f);

            spawner.SpawnAt(Dummy(500), new Vector2(8f, 3f)).TakeDamage(1000f);
            Assert.AreEqual(16 + 36, rig.burst.ActiveCount);
            Assert.AreEqual(1f, rig.shake.Trauma, 0.0001f, "0.3 + 0.7 = 1 (상한)");
        }

        [UnityTest]
        public IEnumerator Hooks_PlayerHurt_FlashesShakesAndBleeds_ButHealingDoesNot()
        {
            BuildWorld();
            var rig = MakeRig();
            yield return null;

            health.TakeDamage(20f);
            Assert.Greater(rig.flash.Strength, 0.4f, "피격 시 화면이 붉게 번쩍임");
            Assert.AreEqual(0.5f, rig.shake.Trauma, 0.0001f);
            Assert.AreEqual(8, rig.burst.ActiveCount);

            yield return new WaitForSeconds(1.5f); // 연출이 가라앉길 기다림
            Assert.AreEqual(0f, rig.flash.Strength, 0.0001f);

            health.Heal(10f);
            Assert.AreEqual(0f, rig.flash.Strength, 0.0001f, "회복은 번쩍이지 않음");
            Assert.AreEqual(0f, rig.shake.Trauma, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Hooks_BiggerDamageFlashesStronger()
        {
            BuildWorld();
            var rig = MakeRig();
            yield return null;

            health.TakeDamage(5f);
            float small = rig.flash.Strength;
            yield return new WaitForSeconds(1.5f);

            health.TakeDamage(40f);
            Assert.Greater(rig.flash.Strength, small, "많이 맞을수록 더 강하게 번쩍임");
        }

        [UnityTest]
        public IEnumerator Hooks_LevelUpAndEvolve_PlayGoldRings()
        {
            BuildWorld();
            var rig = MakeRig();
            yield return null;

            exp.AddExp(exp.ToNext);
            Assert.AreEqual(16, rig.burst.ActiveCount, "레벨업 링");

            var a = ScriptableObject.CreateInstance<WeaponData>();
            a.displayName = "A"; a.type = WeaponType.Arrow; a.cooldown = 10f; toDestroy.Add(a);
            var b = ScriptableObject.CreateInstance<WeaponData>();
            b.displayName = "B"; b.type = WeaponType.Arrow; b.cooldown = 10f; toDestroy.Add(b);
            wc.AddWeapon(a);
            wc.Evolve(a, b);

            Assert.AreEqual(16 + 32, rig.burst.ActiveCount, "진화 링");
            Assert.AreEqual(0.5f, rig.shake.Trauma, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Hooks_DisabledOrUnwired_DoNothing()
        {
            BuildWorld();
            var rig = MakeRig();
            yield return null;

            rig.hooks.enabled = false;
            yield return null;
            spawner.SpawnAt(Dummy(5), new Vector2(8f, 0f)).TakeDamage(100f);
            health.TakeDamage(10f);
            Assert.AreEqual(0, rig.burst.ActiveCount, "꺼진 동안은 연출 없음");

            rig.hooks.enabled = true;
            rig.hooks.Burst = null; // 연결이 빠져도 예외 없이 동작
            rig.hooks.Shake = null;
            rig.hooks.Flash = null;
            yield return null;
            Assert.DoesNotThrow(() =>
            {
                spawner.SpawnAt(Dummy(5), new Vector2(8f, 3f)).TakeDamage(100f);
                health.TakeDamage(10f);
            });
        }
    }
}
