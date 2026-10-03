using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Meta;
using Samkuk.Stages;
using Samkuk.UI;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    public class SoundTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

        static readonly SfxId[] AllSounds = BuildAllSounds();

        static SfxId[] BuildAllSounds()
        {
            var list = new List<SfxId>();
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                if (id != SfxId.None) list.Add(id);
            return list.ToArray();
        }

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
            savePath = Path.Combine(Application.temporaryCachePath, $"test_sound_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
            AudioManager.DestroyInstance();
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

        // ───────────────────────── 효과음 합성 ─────────────────────────

        [Test]
        public void Synth_EverySound_HasARecipe_AndNoneHasNone()
        {
            foreach (var id in AllSounds)
                Assert.IsTrue(SfxSynth.HasRecipe(id), $"{id}: 합성 레시피 누락");

            Assert.IsFalse(SfxSynth.HasRecipe(SfxId.None));
            Assert.AreEqual(0, SfxSynth.Generate(SfxId.None).Length);
            Assert.IsNull(SfxSynth.CreateClip(SfxId.None));
        }

        [Test]
        public void Synth_Samples_AreFinite_Audible_Bounded_AndFadeInAndOut()
        {
            foreach (var id in AllSounds)
            {
                var s = SfxSynth.Generate(id);
                float seconds = s.Length / (float)SfxSynth.SampleRate;
                Assert.GreaterOrEqual(seconds, 0.02f, $"{id}: 너무 짧음");
                Assert.LessOrEqual(seconds, 1.5f, $"{id}: 너무 김");

                float peak = 0f;
                foreach (var v in s)
                {
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), $"{id}: 유효하지 않은 샘플");
                    Assert.LessOrEqual(Mathf.Abs(v), 1f, $"{id}: 클리핑 범위 초과");
                    peak = Mathf.Max(peak, Mathf.Abs(v));
                }
                Assert.Greater(peak, 0.1f, $"{id}: 들리지 않을 만큼 작음");
                Assert.Less(Mathf.Abs(s[0]), 0.05f, $"{id}: 시작이 갑자기 튐 (클릭 잡음)");
                Assert.Less(Mathf.Abs(s[s.Length - 1]), 0.01f, $"{id}: 끝이 갑자기 끊김");
            }
        }

        [Test]
        public void Synth_IsDeterministic_AndEachSoundIsDistinct()
        {
            var seen = new List<float[]>();
            foreach (var id in AllSounds)
            {
                var a = SfxSynth.Generate(id);
                var b = SfxSynth.Generate(id);
                CollectionAssert.AreEqual(a, b, $"{id}: 같은 소리를 두 번 만들면 같아야 함");

                foreach (var other in seen)
                    Assert.IsFalse(other.Length == a.Length && System.Linq.Enumerable.SequenceEqual(other, a),
                        $"{id}: 다른 효과음과 완전히 같음");
                seen.Add(a);
            }
        }

        [Test]
        public void Synth_CreateClip_MatchesSamples()
        {
            var clip = SfxSynth.CreateClip(SfxId.LevelUp);
            toDestroy.Add(clip);

            Assert.IsNotNull(clip);
            Assert.AreEqual(SfxSynth.Generate(SfxId.LevelUp).Length, clip.samples);
            Assert.AreEqual(SfxSynth.SampleRate, clip.frequency);
            Assert.AreEqual(1, clip.channels);
        }

        [Test]
        public void WeaponSounds_EveryWeaponTypeIsMapped_OrbitIsSilent()
        {
            foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
            {
                var id = SfxMap.ForWeapon(type);
                if (type == WeaponType.Orbit) Assert.AreEqual(SfxId.None, id, "회전 무기는 소리를 내지 않음");
                else
                {
                    Assert.AreNotEqual(SfxId.None, id, $"{type}: 효과음 매핑 누락");
                    Assert.IsTrue(SfxSynth.HasRecipe(id));
                }
            }
        }

        // ───────────────────────── AudioManager ─────────────────────────

        [UnityTest]
        public IEnumerator Manager_IsCreatedOnDemand_AndPlays()
        {
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<AudioManager>());

            AudioManager.Play(SfxId.Click);
            yield return null;

            var m = AudioManager.Instance;
            Assert.IsNotNull(m, "처음 필요할 때 스스로 만들어짐");
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Click));
            Assert.AreSame(m, AudioManager.Instance, "하나만 존재");
        }

        [UnityTest]
        public IEnumerator Manager_RateLimitsSameSound_ButNotDifferentOnes()
        {
            yield return null;
            var m = AudioManager.Instance;

            Assert.IsTrue(m.TryPlay(SfxId.Hit));
            Assert.IsFalse(m.TryPlay(SfxId.Hit), "같은 소리는 최소 간격 안에 다시 재생되지 않음");
            Assert.IsTrue(m.TryPlay(SfxId.Kill), "다른 소리는 영향 없음");
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Hit));

            yield return new WaitForSecondsRealtime(AudioManager.MinInterval(SfxId.Hit) + 0.05f);

            Assert.IsTrue(m.TryPlay(SfxId.Hit), "간격이 지나면 다시 재생");
            Assert.AreEqual(2, m.GetPlayCount(SfxId.Hit));
        }

        [UnityTest]
        public IEnumerator Manager_IgnoresNone_AndPlaysNothingWhenMuted()
        {
            yield return null;
            var m = AudioManager.Instance;

            Assert.IsFalse(m.TryPlay(SfxId.None));

            m.SetSfxVolume(0f);
            Assert.IsFalse(m.TryPlay(SfxId.Click), "볼륨 0 이면 재생하지 않음");
            Assert.AreEqual(0, m.GetPlayCount(SfxId.Click));

            m.SetSfxVolume(1f);
            Assert.IsTrue(m.TryPlay(SfxId.Click));
        }

        [UnityTest]
        public IEnumerator Manager_PlaysEverySoundAtOnce_BeyondSourcePoolSize()
        {
            yield return null;
            var m = AudioManager.Instance;

            Assert.DoesNotThrow(() =>
            {
                foreach (var id in AllSounds) Assert.IsTrue(m.TryPlay(id), $"{id} 재생 실패");
            });
            Assert.Greater(AllSounds.Length, 8, "재생기 풀(8개)보다 많은 소리를 한꺼번에 재생해도 안전");
        }

        [UnityTest]
        public IEnumerator Manager_LoadsVolumeFromSave_AndSetVolumePersists()
        {
            SaveSystem.Current.sfxVolume = 0.35f;
            yield return null;
            var m = AudioManager.Instance;
            Assert.AreEqual(0.35f, m.SfxVolume, 0.0001f, "저장된 볼륨으로 시작");

            m.SetSfxVolume(5f);
            Assert.AreEqual(1f, m.SfxVolume, 0.0001f, "범위(0~1) 밖은 보정");
            m.SetSfxVolume(0.5f);

            SaveSystem.ResetCache();
            Assert.AreEqual(0.5f, SaveSystem.Load().sfxVolume, 0.0001f, "파일에 저장됨");
        }

        [UnityTest]
        public IEnumerator Manager_GeneratedClipIsCached_AndOverrideTakesPriority()
        {
            yield return null;
            var m = AudioManager.Instance;

            var generated = m.GetClip(SfxId.Hit);
            Assert.IsNotNull(generated);
            Assert.AreSame(generated, m.GetClip(SfxId.Hit), "합성은 한 번만");

            var custom = AudioClip.Create("custom", 100, 1, 22050, false);
            toDestroy.Add(custom);
            var so = new SerializedObject(m);
            var list = so.FindProperty("overrides");
            list.arraySize = 1;
            var el = list.GetArrayElementAtIndex(0);
            el.FindPropertyRelative("id").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(SfxId)), SfxId.Hit);
            el.FindPropertyRelative("clip").objectReferenceValue = custom;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreSame(custom, m.GetClip(SfxId.Hit), "오버라이드한 음원이 우선");
            Assert.AreSame(m.GetClip(SfxId.Kill), m.GetClip(SfxId.Kill), "다른 소리는 합성음 그대로");
        }

        [Test]
        public void Volume_Steps_CycleAndNameThemselves()
        {
            Assert.AreEqual(0.35f, AudioManager.NextVolumeStep(0f), 0.0001f);
            Assert.AreEqual(0.7f, AudioManager.NextVolumeStep(0.35f), 0.0001f);
            Assert.AreEqual(1f, AudioManager.NextVolumeStep(0.7f), 0.0001f);
            Assert.AreEqual(0f, AudioManager.NextVolumeStep(1f), 0.0001f, "끝에서 처음으로");
            Assert.AreEqual(1f, AudioManager.NextVolumeStep(0.6f), 0.0001f, "단계 사이 값은 가장 가까운 단계(0.7)의 다음");

            Assert.AreEqual("끔", AudioManager.VolumeName(0f));
            Assert.AreEqual("작게", AudioManager.VolumeName(0.35f));
            Assert.AreEqual("보통", AudioManager.VolumeName(0.7f));
            Assert.AreEqual("크게", AudioManager.VolumeName(1f));
            Assert.AreEqual("보통", AudioManager.VolumeName(0.6f));
        }

        [Test]
        public void MinInterval_IsPositive_AndFrequentSoundsHaveLongerSpacing()
        {
            foreach (var id in AllSounds)
                Assert.Greater(AudioManager.MinInterval(id), 0f, $"{id}: 최소 간격");
            Assert.Greater(AudioManager.MinInterval(SfxId.Hit), AudioManager.MinInterval(SfxId.Click),
                "수백 번 몰릴 수 있는 피격음이 UI 클릭음보다 간격이 길어야 함");
        }

        [Test]
        public void Save_SfxVolume_DefaultsAndIsSanitized()
        {
            Assert.AreEqual(0.7f, new SaveData().sfxVolume, 0.0001f);

            // 옛 저장 파일(sfxVolume 없음)은 기본값으로 읽힌다
            File.WriteAllText(savePath, "{\"gold\":12}");
            Assert.AreEqual(0.7f, SaveSystem.Load().sfxVolume, 0.0001f);

            File.WriteAllText(savePath, "{\"sfxVolume\":7.5}");
            Assert.AreEqual(1f, SaveSystem.Load().sfxVolume, 0.0001f, "범위 밖 값 보정");
            File.WriteAllText(savePath, "{\"sfxVolume\":-2}");
            Assert.AreEqual(0f, SaveSystem.Load().sfxVolume, 0.0001f);
        }

        // ───────────────────────── 게임 이벤트 → 소리 ─────────────────────────

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

        SfxHooks MakeHooks()
        {
            var go = new GameObject("TestHooks");
            toDestroy.Add(go);
            var hooks = go.AddComponent<SfxHooks>();
            hooks.PlayerHealth = health;
            hooks.Experience = exp;
            hooks.Weapons = wc;
            return hooks;
        }

        EnemyData Dummy(int hp)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp; d.moveSpeed = 0f; d.contactDamage = 0; d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        WeaponData MakeWeapon(string name, WeaponType type)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.displayName = name; w.type = type; w.damage = 5f; w.cooldown = 10f; w.range = 4f; w.size = 0.4f;
            w.duration = 0.5f; w.projectileSpeed = 10f;
            toDestroy.Add(w);
            return w;
        }

        [UnityTest]
        public IEnumerator Hooks_EnemyHit_PlaysHit_KillingBlowPlaysKillOnly()
        {
            BuildWorld();
            MakeHooks();
            yield return null;
            var m = AudioManager.Instance;

            var survivor = spawner.SpawnAt(Dummy(100), new Vector2(8f, 0f));
            survivor.TakeDamage(10f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Hit));
            Assert.AreEqual(0, m.GetPlayCount(SfxId.Kill));

            var victim = spawner.SpawnAt(Dummy(5), new Vector2(8f, 3f));
            victim.TakeDamage(100f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Kill));
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Hit), "처치하는 타격은 피격음을 따로 내지 않음");
        }

        [UnityTest]
        public IEnumerator Hooks_PlayerHurt_OnlyWhenHealthDrops()
        {
            BuildWorld();
            MakeHooks();
            yield return null;
            var m = AudioManager.Instance;

            health.TakeDamage(10f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.PlayerHurt), "첫 피격도 소리가 나야 함");

            health.Heal(5f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.PlayerHurt), "회복은 피격음 없음");

            health.TakeDamage(10000f); // 사망
            Assert.AreEqual(1, m.GetPlayCount(SfxId.PlayerHurt), "사망은 피격음 대신 종료음(게임 매니저가 있을 때)");
        }

        [UnityTest]
        public IEnumerator Hooks_LevelUp_Evolve_AndLevelUpChoiceClick()
        {
            BuildWorld();
            var hooks = MakeHooks();

            var bow = MakeWeapon("활", WeaponType.Arrow);
            var evolved = MakeWeapon("연노", WeaponType.Arrow);
            wc.AddWeapon(bow);

            var passive = ScriptableObject.CreateInstance<PassiveData>();
            passive.displayName = "패시브"; passive.type = PassiveType.Damage; passive.valuePerLevel = 0.1f; passive.maxLevel = 5;
            toDestroy.Add(passive);
            var catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
            catalog.passives.Add(passive);
            toDestroy.Add(catalog);

            var lvGo = new GameObject("TestLevelUp");
            toDestroy.Add(lvGo);
            var lv = lvGo.AddComponent<LevelUpController>();
            lv.Experience = exp; lv.Weapons = wc; lv.Stats = playerGo.GetComponent<PlayerStats>();
            lv.Health = health; lv.Catalog = catalog; lv.View = new FakeLevelUpView();
            hooks.LevelUp = lv;
            yield return null;
            var m = AudioManager.Instance;

            exp.AddExp(exp.ToNext);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.LevelUp));
            Assert.IsTrue(lv.IsShowing);

            lv.Choose(0);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Click), "선택하면 클릭음");

            wc.Evolve(bow, evolved);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Evolve));
        }

        class FakeLevelUpView : ILevelUpView
        {
            public bool IsVisible { get; private set; }
            public void Show(IReadOnlyList<UpgradeOption> options, Action<int> onChosen) { IsVisible = true; }
            public void Hide() { IsVisible = false; }
        }

        [UnityTest]
        public IEnumerator Hooks_GameEnd_PlaysDefeatOrVictory()
        {
            BuildWorld();
            var hooks = MakeHooks();

            var sys = new GameObject("TestGM");
            toDestroy.Add(sys);
            sys.SetActive(false);
            var stage = sys.AddComponent<StageController>();
            var stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.duration = 5f; toDestroy.Add(stageData);
            stage.Stage = stageData;
            var gm = sys.AddComponent<GameManager>();
            gm.PlayerHealth = health; gm.Stage = stage;
            sys.SetActive(true);
            hooks.Game = gm;
            yield return null;
            var m = AudioManager.Instance;

            stage.Tick(6f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Victory));
            Assert.AreEqual(0, m.GetPlayCount(SfxId.Defeat));
        }

        [UnityTest]
        public IEnumerator Hooks_PlayerDeath_PlaysDefeat()
        {
            BuildWorld();
            var hooks = MakeHooks();

            var sys = new GameObject("TestGM");
            toDestroy.Add(sys);
            sys.SetActive(false);
            var gm = sys.AddComponent<GameManager>();
            gm.PlayerHealth = health;
            sys.SetActive(true);
            hooks.Game = gm;
            yield return null;
            var m = AudioManager.Instance;

            health.TakeDamage(10000f);

            Assert.AreEqual(1, m.GetPlayCount(SfxId.Defeat));
            Assert.AreEqual(0, m.GetPlayCount(SfxId.PlayerHurt), "사망은 피격음 대신 패배음");
        }

        [UnityTest]
        public IEnumerator Hooks_DisabledOrRewired_StopListening()
        {
            BuildWorld();
            var hooks = MakeHooks();
            yield return null;
            var m = AudioManager.Instance;

            var a = spawner.SpawnAt(Dummy(100), new Vector2(8f, 0f));
            a.TakeDamage(1f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Hit));

            hooks.enabled = false;
            yield return new WaitForSecondsRealtime(0.1f);
            a.TakeDamage(1f);
            Assert.AreEqual(1, m.GetPlayCount(SfxId.Hit), "꺼진 뒤에는 소리를 내지 않음");

            hooks.enabled = true;
            yield return new WaitForSecondsRealtime(0.1f);
            a.TakeDamage(1f);
            Assert.AreEqual(2, m.GetPlayCount(SfxId.Hit), "다시 켜면 다시 연결됨");

            // 참조를 바꾸면 이전 대상 구독이 풀려야 한다 (중복 재생 없음)
            hooks.PlayerHealth = null;
            health.TakeDamage(10f);
            Assert.AreEqual(0, m.GetPlayCount(SfxId.PlayerHurt), "연결을 끊은 체력의 변화에는 반응하지 않음");
        }

        [UnityTest]
        public IEnumerator Weapons_PlayTheirAttackSound_WhenTheyFire()
        {
            BuildWorld();
            yield return null;
            spawner.SpawnAt(Dummy(1000), new Vector2(2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            wc.AddWeapon(MakeWeapon("창", WeaponType.Thrust));
            yield return new WaitForSeconds(0.3f);

            Assert.GreaterOrEqual(AudioManager.Instance.GetPlayCount(SfxId.Thrust), 1, "창 찌르기 발동 시 효과음");
        }

        [UnityTest]
        public IEnumerator PauseAndResultButtons_PlayClick()
        {
            yield return null;
            var go = new GameObject("TestPauseUI");
            toDestroy.Add(go);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform);
            UnityEngine.UI.Button MakeButton(string n)
            {
                var b = new GameObject(n, typeof(RectTransform)).AddComponent<UnityEngine.UI.Button>();
                b.transform.SetParent(panel.transform);
                return b;
            }
            var resume = MakeButton("Resume"); var restart = MakeButton("Restart"); var title = MakeButton("Title");
            var ui = go.AddComponent<PauseUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("titleButton").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool resumed = false;
            ui.Show(() => resumed = true, () => { }, () => { });
            resume.onClick.Invoke();

            Assert.IsTrue(resumed, "동작은 그대로 실행됨");
            Assert.AreEqual(1, AudioManager.Instance.GetPlayCount(SfxId.Click));
        }
    }
}
