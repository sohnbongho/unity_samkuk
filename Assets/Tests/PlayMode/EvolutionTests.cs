using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Data;
using Samkuk.Enemies;
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
    public class EvolutionTests
    {
        const string CatalogPath = "Assets/ScriptableObjects/UpgradeCatalog.asset";

        GameObject playerGo;
        PlayerStats stats;
        PlayerHealth health;
        PlayerExperience exp;
        WeaponController wc;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            playerGo = new GameObject("TestPlayer");
            stats = playerGo.AddComponent<PlayerStats>();
            health = playerGo.AddComponent<PlayerHealth>();
            exp = playerGo.AddComponent<PlayerExperience>();
            wc = playerGo.AddComponent<WeaponController>(); // PlayerController 자동 추가
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (playerGo != null) UnityEngine.Object.Destroy(playerGo);
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        WeaponData MakeWeapon(string name, WeaponType type = WeaponType.Arrow, int maxLevel = 8)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.displayName = name; w.type = type; w.maxLevel = maxLevel;
            w.damage = 10f; w.cooldown = 10f; w.range = 8f; w.size = 0.3f; w.duration = 1f; w.projectileSpeed = 10f;
            w.description = $"{name} 설명";
            toDestroy.Add(w);
            return w;
        }

        PassiveData MakePassive(string name, PassiveType type = PassiveType.Damage, int maxLevel = 5)
        {
            var p = ScriptableObject.CreateInstance<PassiveData>();
            p.displayName = name; p.type = type; p.valuePerLevel = 0.1f; p.maxLevel = maxLevel;
            toDestroy.Add(p);
            return p;
        }

        EvolutionData MakeEvo(WeaponData baseWeapon, PassiveData passive, WeaponData evolved,
            int requiredLevel = 5, int requiredPassiveLevel = 1)
        {
            var e = ScriptableObject.CreateInstance<EvolutionData>();
            e.baseWeapon = baseWeapon; e.requiredPassive = passive; e.evolvedWeapon = evolved;
            e.requiredLevel = requiredLevel; e.requiredPassiveLevel = requiredPassiveLevel;
            toDestroy.Add(e);
            return e;
        }

        UpgradeCatalog MakeCatalog(IEnumerable<WeaponData> weapons, IEnumerable<PassiveData> passives,
            IEnumerable<EvolutionData> evolutions)
        {
            var c = ScriptableObject.CreateInstance<UpgradeCatalog>();
            c.weapons.AddRange(weapons);
            c.passives.AddRange(passives);
            c.evolutions.AddRange(evolutions);
            toDestroy.Add(c);
            return c;
        }

        /// <summary>무기를 추가하고 목표 레벨까지 올린다.</summary>
        Weapon Give(WeaponData data, int level)
        {
            var w = wc.AddWeapon(data);
            while (w.Level < level) w.LevelUp();
            return w;
        }

        // ───────────────────────── 진화 조건 ─────────────────────────

        [Test]
        public void Evolve_IsOffered_WhenWeaponLevelAndPassiveAreMet()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노"); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 3);
            var catalog = MakeCatalog(new[] { bow }, new[] { haste }, new[] { evo });

            Give(bow, 3);
            stats.AddPassive(haste);

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health);

            Assert.AreEqual(UpgradeKind.Evolve, options[0].Kind);
            Assert.AreSame(evolved, options[0].Weapon);
            Assert.AreSame(evo, options[0].Evolution);
            StringAssert.Contains("활", options[0].Title);
            StringAssert.Contains("연노", options[0].Title);
            StringAssert.Contains("신속", options[0].Description, "필요한 패시브 이름을 알려줌");
            StringAssert.Contains("연노 설명", options[0].Description, "진화 무기 설명 포함");
        }

        [Test]
        public void Evolve_IsNotOffered_WhenAnyConditionIsMissing()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노"); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 4, requiredPassiveLevel: 2);
            var catalog = MakeCatalog(new[] { bow }, new[] { haste }, new[] { evo });

            // 무기를 갖고 있지 않음
            stats.AddPassive(haste); stats.AddPassive(haste);
            Assert.IsFalse(UpgradeGenerator.CanEvolve(evo, wc, stats), "무기 미보유");

            // 무기 레벨 부족
            Give(bow, 3);
            Assert.IsFalse(UpgradeGenerator.CanEvolve(evo, wc, stats), "무기 레벨 부족");
            wc.Weapons[0].LevelUp();
            Assert.IsTrue(UpgradeGenerator.CanEvolve(evo, wc, stats), "조건 충족");

            // 패시브 레벨 부족
            var evo2 = MakeEvo(bow, haste, evolved, requiredLevel: 4, requiredPassiveLevel: 3);
            Assert.IsFalse(UpgradeGenerator.CanEvolve(evo2, wc, stats), "패시브 레벨 부족");

            // 패시브 미보유
            var other = MakePassive("다른");
            var evo3 = MakeEvo(bow, other, evolved, requiredLevel: 4);
            Assert.IsFalse(UpgradeGenerator.CanEvolve(evo3, wc, stats), "패시브 미보유");

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10);
            Assert.AreEqual(UpgradeKind.Evolve, options[0].Kind, "충족했으니 선택지에 포함");
        }

        [Test]
        public void Evolve_RequiredLevelIsClampedToWeaponMaxLevel()
        {
            var bow = MakeWeapon("활", maxLevel: 3); var evolved = MakeWeapon("연노"); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 5); // 최대 레벨(3)보다 큼
            Give(bow, 3);
            stats.AddPassive(haste);

            Assert.IsTrue(UpgradeGenerator.CanEvolve(evo, wc, stats), "필요 레벨이 최대 레벨보다 크면 최대 레벨에서 진화 가능");
        }

        [Test]
        public void Evolve_InvalidData_IsNeverOffered()
        {
            var bow = MakeWeapon("활"); var haste = MakePassive("신속");
            var broken = MakeEvo(bow, haste, null);
            Give(bow, 5);
            stats.AddPassive(haste);

            Assert.IsFalse(broken.IsValid);
            Assert.IsFalse(UpgradeGenerator.CanEvolve(broken, wc, stats));
            Assert.IsFalse(UpgradeGenerator.CanEvolve(null, wc, stats));
            Assert.IsFalse(UpgradeGenerator.CanEvolve(broken, null, stats));
        }

        // ───────────────────────── 선택지 구성 ─────────────────────────

        [Test]
        public void Evolve_IsAlwaysFirst_EvenWithManyOtherCandidates()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노"); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 2);
            var passives = new List<PassiveData> { haste };
            for (int i = 0; i < 6; i++) passives.Add(MakePassive($"패시브{i}"));
            var catalog = MakeCatalog(new[] { bow, MakeWeapon("검", WeaponType.Slash), MakeWeapon("창", WeaponType.Thrust) },
                passives, new[] { evo });

            Give(bow, 2);
            stats.AddPassive(haste);

            for (int trial = 0; trial < 40; trial++)
            {
                var options = UpgradeGenerator.Generate(catalog, wc, stats, health);
                Assert.AreEqual(3, options.Count);
                Assert.AreEqual(UpgradeKind.Evolve, options[0].Kind, "진화 선택지는 항상 맨 앞");
                Assert.AreEqual(1, options.FindAll(o => o.Kind == UpgradeKind.Evolve).Count);
            }
        }

        [Test]
        public void Evolve_MultipleAvailable_TakeTheFirstSlots()
        {
            var a = MakeWeapon("A"); var b = MakeWeapon("B", WeaponType.Slash);
            var ea = MakeWeapon("A+"); var eb = MakeWeapon("B+", WeaponType.Slash);
            var p = MakePassive("P");
            var catalog = MakeCatalog(new[] { a, b }, new[] { p },
                new[] { MakeEvo(a, p, ea, 2), MakeEvo(b, p, eb, 2) });
            Give(a, 2); Give(b, 2);
            stats.AddPassive(p);

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health);

            Assert.AreEqual(UpgradeKind.Evolve, options[0].Kind);
            Assert.AreEqual(UpgradeKind.Evolve, options[1].Kind);
            Assert.AreEqual(3, options.Count);
            Assert.AreNotEqual(options[0].Title, options[1].Title);
        }

        // ───────────────────────── 진화 적용 ─────────────────────────

        [UnityTest]
        public IEnumerator Evolve_ReplacesBaseWeapon_WithEvolvedAtLevelOne()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노", WeaponType.Slash); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 4);
            var other = MakeWeapon("검", WeaponType.Thrust);
            var catalog = MakeCatalog(new[] { bow, other }, new[] { haste }, new[] { evo });

            var oldWeapon = Give(bow, 4);
            var otherWeapon = Give(other, 3);
            stats.AddPassive(haste);

            WeaponData fromEvt = null, toEvt = null;
            wc.Evolved += (f, t) => { fromEvt = f; toEvt = t; };

            var options = UpgradeGenerator.Generate(catalog, wc, stats, health);
            options[0].Apply();
            yield return null; // Destroy 반영

            Assert.IsFalse(wc.Owns(bow), "기본 무기는 사라짐");
            Assert.IsTrue(wc.Owns(evolved));
            Assert.AreEqual(2, wc.Weapons.Count, "무기 개수는 그대로 (교체)");
            Assert.IsTrue(oldWeapon == null, "기존 무기 오브젝트가 파괴됨");

            var newWeapon = wc.Weapons.Count > 0 ? FindWeapon(evolved) : null;
            Assert.IsNotNull(newWeapon);
            Assert.AreEqual(1, newWeapon.Level, "진화 무기는 1레벨에서 시작");
            Assert.IsInstanceOf<SlashWeapon>(newWeapon, "진화 무기의 동작 방식은 자신의 데이터 타입을 따름");

            Assert.AreEqual(3, otherWeapon.Level, "다른 무기는 영향 없음");
            Assert.IsTrue(wc.HasEvolved(bow));
            Assert.AreSame(bow, fromEvt);
            Assert.AreSame(evolved, toEvt);
        }

        Weapon FindWeapon(WeaponData data)
        {
            foreach (var w in wc.Weapons) if (w.Data == data) return w;
            return null;
        }

        [UnityTest]
        public IEnumerator Evolved_CannotBeOfferedAgain_AndBaseIsNotOfferedAsNewWeapon()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노"); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 2);
            var catalog = MakeCatalog(new[] { bow }, new[] { haste }, new[] { evo });
            Give(bow, 2);
            stats.AddPassive(haste);

            UpgradeGenerator.Generate(catalog, wc, stats, health)[0].Apply();
            yield return null;

            for (int i = 0; i < 20; i++)
            {
                var options = UpgradeGenerator.Generate(catalog, wc, stats, health, count: 10);
                Assert.IsFalse(options.Exists(o => o.Kind == UpgradeKind.Evolve), "이미 진화했으면 다시 나오지 않음");
                Assert.IsFalse(options.Exists(o => o.Kind == UpgradeKind.NewWeapon && o.Weapon == bow),
                    "진화한 기본 무기가 새 무기로 다시 나오면 안 됨");
            }
            Assert.IsFalse(UpgradeGenerator.CanEvolve(evo, wc, stats));
        }

        [Test]
        public void Evolve_ReturnsNull_ForInvalidRequests()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노");

            Assert.IsNull(wc.Evolve(bow, evolved), "기본 무기를 갖고 있지 않음");
            Assert.IsNull(wc.Evolve(null, evolved));
            Assert.IsNull(wc.Evolve(bow, null));
            Assert.IsNull(wc.Evolve(bow, bow), "같은 무기로는 진화 불가");

            wc.AddWeapon(bow);
            wc.AddWeapon(evolved);
            Assert.IsNull(wc.Evolve(bow, evolved), "진화 무기를 이미 갖고 있음");
            Assert.AreEqual(2, wc.Weapons.Count, "실패한 진화는 아무것도 바꾸지 않음");
            Assert.IsTrue(wc.Owns(bow));
        }

        [UnityTest]
        public IEnumerator EvolvedWeapon_CanKeepLevelingUp()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노"); evolved.maxLevel = 4;
            wc.AddWeapon(bow);
            var w = wc.Evolve(bow, evolved);
            yield return null;

            Assert.IsNotNull(w);
            Assert.IsTrue(w.LevelUp());
            Assert.AreEqual(2, w.Level);
            Assert.IsTrue(w.LevelUp()); Assert.IsTrue(w.LevelUp());
            Assert.IsFalse(w.LevelUp(), "진화 무기도 최대 레벨이 있음");
        }

        // ───────────────────────── 레벨업 흐름 / UI ─────────────────────────

        class FakeView : ILevelUpView
        {
            public IReadOnlyList<UpgradeOption> LastOptions;
            public bool IsVisible { get; private set; }
            public void Show(IReadOnlyList<UpgradeOption> options, Action<int> onChosen) { LastOptions = options; IsVisible = true; }
            public void Hide() { IsVisible = false; }
        }

        [UnityTest]
        public IEnumerator LevelUpFlow_OffersAndAppliesEvolution()
        {
            var bow = MakeWeapon("활"); var evolved = MakeWeapon("연노"); var haste = MakePassive("신속");
            var evo = MakeEvo(bow, haste, evolved, requiredLevel: 3);
            var catalog = MakeCatalog(new[] { bow }, new[] { haste }, new[] { evo });
            Give(bow, 3);
            stats.AddPassive(haste);

            var go = new GameObject("TestLevelUp");
            toDestroy.Add(go);
            var view = new FakeView();
            var c = go.AddComponent<LevelUpController>();
            c.Experience = exp; c.Weapons = wc; c.Stats = stats; c.Health = health; c.Catalog = catalog; c.View = view;

            exp.AddExp(5); // 레벨업

            Assert.IsTrue(c.IsShowing);
            Assert.AreEqual(UpgradeKind.Evolve, view.LastOptions[0].Kind, "레벨업 화면의 첫 카드가 진화");

            c.Choose(0);
            yield return null;

            Assert.IsTrue(wc.Owns(evolved));
            Assert.IsFalse(wc.Owns(bow));
            Assert.AreEqual(1f, Time.timeScale, "선택 후 게임 재개");
        }

        [Test]
        public void LevelUpUI_HighlightsEvolveCard_AndRestoresNormalColor()
        {
            var uiGo = new GameObject("TestLevelUpUI");
            toDestroy.Add(uiGo);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(uiGo.transform);

            var baseColor = new Color(0.17f, 0.2f, 0.3f, 1f);
            var cards = new UpgradeCardView[3];
            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject($"Card{i}", typeof(RectTransform));
                cardGo.transform.SetParent(panel.transform);
                var img = cardGo.AddComponent<Image>();
                img.color = baseColor;
                var btn = cardGo.AddComponent<Button>();
                btn.targetGraphic = img;
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

            var evolveFirst = new List<UpgradeOption>
            {
                new UpgradeOption { Kind = UpgradeKind.Evolve, Title = "진화", Description = "d" },
                new UpgradeOption { Kind = UpgradeKind.Passive, Title = "패시브", Description = "d" },
                new UpgradeOption { Kind = UpgradeKind.Heal, Title = "회복", Description = "d" },
            };
            ui.Show(evolveFirst, _ => { });

            var evolveColor = cards[0].button.targetGraphic.color;
            Assert.AreEqual(UiSkin.CardTint(UpgradeKind.Evolve), evolveColor, "진화 카드는 진화색");
            Assert.Greater(evolveColor.r, evolveColor.b, "금빛 계열");
            Assert.AreEqual(UiSkin.CardTint(UpgradeKind.Passive), cards[1].button.targetGraphic.color, "패시브는 패시브색");
            Assert.AreEqual(UiSkin.CardTint(UpgradeKind.Heal), cards[2].button.targetGraphic.color, "회복은 회복색");
            Assert.AreNotEqual(cards[1].button.targetGraphic.color, cards[2].button.targetGraphic.color, "종류가 다르면 색도 다름");

            var normal = new List<UpgradeOption>(evolveFirst);
            normal[0] = new UpgradeOption { Kind = UpgradeKind.NewWeapon, Title = "새 무기", Description = "d" };
            ui.Show(normal, _ => { });

            Assert.AreEqual(UiSkin.CardTint(UpgradeKind.NewWeapon), cards[0].button.targetGraphic.color, "진화가 아니면 진화색이 남지 않음");
            Assert.AreNotEqual(evolveColor, cards[0].button.targetGraphic.color);
        }

        // ───────────────────────── 실제 에셋 ─────────────────────────

        [Test]
        public void CatalogAsset_HasWellFormedEvolutions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "UpgradeCatalog 가 없습니다. Samkuk > Step 6 / 8-2 / 8-4 를 먼저 실행하세요.");
            Assert.GreaterOrEqual(catalog.evolutions.Count, 5, "진화 조합 5개 이상 (Step 8-4 실행)");

            var bases = new HashSet<WeaponData>();
            var evolvedSet = new HashSet<WeaponData>();
            var catalogNames = new HashSet<string>();
            foreach (var w in catalog.weapons) catalogNames.Add(w.displayName);

            foreach (var evo in catalog.evolutions)
            {
                Assert.IsNotNull(evo, "빈 진화 항목");
                Assert.IsTrue(evo.IsValid, $"{evo.name}: 필드 누락");
                Assert.Contains(evo.baseWeapon, catalog.weapons, $"{evo.name}: 기본 무기가 카탈로그에 있어야 함");
                Assert.Contains(evo.requiredPassive, catalog.passives, $"{evo.name}: 필요 패시브가 카탈로그에 있어야 함");
                Assert.IsFalse(catalog.weapons.Contains(evo.evolvedWeapon), $"{evo.name}: 진화 무기가 일반 선택지에 섞이면 안 됨");
                Assert.IsTrue(bases.Add(evo.baseWeapon), $"{evo.name}: 같은 기본 무기에 진화 조합이 둘");
                Assert.IsTrue(evolvedSet.Add(evo.evolvedWeapon), $"{evo.name}: 진화 무기 중복");

                Assert.LessOrEqual(evo.requiredLevel, evo.baseWeapon.maxLevel, $"{evo.name}: 필요 레벨이 최대 레벨 이하");
                Assert.GreaterOrEqual(evo.requiredLevel, 2);

                var b = evo.baseWeapon; var e = evo.evolvedWeapon;
                Assert.AreEqual(b.type, e.type, $"{evo.name}: 진화 후에도 같은 계열의 동작");
                Assert.Greater(e.damage, b.damage, $"{evo.name}: 진화 무기가 더 강해야 함");
                Assert.IsFalse(string.IsNullOrEmpty(e.description), $"{evo.name}: 진화 무기 설명");
                Assert.IsFalse(catalogNames.Contains(e.displayName), $"{evo.name}: 진화 무기 이름이 일반 무기와 겹침 ({e.displayName})");
                Assert.AreNotEqual(b.displayName, e.displayName);
            }
        }
    }
}
