using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>
    /// 장수별 무기 목록(Step 8-5): 레벨업의 새 무기 선택지는 고른 장수의 목록 안에서만 나오고,
    /// 목록이 비면 예전처럼 공용 무기 전부가 나온다. 에셋 검사는 Step 8-2 / Step 8 셋업 결과를 전제한다.
    /// </summary>
    public class HeroWeaponListTests
    {
        const string HeroCatalogPath = "Assets/ScriptableObjects/HeroCatalog.asset";
        const string UpgradeCatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";
        /// <summary>전용 무기 1 + 공용 목록. 보유 한도 4 에서 공용은 3개까지 얻으므로 조합이 생기려면 공용이 4개 이상이어야 한다.</summary>
        const int MinCommonWeapons = 4;

        GameObject playerGo;
        PlayerStats stats;
        PlayerHealth health;
        WeaponController wc;
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.SelectedHero = null;
            playerGo = new GameObject("TestPlayer");
            stats = playerGo.AddComponent<PlayerStats>();
            health = playerGo.AddComponent<PlayerHealth>();
            wc = playerGo.AddComponent<WeaponController>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameSession.SelectedHero = null;
            if (playerGo != null) Object.Destroy(playerGo);
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        WeaponData MakeWeapon(string name)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.displayName = name; w.type = WeaponType.Arrow; w.maxLevel = 8;
            w.damage = 10f; w.cooldown = 10f; w.range = 8f; w.size = 0.3f; w.duration = 1f; w.projectileSpeed = 10f;
            toDestroy.Add(w);
            return w;
        }

        UpgradeCatalog MakeCatalog(params WeaponData[] weapons)
        {
            var c = ScriptableObject.CreateInstance<UpgradeCatalog>();
            c.weapons.AddRange(weapons);
            // 새 무기만 뽑히게 가중치를 몰아 준다 (패시브 없음, 보유 무기 강화 0)
            c.weaponLevelUpWeight = 0f; c.passiveWeight = 0f; c.newWeaponWeight = 1f;
            toDestroy.Add(c);
            return c;
        }

        HeroData MakeHero(WeaponData exclusive, params WeaponData[] common)
        {
            var h = ScriptableObject.CreateInstance<HeroData>();
            h.displayName = "테스트 장수";
            h.startingWeapon = exclusive;
            h.weapons.AddRange(common);
            toDestroy.Add(h);
            return h;
        }

        static HashSet<WeaponData> NewWeaponsIn(List<UpgradeOption> options)
        {
            var set = new HashSet<WeaponData>();
            foreach (var o in options)
                if (o.Kind == UpgradeKind.NewWeapon) set.Add(o.Weapon);
            return set;
        }

        // ───────────────────────── 규칙 ─────────────────────────

        [Test]
        public void AllowsWeapon_EmptyOrNullList_AllowsEverything()
        {
            var w = MakeWeapon("활");
            Assert.IsTrue(UpgradeGenerator.AllowsWeapon(null, w));
            Assert.IsTrue(UpgradeGenerator.AllowsWeapon(new List<WeaponData>(), w));
        }

        [Test]
        public void AllowsWeapon_ListGiven_OnlyListedWeapons()
        {
            var inList = MakeWeapon("활");
            var outside = MakeWeapon("화계");
            var list = new List<WeaponData> { inList };
            Assert.IsTrue(UpgradeGenerator.AllowsWeapon(list, inList));
            Assert.IsFalse(UpgradeGenerator.AllowsWeapon(list, outside));
        }

        [Test]
        public void Generate_WithHeroList_NewWeaponsOnlyFromList()
        {
            var a = MakeWeapon("활"); var b = MakeWeapon("쇠뇌"); var c = MakeWeapon("화계"); var d = MakeWeapon("뇌격");
            var catalog = MakeCatalog(a, b, c, d);
            var hero = MakeHero(MakeWeapon("쌍고검"), a, b);

            // 후보가 목록(2개)뿐이라 10개를 달라고 해도 2개 + 체력 회복만 나온다
            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10, maxWeapons: 4, allowedWeapons: hero.weapons);
            var seen = NewWeaponsIn(options);

            CollectionAssert.AreEquivalent(new[] { a, b }, seen);
            Assert.IsFalse(seen.Contains(c));
            Assert.IsFalse(seen.Contains(d));
        }

        [Test]
        public void Generate_EmptyHeroList_AllCatalogWeapons()
        {
            var a = MakeWeapon("활"); var b = MakeWeapon("쇠뇌"); var c = MakeWeapon("화계");
            var catalog = MakeCatalog(a, b, c);
            var hero = MakeHero(MakeWeapon("쌍고검")); // 목록 없음 → 예전 동작

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10, maxWeapons: 4, allowedWeapons: hero.weapons);

            CollectionAssert.AreEquivalent(new[] { a, b, c }, NewWeaponsIn(options));
        }

        [Test]
        public void Generate_OwnedWeaponOutsideList_StillLevelsUp()
        {
            // 전용 무기는 목록에 없지만 들고 시작하므로 강화 선택지는 계속 나와야 한다
            var exclusive = MakeWeapon("쌍고검");
            var a = MakeWeapon("활");
            var catalog = MakeCatalog(a);
            catalog.weaponLevelUpWeight = 1f;
            var hero = MakeHero(exclusive, a);
            wc.AddWeapon(exclusive);

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10, maxWeapons: 4, allowedWeapons: hero.weapons);

            bool levelUpShown = false;
            foreach (var o in options)
                if (o.Kind == UpgradeKind.WeaponLevelUp && o.Weapon == exclusive) levelUpShown = true;
            Assert.IsTrue(levelUpShown, "전용 무기 강화가 목록 때문에 막히면 안 된다");
        }

        [Test]
        public void LevelUpController_UsesSelectedHeroList()
        {
            var a = MakeWeapon("활"); var b = MakeWeapon("화계");
            var catalog = MakeCatalog(a, b);
            var hero = MakeHero(MakeWeapon("쌍고검"), a);
            GameSession.SelectedHero = hero;

            var go = new GameObject("LevelUp");
            toDestroy.Add(go);
            var ctrl = go.AddComponent<LevelUpController>();
            ctrl.Weapons = wc; ctrl.Stats = stats; ctrl.Health = health; ctrl.Catalog = catalog;
            var view = new FakeView();
            ctrl.View = view;
            var exp = playerGo.AddComponent<PlayerExperience>();
            ctrl.Experience = exp;

            exp.AddExp(exp.ToNext);
            Assert.IsTrue(ctrl.IsShowing);
            var seen = NewWeaponsIn(new List<UpgradeOption>(ctrl.CurrentOptions));
            Assert.IsTrue(seen.Contains(a));
            Assert.IsFalse(seen.Contains(b), "고른 장수의 목록 밖 무기가 나오면 안 된다");

            // 정리: 남은 레벨업을 전부 소비해 timeScale 복구
            while (ctrl.IsShowing) ctrl.Choose(0);
        }

        class FakeView : UI.ILevelUpView
        {
            public bool IsVisible { get; private set; }
            public void Show(IReadOnlyList<UpgradeOption> options, System.Action<int> onChosen) => IsVisible = true;
            public void Hide() => IsVisible = false;
        }

        // ───────────────────────── 에셋 검사 (셋업 결과) ─────────────────────────

        [Test]
        public void Assets_EveryHeroHasEnoughCommonWeapons_AndNoExclusiveInList()
        {
            var heroes = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HeroCatalogPath);
            Assert.IsNotNull(heroes, "HeroCatalog 이 없습니다. Step 8 을 먼저 실행하세요.");

            var exclusives = new HashSet<WeaponData>();
            foreach (var h in heroes.heroes)
                if (h != null && h.startingWeapon != null) exclusives.Add(h.startingWeapon);

            foreach (var h in heroes.heroes)
            {
                Assert.IsNotNull(h);
                Assert.GreaterOrEqual(h.weapons.Count, MinCommonWeapons,
                    $"{h.displayName} 의 공용 무기 목록이 {MinCommonWeapons}개 미만입니다. Step 8 을 다시 실행하거나 Hero 에셋을 채우세요.");
                foreach (var w in h.weapons)
                {
                    Assert.IsNotNull(w, $"{h.displayName} 목록에 빈 칸이 있습니다");
                    Assert.IsFalse(exclusives.Contains(w), $"{h.displayName} 목록에 전용 무기 {w.displayName} 이 들어 있습니다");
                }
                Assert.AreEqual(h.weapons.Count, new HashSet<WeaponData>(h.weapons).Count, $"{h.displayName} 목록에 중복이 있습니다");
            }
        }

        [Test]
        public void Assets_EveryCommonWeaponIsInSomeHeroList()
        {
            var heroes = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HeroCatalogPath);
            var upgrades = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(UpgradeCatalogPath);
            Assert.IsNotNull(heroes, "HeroCatalog 이 없습니다. Step 8 을 먼저 실행하세요.");
            Assert.IsNotNull(upgrades, "UpgradeCatalog 이 없습니다. Step 6/8-2 를 먼저 실행하세요.");

            var used = new HashSet<WeaponData>();
            foreach (var h in heroes.heroes)
                if (h != null) used.UnionWith(h.weapons);

            foreach (var w in upgrades.weapons)
            {
                if (w == null) continue;
                Assert.IsTrue(used.Contains(w), $"공용 무기 {w.displayName} 을 쓰는 장수가 없습니다 (어떤 장수도 얻을 수 없음)");
            }
        }
    }
}
