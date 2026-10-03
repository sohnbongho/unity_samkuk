using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Pickups;
using Samkuk.Player;
using Samkuk.UI;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    public class ProgressionTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string GemPrefabPath = "Assets/Prefabs/ExpGem.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager enemyManager;
        EnemySpawner spawner;
        ExpGemManager gems;
        PlayerHealth health;
        PlayerStats stats;
        PlayerExperience exp;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        // ───────────────────────── 준비/정리 ─────────────────────────

        [SetUp]
        public void SetUp()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            var gemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GemPrefabPath)?.GetComponent<ExpGem>();
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");
            Assert.IsNotNull(gemPrefab, "ExpGem 프리팹이 없습니다. Samkuk > Step 6 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            stats = playerGo.AddComponent<PlayerStats>(); // PlayerHealth/Experience.Awake가 참조하므로 먼저 추가
            health = playerGo.AddComponent<PlayerHealth>();
            exp = playerGo.AddComponent<PlayerExperience>();

            camGo = new GameObject("TestCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.aspect = 16f / 9f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            root = new GameObject("TestSystems");
            root.SetActive(false);
            enemyManager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            gems = root.AddComponent<ExpGemManager>();
            enemyManager.Target = playerGo.transform;
            spawner.Manager = enemyManager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = enemyPrefab;
            spawner.autoSpawn = false;
            gems.GemPrefab = gemPrefab;
            gems.Target = playerGo.transform;
            gems.Experience = exp;
            root.SetActive(true);
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

        PassiveData MakePassive(PassiveType type, float value, int maxLevel = 5, string name = null)
        {
            var p = ScriptableObject.CreateInstance<PassiveData>();
            p.type = type;
            p.valuePerLevel = value;
            p.maxLevel = maxLevel;
            p.displayName = name ?? type.ToString();
            toDestroy.Add(p);
            return p;
        }

        WeaponData MakeWeapon(WeaponType type, string name)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.type = type;
            w.displayName = name;
            w.damage = 10f; w.cooldown = 1f; w.range = 8f; w.projectileSpeed = 10f; w.duration = 1f; w.size = 0.3f;
            toDestroy.Add(w);
            return w;
        }

        EnemyData MakeEnemy(int hp, int expReward)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp; d.moveSpeed = 0f; d.contactDamage = 0; d.expReward = expReward; d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        WeaponController AddWeaponController()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<Projectile>();
            Assert.IsNotNull(projectile, "Projectile 프리팹이 없습니다. Step 5 를 먼저 실행하세요.");
            var wc = playerGo.AddComponent<WeaponController>();
            wc.ProjectilePrefab = projectile;
            wc.EnemyManager = enemyManager;
            return wc;
        }

        UpgradeCatalog MakeCatalog(IEnumerable<WeaponData> weapons, IEnumerable<PassiveData> passives)
        {
            var c = ScriptableObject.CreateInstance<UpgradeCatalog>();
            c.weapons.AddRange(weapons);
            c.passives.AddRange(passives);
            toDestroy.Add(c);
            return c;
        }

        // ───────────────────────── 경험치/레벨 ─────────────────────────

        [Test]
        public void Experience_LevelsUp_AndCarriesOverRemainder()
        {
            int levelUps = 0;
            exp.LevelUp += _ => levelUps++;

            int need = PlayerExperience.RequiredFor(1);
            exp.AddExp(need + 2);

            Assert.AreEqual(2, exp.Level);
            Assert.AreEqual(2, exp.Current, "남은 경험치는 이월");
            Assert.AreEqual(1, levelUps);
        }

        [Test]
        public void Experience_MultipleLevelsAtOnce_FiresLevelUpForEach()
        {
            var levels = new List<int>();
            exp.LevelUp += lv => levels.Add(lv);

            exp.AddExp(100);

            Assert.GreaterOrEqual(levels.Count, 3);
            for (int i = 0; i < levels.Count; i++) Assert.AreEqual(i + 2, levels[i], "레벨이 하나씩 순서대로");
            Assert.Less(exp.Current, exp.ToNext);
        }

        [Test]
        public void Experience_IgnoresNonPositive()
        {
            Assert.AreEqual(0, exp.AddExp(0f));
            Assert.AreEqual(0, exp.AddExp(-3f));
            Assert.AreEqual(1, exp.Level);
            Assert.AreEqual(0, exp.Current);
        }

        [Test]
        public void Experience_AppliesExpGainPassive()
        {
            stats.AddPassive(MakePassive(PassiveType.ExpGain, 0.5f));
            Assert.AreEqual(3, exp.AddExp(2), "2 × 1.5 = 3");
            Assert.AreEqual(3, exp.Current);
        }

        // ───────────────────────── 스탯/패시브 ─────────────────────────

        [Test]
        public void Stats_Passives_StackByLevel_AndStopAtMax()
        {
            var dmg = MakePassive(PassiveType.Damage, 0.15f, maxLevel: 2);

            Assert.AreEqual(1f, stats.DamageMultiplier, 0.0001f);
            Assert.IsTrue(stats.AddPassive(dmg));
            Assert.IsTrue(stats.AddPassive(dmg));
            Assert.AreEqual(1.3f, stats.DamageMultiplier, 0.0001f);
            Assert.AreEqual(2, stats.GetLevel(dmg));

            Assert.IsFalse(stats.CanUpgrade(dmg));
            Assert.IsFalse(stats.AddPassive(dmg), "최대 레벨 초과 불가");
            Assert.AreEqual(1.3f, stats.DamageMultiplier, 0.0001f);
        }

        [Test]
        public void Stats_CooldownMultiplier_HasFloor()
        {
            stats.AddPassive(MakePassive(PassiveType.Cooldown, 0.9f));
            Assert.AreEqual(0.3f, stats.CooldownMultiplier, 0.0001f);
        }

        [Test]
        public void Stats_ChangedEvent_FiresOnAdd()
        {
            int fired = 0;
            stats.Changed += () => fired++;
            stats.AddPassive(MakePassive(PassiveType.MoveSpeed, 0.1f));
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void MaxHpPassive_IncreasesMax_AndHealsByDifference()
        {
            health.TakeDamage(50f);
            Assert.AreEqual(50f, health.Current, 0.001f);

            stats.AddPassive(MakePassive(PassiveType.MaxHp, 20f));

            Assert.AreEqual(120f, health.Max, 0.001f);
            Assert.AreEqual(70f, health.Current, 0.001f, "최대 체력이 늘어난 만큼 회복");
        }

        [UnityTest]
        public IEnumerator RegenPassive_HealsOverTime()
        {
            yield return null;
            health.TakeDamage(50f);
            stats.AddPassive(MakePassive(PassiveType.Regen, 4f));

            yield return new WaitForSeconds(1.2f);

            Assert.Greater(health.Current, 53f, $"초당 4 재생 (현재 {health.Current})");
            Assert.LessOrEqual(health.Current, health.Max);
        }

        [UnityTest]
        public IEnumerator DamagePassive_RaisesWeaponDamage()
        {
            var wc = AddWeaponController();
            var data = MakeWeapon(WeaponType.Arrow, "활");
            data.damage = 10f;
            var w = wc.AddWeapon(data);
            yield return null;

            Assert.AreEqual(10f, w.Damage, 0.001f);
            stats.AddPassive(MakePassive(PassiveType.Damage, 0.5f));
            Assert.AreEqual(15f, w.Damage, 0.001f);

            stats.AddPassive(MakePassive(PassiveType.Cooldown, 0.2f));
            Assert.AreEqual(0.8f, w.Cooldown, 0.001f);
        }

        // ───────────────────────── 경험치 보석 ─────────────────────────

        [UnityTest]
        public IEnumerator Gem_NearPlayer_IsAttractedAndCollected()
        {
            yield return null;
            var gem = gems.Spawn(new Vector2(1.5f, 0f), 3);
            Assert.IsNotNull(gem);
            Assert.AreEqual(1, gems.ActiveCount);

            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(0, gems.ActiveCount, "획득되어 사라짐");
            Assert.AreEqual(3, exp.Current);
        }

        [UnityTest]
        public IEnumerator Gem_FarFromPlayer_StaysPut()
        {
            yield return null;
            var gem = gems.Spawn(new Vector2(10f, 0f), 3);

            yield return new WaitForSeconds(0.5f);

            Assert.IsFalse(gem.Attracted);
            Assert.AreEqual(10f, gem.transform.position.x, 0.001f);
            Assert.AreEqual(1, gems.ActiveCount);
            Assert.AreEqual(0, exp.Current);
        }

        [UnityTest]
        public IEnumerator Gem_PickupRadiusPassive_ExtendsMagnet()
        {
            yield return null;
            var gem = gems.Spawn(new Vector2(3.0f, 0f), 2); // 기본 범위(2.2) 밖
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(gem.Attracted, "기본 범위 밖에서는 끌려오지 않음");

            stats.AddPassive(MakePassive(PassiveType.PickupRadius, 0.5f)); // 2.2 → 3.3
            yield return new WaitForSeconds(1.2f);

            Assert.AreEqual(0, gems.ActiveCount);
            Assert.AreEqual(2, exp.Current);
        }

        [UnityTest]
        public IEnumerator Gem_OverCap_GrantsExpImmediately()
        {
            yield return null;
            gems.MaxGems = 2;

            for (int i = 0; i < 5; i++) gems.Spawn(new Vector2(10f + i, 0f), 1);

            Assert.AreEqual(2, gems.ActiveCount);
            Assert.AreEqual(3, exp.Current, "한도를 넘은 3개는 즉시 지급");
        }

        [UnityTest]
        public IEnumerator Gem_PoolReusesInstances()
        {
            yield return null;
            gems.Spawn(new Vector2(1f, 0f), 1);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, gems.ActiveCount);
            int created = gems.PooledTotal;

            gems.Spawn(new Vector2(1f, 0f), 1);
            Assert.AreEqual(created, gems.PooledTotal, "수확한 보석 인스턴스를 재사용");
        }

        [UnityTest]
        public IEnumerator Gem_DroppedWhenEnemyDies_WithRewardValue()
        {
            yield return null;
            var data = MakeEnemy(hp: 5, expReward: 4);
            var enemy = spawner.SpawnAt(data, new Vector2(8f, 0f)); // 자석 범위 밖에서 죽임

            enemy.TakeDamage(10f);

            Assert.AreEqual(1, gems.ActiveCount);
            yield return null;

            // 매니저의 활성 보석을 찾아 값 확인
            var found = gems.GetComponentsInChildren<ExpGem>(false);
            Assert.AreEqual(1, found.Length);
            Assert.AreEqual(4, found[0].Value);
            Assert.AreEqual(8f, found[0].transform.position.x, 0.2f, "죽은 위치에 떨어짐");
        }

        // ───────────────────────── 업그레이드 생성기 ─────────────────────────

        [Test]
        public void Generator_ReturnsThreeDistinctOptions()
        {
            var wc = AddWeaponController();
            var bow = MakeWeapon(WeaponType.Arrow, "활");
            wc.AddWeapon(bow);
            var catalog = MakeCatalog(
                new[] { bow, MakeWeapon(WeaponType.Slash, "검"), MakeWeapon(WeaponType.Orbit, "도끼") },
                new[] { MakePassive(PassiveType.Damage, 0.1f, name: "A"), MakePassive(PassiveType.MoveSpeed, 0.1f, name: "B") });

            for (int trial = 0; trial < 20; trial++)
            {
                var options = UpgradeGenerator.Generate(catalog, wc, stats, health);
                Assert.AreEqual(3, options.Count);
                Assert.AreEqual(3, new HashSet<string>(options.ConvertAll(o => o.Title)).Count, "중복 없음");
            }
        }

        [Test]
        public void Generator_ExcludesOwnedWeaponsFromNew_AndMaxedFromLevelUp()
        {
            var wc = AddWeaponController();
            var bow = MakeWeapon(WeaponType.Arrow, "활");
            bow.maxLevel = 1; // 이미 최대 레벨
            wc.AddWeapon(bow);
            var sword = MakeWeapon(WeaponType.Slash, "검");
            var catalog = MakeCatalog(new[] { bow, sword }, Array.Empty<PassiveData>());

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10);

            Assert.IsFalse(options.Exists(o => o.Weapon == bow), "보유 중이고 최대 레벨인 활은 나오지 않음");
            Assert.IsTrue(options.Exists(o => o.Kind == UpgradeKind.NewWeapon && o.Weapon == sword));
        }

        [Test]
        public void Generator_NoNewWeapons_WhenWeaponSlotsFull()
        {
            var wc = AddWeaponController();
            var a = MakeWeapon(WeaponType.Arrow, "A");
            var b = MakeWeapon(WeaponType.Slash, "B");
            var c = MakeWeapon(WeaponType.Orbit, "C");
            wc.AddWeapon(a); wc.AddWeapon(b);
            var catalog = MakeCatalog(new[] { a, b, c }, Array.Empty<PassiveData>());

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10, maxWeapons: 2);

            Assert.IsFalse(options.Exists(o => o.Kind == UpgradeKind.NewWeapon));
        }

        [Test]
        public void Generator_ExcludesMaxedPassives()
        {
            var wc = AddWeaponController();
            var maxed = MakePassive(PassiveType.Damage, 0.1f, maxLevel: 1, name: "maxed");
            var open = MakePassive(PassiveType.MoveSpeed, 0.1f, name: "open");
            stats.AddPassive(maxed);
            var catalog = MakeCatalog(Array.Empty<WeaponData>(), new[] { maxed, open });

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10);

            Assert.IsFalse(options.Exists(o => o.Passive == maxed));
            Assert.IsTrue(options.Exists(o => o.Passive == open));
        }

        [Test]
        public void Generator_FallsBackToHeal_WhenNoCandidates()
        {
            var wc = AddWeaponController();
            var catalog = MakeCatalog(Array.Empty<WeaponData>(), Array.Empty<PassiveData>());
            health.TakeDamage(60f);

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health);

            Assert.AreEqual(1, options.Count);
            Assert.AreEqual(UpgradeKind.Heal, options[0].Kind);
            options[0].Apply();
            Assert.AreEqual(70f, health.Current, 0.001f, "최대 체력의 30% 회복");
        }

        [Test]
        public void Generator_OptionsApplyTheirEffects()
        {
            var wc = AddWeaponController();
            var bow = MakeWeapon(WeaponType.Arrow, "활");
            wc.AddWeapon(bow);
            var sword = MakeWeapon(WeaponType.Slash, "검");
            var passive = MakePassive(PassiveType.Damage, 0.2f);
            var catalog = MakeCatalog(new[] { bow, sword }, new[] { passive });

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10);

            options.Find(o => o.Kind == UpgradeKind.WeaponLevelUp).Apply();
            Assert.AreEqual(2, wc.Weapons[0].Level);

            options.Find(o => o.Kind == UpgradeKind.NewWeapon).Apply();
            Assert.AreEqual(2, wc.Weapons.Count);

            options.Find(o => o.Kind == UpgradeKind.Passive).Apply();
            Assert.AreEqual(1, stats.GetLevel(passive));
        }

        // ───────────────────────── 레벨업 흐름 ─────────────────────────

        class FakeView : ILevelUpView
        {
            public int ShowCount;
            public int HideCount;
            public IReadOnlyList<UpgradeOption> LastOptions;
            public bool IsVisible { get; private set; }
            public void Show(IReadOnlyList<UpgradeOption> options, Action<int> onChosen)
            {
                ShowCount++;
                LastOptions = options;
                IsVisible = true;
            }
            public void Hide() { HideCount++; IsVisible = false; }
        }

        LevelUpController MakeController(FakeView view, UpgradeCatalog catalog = null)
        {
            var wc = AddWeaponController();
            wc.AddWeapon(MakeWeapon(WeaponType.Arrow, "활"));

            var go = new GameObject("TestLevelUp");
            toDestroy.Add(go);
            var c = go.AddComponent<LevelUpController>();
            c.Experience = exp;
            c.Weapons = wc;
            c.Stats = stats;
            c.Health = health;
            c.Catalog = catalog ?? MakeCatalog(
                new[] { MakeWeapon(WeaponType.Slash, "검") },
                new[] { MakePassive(PassiveType.Damage, 0.1f), MakePassive(PassiveType.MoveSpeed, 0.1f) });
            c.View = view;
            return c;
        }

        [Test]
        public void LevelUp_PausesGame_ShowsOptions_ThenResumesAfterChoice()
        {
            var view = new FakeView();
            var c = MakeController(view);
            UpgradeOption chosen = null;
            c.Chosen += o => chosen = o;

            exp.AddExp(exp.ToNext);

            Assert.IsTrue(c.IsShowing);
            Assert.AreEqual(1, view.ShowCount);
            Assert.AreEqual(3, view.LastOptions.Count);
            Assert.AreEqual(0f, Time.timeScale, "레벨업 중에는 게임 정지");

            c.Choose(1);

            Assert.IsFalse(c.IsShowing);
            Assert.IsNotNull(chosen);
            Assert.AreSame(view.LastOptions[1], chosen);
            Assert.AreEqual(1f, Time.timeScale, "선택 후 재개");
            Assert.AreEqual(1, view.HideCount);
        }

        [Test]
        public void LevelUp_MultipleLevels_AsksRepeatedly_ThenResumes()
        {
            var view = new FakeView();
            var c = MakeController(view);

            // 1→2, 2→3, 3→4 레벨에 필요한 경험치를 한꺼번에 → 3레벨업
            exp.AddExp(PlayerExperience.RequiredFor(1) + PlayerExperience.RequiredFor(2) + PlayerExperience.RequiredFor(3));

            Assert.AreEqual(3, c.PendingCount);
            int picks = 0;
            while (c.IsShowing && picks < 10)
            {
                Assert.AreEqual(0f, Time.timeScale);
                c.Choose(0);
                picks++;
            }

            Assert.AreEqual(3, picks);
            Assert.AreEqual(3, view.ShowCount);
            Assert.AreEqual(0, c.PendingCount);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void LevelUp_InvalidChoice_IsIgnored()
        {
            var view = new FakeView();
            var c = MakeController(view);

            c.Choose(0); // 표시 중이 아님
            exp.AddExp(exp.ToNext);
            c.Choose(-1);
            c.Choose(99);

            Assert.IsTrue(c.IsShowing, "잘못된 선택은 무시되고 계속 대기");
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void LevelUp_NotShown_WhenPlayerIsDead()
        {
            var view = new FakeView();
            var c = MakeController(view);
            health.TakeDamage(1000f);

            exp.AddExp(exp.ToNext);

            Assert.AreEqual(0, view.ShowCount);
            Assert.IsFalse(c.IsShowing);
            Assert.AreEqual(1f, Time.timeScale);
        }

        // ───────────────────────── 레벨업 UI ─────────────────────────

        [Test]
        public void LevelUpUI_ShowsOptionTexts_AndClickReportsIndex()
        {
            var uiGo = new GameObject("TestLevelUpUI");
            toDestroy.Add(uiGo);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(uiGo.transform);

            var cards = new UpgradeCardView[3];
            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject($"Card{i}", typeof(RectTransform));
                cardGo.transform.SetParent(panel.transform);
                var btn = cardGo.AddComponent<Button>();
                var title = new GameObject("T", typeof(RectTransform)).AddComponent<Text>();
                var desc = new GameObject("D", typeof(RectTransform)).AddComponent<Text>();
                title.transform.SetParent(cardGo.transform);
                desc.transform.SetParent(cardGo.transform);
                cards[i] = new UpgradeCardView { button = btn, title = title, description = desc };
            }

            var ui = uiGo.AddComponent<LevelUpUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel;
            var arr = so.FindProperty("cards");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = cards[i].button;
                el.FindPropertyRelative("title").objectReferenceValue = cards[i].title;
                el.FindPropertyRelative("description").objectReferenceValue = cards[i].description;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var options = new List<UpgradeOption>
            {
                new UpgradeOption { Title = "제목A", Description = "설명A" },
                new UpgradeOption { Title = "제목B", Description = "설명B" }
            };
            int picked = -1;
            ui.Show(options, i => picked = i);

            Assert.IsTrue(ui.IsVisible);
            Assert.AreEqual("제목A", cards[0].title.text);
            Assert.AreEqual("설명B", cards[1].description.text);
            Assert.IsTrue(cards[1].button.gameObject.activeSelf);
            Assert.IsFalse(cards[2].button.gameObject.activeSelf, "선택지가 2개면 세 번째 카드는 숨김");

            cards[1].button.onClick.Invoke();
            Assert.AreEqual(1, picked);

            ui.Hide();
            Assert.IsFalse(ui.IsVisible);
        }
    }
}
