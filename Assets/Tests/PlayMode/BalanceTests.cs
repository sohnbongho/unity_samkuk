using System;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Balance;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.Player;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Samkuk.Tests
{
    /// <summary>
    /// 밸런스 점검. BalanceModel(어림 모델)로 한 판의 흐름이 의도한 범위(난이도 "보통")에 있는지 확인한다.
    /// 숫자를 바꾼 뒤 이 테스트가 실패하면 "의도한 흐름에서 벗어났다"는 신호이며, 범위 자체가 틀렸다면
    /// 근거를 확인하고 범위를 고친다. 모델은 어림값이므로 최종 체감은 직접 플레이로 확인한다.
    /// </summary>
    public class BalanceTests
    {
        const string StagePath = "Assets/ScriptableObjects/Stage/Stage_YellowTurban.asset";
        const string HeroCatalogPath = "Assets/ScriptableObjects/HeroCatalog.asset";
        const string UpgradeCatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";
        const string MetaCatalogPath = "Assets/ScriptableObjects/MetaCatalog.asset";

        StageData stage;
        HeroCatalog heroes;
        UpgradeCatalog upgrades;
        MetaCatalog meta;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
            heroes = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HeroCatalogPath);
            upgrades = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(UpgradeCatalogPath);
            meta = AssetDatabase.LoadAssetAtPath<MetaCatalog>(MetaCatalogPath);
            Assert.IsNotNull(stage, "스테이지 에셋이 없습니다. Samkuk > Run All Setup 을 실행하세요.");
            Assert.IsNotNull(heroes, "HeroCatalog 가 없습니다.");
            Assert.IsNotNull(upgrades, "UpgradeCatalog 가 없습니다.");
            Assert.IsNotNull(meta, "MetaCatalog 가 없습니다.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        List<WeaponData> HeroWeapons()
        {
            var list = new List<WeaponData>();
            foreach (var h in heroes.heroes) list.Add(h.startingWeapon);
            return list;
        }

        /// <summary>시각 t에서 모든 장수의 평균 압박비.</summary>
        float AveragePressure(float t, List<int> levelUps, List<WeaponData> extras)
        {
            float sum = 0f;
            var weapons = HeroWeapons();
            foreach (var w in weapons) sum += BalanceModel.PressureRatio(stage, t, levelUps, w, extras);
            return sum / weapons.Count;
        }

        // ───────────────────────── 경험치 / 레벨 속도 ─────────────────────────

        [Test]
        public void ExpCurve_IsLinear_AndUsesTheTunableConstants()
        {
            Assert.AreEqual(PlayerExperience.BaseRequired, PlayerExperience.RequiredFor(1));
            for (int level = 1; level < 30; level++)
            {
                Assert.AreEqual(PlayerExperience.RequiredFor(level) + PlayerExperience.RequiredStep,
                    PlayerExperience.RequiredFor(level + 1), $"Lv.{level} → Lv.{level + 1}: 필요 경험치가 일정하게 늘어남");
            }
        }

        [Test]
        public void Levels_AtClear_ArePacedForAOneMinuteStage()
        {
            var times = BalanceModel.LevelUpTimes(BalanceModel.ExpPerSecond(stage));
            int finalLevel = times.Count + 1;

            // 너무 빠르면(과거 17레벨) 선택 창이 쉴 새 없이 떠서 흐름이 끊기고, 너무 느리면 빌드가 만들어지지 않는다
            Assert.GreaterOrEqual(finalLevel, 10, $"1분에 도달하는 레벨이 너무 낮음 ({finalLevel})");
            Assert.LessOrEqual(finalLevel, 14, $"1분에 도달하는 레벨이 너무 높음 ({finalLevel})");
        }

        [Test]
        public void LevelUps_StartEarly_AndKeepASteadyPace()
        {
            var times = BalanceModel.LevelUpTimes(BalanceModel.ExpPerSecond(stage));

            Assert.GreaterOrEqual(times[0], 2, "첫 레벨업이 너무 빨라 조작에 익숙해지기 전에 선택 창이 뜸");
            Assert.LessOrEqual(times[0], 8, "첫 레벨업이 너무 늦음");

            for (int i = 1; i < times.Count; i++)
                Assert.LessOrEqual(times[i] - times[i - 1], 20, $"{i}번째와 {i + 1}번째 레벨업 사이가 너무 김");
            Assert.GreaterOrEqual(times[times.Count - 1], stage.duration * 0.7f, "후반에도 레벨업이 이어져야 함");
        }

        [Test]
        public void Levels_WithStrongestExpBonuses_DoNotExplode()
        {
            // 현실적인 상한: 장수 1.2(유비) × 패시브 1.45 × 영구 강화 1.25
            float bonus = 1.2f * 1.45f * 1.25f;
            var times = BalanceModel.LevelUpTimes(BalanceModel.ExpPerSecond(stage, bonus));

            Assert.LessOrEqual(times.Count + 1, 20, $"경험치 보너스를 모두 쌓아도 60초에 20레벨을 넘지 않아야 함 ({times.Count + 1})");
        }

        // ───────────────────────── 전투 압박 ─────────────────────────

        [Test]
        public void Pressure_FollowsTheIntendedDifficultyCurve()
        {
            var levelUps = BalanceModel.LevelUpTimes(BalanceModel.ExpPerSecond(stage));
            var extras = BalanceModel.MedianCatalogWeapons(upgrades.weapons);

            // 각 웨이브가 시작하고 5초 뒤 (그 시점의 빌드 vs 그 웨이브의 적)
            var ratios = new List<float>();
            foreach (var w in stage.waves)
                ratios.Add(AveragePressure(w.startTime + 5f, levelUps, extras));

            Assert.AreEqual(4, ratios.Count, "웨이브 4개");
            // 초반은 여유롭게(1 이상), 마지막 웨이브는 평균 빌드로는 살짝 밀리고 진화/집중 빌드로 버티는 수준
            Assert.That(ratios[0], Is.InRange(1.1f, 2.0f), $"웨이브 1 압박비 {ratios[0]:0.00}");
            Assert.That(ratios[1], Is.InRange(1.0f, 1.8f), $"웨이브 2 압박비 {ratios[1]:0.00}");
            Assert.That(ratios[2], Is.InRange(0.7f, 1.2f), $"웨이브 3 압박비 {ratios[2]:0.00}");
            Assert.That(ratios[3], Is.InRange(0.55f, 1.0f), $"웨이브 4 압박비 {ratios[3]:0.00}");

            for (int i = 1; i < ratios.Count; i++)
                Assert.LessOrEqual(ratios[i], ratios[i - 1] + 0.05f, $"웨이브 {i + 1}는 앞 웨이브보다 쉬워지면 안 됨 (난이도가 계단식으로 오름)");
        }

        [Test]
        public void Pressure_IsPlayableForEveryHero_AtEveryWave()
        {
            var levelUps = BalanceModel.LevelUpTimes(BalanceModel.ExpPerSecond(stage));
            var extras = BalanceModel.MedianCatalogWeapons(upgrades.weapons);

            foreach (var hero in heroes.heroes)
            {
                foreach (var w in stage.waves)
                {
                    float r = BalanceModel.PressureRatio(stage, w.startTime + 5f, levelUps, hero.startingWeapon, extras);
                    Assert.Greater(r, 0.45f, $"{hero.displayName}: {w.name} 압박비 {r:0.00} — 해당 장수가 너무 불리함");
                    Assert.Less(r, 2.6f, $"{hero.displayName}: {w.name} 압박비 {r:0.00} — 해당 장수가 너무 유리함");
                }
            }
        }

        [Test]
        public void HeroStartingWeapons_HaveComparablePower()
        {
            float min = float.MaxValue, max = 0f;
            string minName = "", maxName = "";
            foreach (var hero in heroes.heroes)
            {
                float dps = BalanceModel.WeaponDps(hero.startingWeapon, 1);
                if (dps < min) { min = dps; minName = hero.displayName; }
                if (dps > max) { max = dps; maxName = hero.displayName; }
            }

            // 장수 고유 보정(체력, 공격력, 이동 속도, 스킬)이 따로 있으므로 시작 무기끼리는 비슷해야 한다
            Assert.LessOrEqual(max / min, 1.6f, $"시작 무기 화력 편차가 큼: {maxName} {max:0.0} vs {minName} {min:0.0}");
        }

        [Test]
        public void EvolvedWeapons_ArePowerfulButNotAbsurd()
        {
            foreach (var evo in upgrades.evolutions)
            {
                float before = BalanceModel.WeaponDps(evo.baseWeapon, evo.requiredLevel);
                float after = BalanceModel.WeaponDps(evo.evolvedWeapon, 1);
                float ratio = after / before;

                // 진화하면 1레벨로 돌아가므로, 진화 직전(필요 레벨) 무기보다 확실히 강해야 하지만 게임을 깨뜨릴 정도는 아니어야 함
                Assert.That(ratio, Is.InRange(1.5f, 4.0f),
                    $"{evo.baseWeapon.displayName} → {evo.evolvedWeapon.displayName}: 진화 위력 비율 {ratio:0.00} (직전 {before:0} → {after:0})");
            }
        }

        // ───────────────────────── 생존 ─────────────────────────

        [Test]
        public void Enemies_NeverShredThePlayerQuickly()
        {
            const float baseHp = 100f;
            var seen = new HashSet<EnemyData>();
            foreach (var w in stage.waves)
                foreach (var e in w.enemies)
                    if (e.data != null) seen.Add(e.data);
            foreach (var ev in stage.events)
                if (ev.enemy != null) seen.Add(ev.enemy);

            Assert.Greater(seen.Count, 8, "스테이지에 등장하는 적 종류");
            foreach (var e in seen)
            {
                // 한 종류에게 완전히 둘러싸여도(무적 시간마다 한 대씩) 일정 시간은 버텨야 한다
                float seconds = BalanceModel.SecondsToDieUnderContact(baseHp, e.contactDamage);
                Assert.GreaterOrEqual(seconds, 2.5f, $"{e.displayName}: 둘러싸이면 {seconds:0.0}초 만에 사망");

                if (e.attackRange > 0f)
                    Assert.LessOrEqual(e.projectileDamage, baseHp * 0.12f, $"{e.displayName}: 화살 한 발이 체력의 12%를 넘음");
            }
        }

        // ───────────────────────── 골드 ─────────────────────────

        [Test]
        public void Gold_FirstPurchaseIsReachable_AndFullProgressionTakesManyRuns()
        {
            int shortRun = RunRewards.Calculate(80, 30f, false);
            int clearRun = RunRewards.Calculate(250, stage.duration, true);
            int cheapest = BalanceModel.CheapestMetaCost(meta);

            Assert.LessOrEqual(cheapest, shortRun, $"30초 만에 죽은 판({shortRun}G)으로도 첫 영구 강화({cheapest}G)는 살 수 있어야 함");

            float runsToMax = BalanceModel.TotalMetaCost(meta) / (float)clearRun;
            Assert.That(runsToMax, Is.InRange(25f, 80f), $"클리어 판({clearRun}G) 기준으로 전부 최대까지 {runsToMax:0}판");
        }

        [Test]
        public void MetaUpgrades_TotalEffect_IsMeaningfulButNotAStomp()
        {
            var bonuses = MetaProgression.ComputeBonuses(meta, MaxedSave());

            Assert.That(bonuses.damage, Is.InRange(0.1f, 0.35f), $"공격력 영구 강화 합계 +{bonuses.damage * 100:0}%");
            Assert.That(bonuses.maxHp, Is.InRange(30f, 80f), $"최대 체력 영구 강화 합계 +{bonuses.maxHp:0}");
            Assert.That(bonuses.moveSpeed, Is.InRange(0.05f, 0.25f), $"이동 속도 영구 강화 합계 +{bonuses.moveSpeed * 100:0}%");
            Assert.LessOrEqual(bonuses.expGain, 0.4f, "경험치 영구 강화는 레벨 속도를 크게 바꾸지 않아야 함");
        }

        SaveData MaxedSave()
        {
            var save = new SaveData();
            foreach (var u in meta.upgrades) save.SetUpgradeLevel(MetaProgression.Key(u), u.maxLevel);
            return save;
        }

        // ───────────────────────── 레벨업 선택지와 진화 ─────────────────────────

        [Test]
        public void UpgradeWeights_FavorUpgradingWhatYouOwn()
        {
            Assert.Greater(upgrades.weaponLevelUpWeight, upgrades.passiveWeight, "보유 무기 강화 > 패시브");
            Assert.Greater(upgrades.passiveWeight, upgrades.newWeaponWeight, "패시브 > 새 무기");
            Assert.Greater(upgrades.newWeaponWeight, 0f, "새 무기도 나올 수 있어야 함");
        }

        PlayerRig NewRig()
        {
            var go = new GameObject("BalanceTestPlayer");
            toDestroy.Add(go);
            var rig = new PlayerRig
            {
                go = go,
                stats = go.AddComponent<PlayerStats>(),
                health = go.AddComponent<PlayerHealth>(),
                wc = go.AddComponent<WeaponController>()
            };
            return rig;
        }

        class PlayerRig
        {
            public GameObject go;
            public PlayerStats stats;
            public PlayerHealth health;
            public WeaponController wc;
        }

        [Test]
        public void Generator_DrawsKindsInProportionToTheirWeights_AndNeverZeroWeights()
        {
            Random.InitState(7);
            var rig = NewRig();

            WeaponData Weapon(string name)
            {
                var w = ScriptableObject.CreateInstance<WeaponData>();
                w.displayName = name; w.type = WeaponType.Slash; w.cooldown = 99f; toDestroy.Add(w);
                return w;
            }
            PassiveData Passive(string name)
            {
                var p = ScriptableObject.CreateInstance<PassiveData>();
                p.displayName = name; p.type = PassiveType.Damage; p.valuePerLevel = 0.1f; p.maxLevel = 5; toDestroy.Add(p);
                return p;
            }

            var owned = Weapon("보유");
            rig.wc.AddWeapon(owned);
            var catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
            toDestroy.Add(catalog);
            catalog.weapons.Add(Weapon("신규1")); catalog.weapons.Add(Weapon("신규2")); catalog.weapons.Add(Weapon("신규3"));
            catalog.passives.Add(Passive("패1")); catalog.passives.Add(Passive("패2"));
            catalog.weaponLevelUpWeight = 4f; catalog.passiveWeight = 2f; catalog.newWeaponWeight = 1f;

            // 총 가중치 4 + 2×2 + 1×3 = 11 → 강화 36%, 패시브 36%, 새 무기 27%
            int levelUp = 0, passive = 0, newWeapon = 0;
            const int trials = 4000;
            for (int i = 0; i < trials; i++)
            {
                var first = UpgradeGenerator.Generate(catalog, rig.wc, rig.stats, rig.health, count: 1)[0];
                if (first.Kind == UpgradeKind.WeaponLevelUp) levelUp++;
                else if (first.Kind == UpgradeKind.Passive) passive++;
                else if (first.Kind == UpgradeKind.NewWeapon) newWeapon++;
            }
            Assert.AreEqual(4f / 11f, levelUp / (float)trials, 0.04f, "보유 무기 강화");
            Assert.AreEqual(4f / 11f, passive / (float)trials, 0.04f, "패시브");
            Assert.AreEqual(3f / 11f, newWeapon / (float)trials, 0.04f, "새 무기");

            // 가중치 0 인 종류는 절대 나오지 않는다
            catalog.newWeaponWeight = 0f;
            for (int i = 0; i < 300; i++)
            {
                var options = UpgradeGenerator.Generate(catalog, rig.wc, rig.stats, rig.health, count: 3);
                Assert.IsFalse(options.Exists(o => o.Kind == UpgradeKind.NewWeapon), "가중치 0 인 새 무기는 나오지 않음");
                Assert.AreEqual(3, options.Count, "다른 후보로 채워져 3장");
            }
        }

        /// <summary>
        /// 실제 선택지 생성기로 한 판의 레벨업을 시뮬레이션해, 진화에 도달하는 비율을 잰다.
        /// "집중" 플레이어는 진화 가능한 무기 하나를 키우고 짝 패시브를 고르며, "무작위" 플레이어는 아무거나 고른다.
        /// </summary>
        float EvolutionRate(bool focused, int trials, int levelUps)
        {
            var heroWeapons = HeroWeapons();
            int evolvedCount = 0;

            for (int trial = 0; trial < trials; trial++)
            {
                var rig = NewRig();
                rig.wc.AddWeapon(heroWeapons[trial % heroWeapons.Count]);
                bool evolved = false;

                for (int step = 0; step < levelUps && !evolved; step++)
                {
                    var options = UpgradeGenerator.Generate(upgrades, rig.wc, rig.stats, rig.health);
                    var choice = focused ? PickFocused(options, rig) : options[Random.Range(0, options.Count)];
                    choice.Apply();
                    if (choice.Kind == UpgradeKind.Evolve) evolved = true;
                }

                if (evolved) evolvedCount++;
                UnityEngine.Object.DestroyImmediate(rig.go);
            }
            return evolvedCount / (float)trials;
        }

        UpgradeOption PickFocused(List<UpgradeOption> options, PlayerRig rig)
        {
            // 1) 진화 > 2) 진화 가능한 무기의 강화 > 3) 그 무기의 짝 패시브(아직 없을 때) > 4) 아무 무기 강화 > 5) 아무 패시브
            foreach (var o in options) if (o.Kind == UpgradeKind.Evolve) return o;

            Weapon target = null;
            EvolutionData targetEvo = null;
            foreach (var w in rig.wc.Weapons)
            {
                var evo = upgrades.evolutions.Find(e => e.baseWeapon == w.Data);
                if (evo != null && (target == null || w.Level > target.Level)) { target = w; targetEvo = evo; }
            }

            if (target != null)
            {
                foreach (var o in options)
                    if (o.Kind == UpgradeKind.WeaponLevelUp && o.Weapon == target.Data) return o;
                foreach (var o in options)
                    if (o.Kind == UpgradeKind.Passive && o.Passive == targetEvo.requiredPassive
                        && rig.stats.GetLevel(targetEvo.requiredPassive) < targetEvo.requiredPassiveLevel) return o;
            }
            foreach (var o in options) if (o.Kind == UpgradeKind.WeaponLevelUp) return o;
            foreach (var o in options) if (o.Kind == UpgradeKind.Passive) return o;
            return options[0];
        }

        [Test]
        public void Evolution_IsReachableWithinOneStage_ForFocusedPlay_ButNotGuaranteed()
        {
            Random.InitState(20240601);
            int levelUps = BalanceModel.LevelUpTimes(BalanceModel.ExpPerSecond(stage)).Count;

            float focused = EvolutionRate(focused: true, trials: 120, levelUps: levelUps);
            float random = EvolutionRate(focused: false, trials: 120, levelUps: levelUps);

            // 과거(필요 Lv.5, 균등 선택, 장수 무기 진화 없음)에는 집중해도 약 20%, 무작위는 1% 였다
            Assert.GreaterOrEqual(focused, 0.55f, $"집중해서 키우면 한 판에 진화에 닿아야 함 ({focused * 100:0}%, 레벨업 {levelUps}회)");
            Assert.LessOrEqual(focused, 0.97f, $"집중해도 거의 확정이면 선택의 의미가 없음 ({focused * 100:0}%)");
            Assert.LessOrEqual(random, 0.35f, $"아무렇게나 골라도 진화하면 안 됨 ({random * 100:0}%)");
            Assert.Greater(focused, random + 0.3f, "집중 육성이 확실히 유리해야 함");
        }
    }
}
