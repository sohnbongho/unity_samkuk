using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Heroes;
using Samkuk.Pickups;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    public class HeroTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string GemPrefabPath = "Assets/Prefabs/ExpGem.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
        const string HeroCatalogPath = "Assets/ScriptableObjects/HeroCatalog.asset";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        SpriteRenderer body;
        EnemyManager manager;
        EnemySpawner spawner;
        ExpGemManager gems;
        PlayerStats stats;
        PlayerHealth health;
        PlayerExperience exp;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.SelectedHero = null;

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            var gemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GemPrefabPath)?.GetComponent<ExpGem>();
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");
            Assert.IsNotNull(gemPrefab, "ExpGem 프리팹이 없습니다. Step 6 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(playerGo.transform, false);
            body = bodyGo.AddComponent<SpriteRenderer>();
            stats = playerGo.AddComponent<PlayerStats>();
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
            manager = root.AddComponent<EnemyManager>();
            spawner = root.AddComponent<EnemySpawner>();
            gems = root.AddComponent<ExpGemManager>();
            manager.Target = playerGo.transform;
            spawner.Manager = manager;
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
            GameSession.SelectedHero = null;
            if (root != null) UnityEngine.Object.Destroy(root);
            if (playerGo != null) UnityEngine.Object.Destroy(playerGo);
            if (camGo != null) UnityEngine.Object.Destroy(camGo);
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        EnemyData MakeEnemy(int hp = 100, float speed = 0f, int contactDamage = 0)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp; d.moveSpeed = speed; d.contactDamage = contactDamage; d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        HeroData MakeHero(string name, Color? tint = null, WeaponData weapon = null, SkillData skill = null)
        {
            var h = ScriptableObject.CreateInstance<HeroData>();
            h.displayName = name;
            h.tint = tint ?? Color.white;
            h.startingWeapon = weapon;
            h.skill = skill;
            toDestroy.Add(h);
            return h;
        }

        WeaponData MakeWeapon(string name)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.displayName = name; w.type = WeaponType.Arrow;
            w.damage = 10f; w.cooldown = 1f; w.range = 8f; w.projectileSpeed = 10f; w.duration = 1f; w.size = 0.3f;
            toDestroy.Add(w);
            return w;
        }

        SkillData MakeSkill(SkillType type, float cooldown = 0.5f)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.displayName = type.ToString(); s.type = type; s.cooldown = cooldown;
            toDestroy.Add(s);
            return s;
        }

        PassiveData MakePassive(PassiveType type, float value)
        {
            var p = ScriptableObject.CreateInstance<PassiveData>();
            p.type = type; p.valuePerLevel = value; p.maxLevel = 5;
            toDestroy.Add(p);
            return p;
        }

        SkillController AddSkillController(SkillData skill)
        {
            var sc = playerGo.AddComponent<SkillController>();
            sc.Enemies = manager;
            sc.Gems = gems;
            sc.SetSkill(skill);
            return sc;
        }

        WeaponController AddWeaponController()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<Projectile>();
            Assert.IsNotNull(projectile, "Projectile 프리팹이 없습니다. Step 5 를 먼저 실행하세요.");
            var wc = playerGo.AddComponent<WeaponController>();
            wc.ProjectilePrefab = projectile;
            wc.EnemyManager = manager;
            return wc;
        }

        // ───────────────────────── 스탯: 장수 보정 / 버프 ─────────────────────────

        [Test]
        public void Stats_ApplyHero_ScalesAndStacksWithPassives()
        {
            var hero = MakeHero("테스트");
            hero.damageMultiplier = 1.2f; hero.moveSpeedMultiplier = 1.1f;
            hero.expMultiplier = 1.2f; hero.pickupRadiusMultiplier = 1.3f; hero.maxHpBonus = 20f;

            stats.ApplyHero(hero);

            Assert.AreEqual(1.2f, stats.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.1f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(1.2f, stats.ExpMultiplier, 0.0001f);
            Assert.AreEqual(1.3f, stats.PickupRadiusMultiplier, 0.0001f);
            Assert.AreEqual(120f, health.Max, 0.001f);
            Assert.AreEqual(120f, health.Current, 0.001f, "최대 체력이 늘면 그만큼 회복");

            stats.AddPassive(MakePassive(PassiveType.Damage, 0.5f));
            Assert.AreEqual(1.8f, stats.DamageMultiplier, 0.0001f, "(1 + 0.5) × 1.2");

            stats.ApplyHero(null);
            Assert.AreEqual(1.5f, stats.DamageMultiplier, 0.0001f, "장수 보정만 제거되고 패시브는 유지");
            Assert.AreEqual(100f, health.Max, 0.001f);
        }

        [Test]
        public void Stats_HeroWithLowerMaxHp_ClampsCurrent()
        {
            var hero = MakeHero("여포"); hero.maxHpBonus = -20f;
            stats.ApplyHero(hero);

            Assert.AreEqual(80f, health.Max, 0.001f);
            Assert.AreEqual(80f, health.Current, 0.001f);
        }

        [UnityTest]
        public IEnumerator Stats_TimedBuff_AppliesThenExpires()
        {
            yield return null;
            stats.AddPassive(MakePassive(PassiveType.Cooldown, 0.2f));
            stats.AddBuff(damageBonus: 0.5f, cooldownReduction: 0.3f, speedBonus: 0.2f, duration: 0.6f);

            Assert.AreEqual(1.5f, stats.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.2f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0.56f, stats.CooldownMultiplier, 0.0001f, "(1 - 0.2) × (1 - 0.3)");
            Assert.AreEqual(1, stats.ActiveBuffCount);

            yield return new WaitForSeconds(0.9f);

            Assert.AreEqual(0, stats.ActiveBuffCount);
            Assert.AreEqual(1f, stats.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0.8f, stats.CooldownMultiplier, 0.0001f, "패시브만 남음");
        }

        [Test]
        public void Stats_Buff_WithZeroDuration_IsIgnored()
        {
            stats.AddBuff(1f, 0f, 0f, 0f);
            Assert.AreEqual(0, stats.ActiveBuffCount);
            Assert.AreEqual(1f, stats.DamageMultiplier, 0.0001f);
        }

        // ───────────────────────── 체력: 색 / 스킬 무적 ─────────────────────────

        [Test]
        public void Health_BaseColor_IsApplied_AndKeptOnDeath()
        {
            var red = new Color(1f, 0.2f, 0.2f);
            health.SetBaseColor(red);
            Assert.AreEqual(red, body.color);

            health.TakeDamage(1000f);
            Assert.AreEqual(red, body.color, "사망 시에도 장수 색 유지");
        }

        [UnityTest]
        public IEnumerator Health_Flicker_KeepsHeroHue_AndRestoresBaseColor()
        {
            yield return null;
            var blue = new Color(0.2f, 0.4f, 1f);
            health.SetBaseColor(blue);

            health.TryContactDamage(5f);
            yield return null;
            yield return null;
            Assert.AreEqual(blue.r, body.color.r, 0.001f, "깜빡이는 동안에도 색조 유지");
            Assert.AreEqual(blue.b, body.color.b, 0.001f);

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(blue, body.color, "무적이 끝나면 원래 색으로");
        }

        [UnityTest]
        public IEnumerator Health_SkillInvulnerability_BlocksAllDamage_ThenExpires()
        {
            yield return null;
            health.SetInvulnerable(0.5f);

            health.TakeDamage(30f);
            Assert.IsFalse(health.TryContactDamage(30f));
            Assert.IsTrue(health.IsSkillInvulnerable);
            Assert.AreEqual(100f, health.Current, 0.001f);

            yield return new WaitForSeconds(0.7f);

            Assert.IsFalse(health.IsSkillInvulnerable);
            health.TakeDamage(30f);
            Assert.AreEqual(70f, health.Current, 0.001f);
        }

        // ───────────────────────── 적 기절 ─────────────────────────

        [UnityTest]
        public IEnumerator Stun_FreezesEnemy_ThenItResumesChasing()
        {
            yield return null;
            var e = spawner.SpawnAt(MakeEnemy(speed: 2f), new Vector2(8f, 0f));
            e.Stun(0.6f);
            Assert.IsTrue(e.IsStunned);

            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(8f, e.Position.x, 0.05f, "기절 중에는 움직이지 않음");

            yield return new WaitForSeconds(1.0f);
            Assert.IsFalse(e.IsStunned);
            Assert.Less(e.Position.x, 7.5f, "기절이 풀리면 다시 추적");
        }

        [UnityTest]
        public IEnumerator Stun_PreventsContactDamage_UntilItEnds()
        {
            yield return null;
            var e = spawner.SpawnAt(MakeEnemy(speed: 0f, contactDamage: 10), new Vector2(0.5f, 0f));
            e.Stun(0.5f);

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(100f, health.Current, 0.001f, "기절한 적은 접촉 피해를 주지 못함");

            yield return new WaitForSeconds(0.6f);
            Assert.Less(health.Current, 100f, "기절이 풀리면 다시 피해");
        }

        [UnityTest]
        public IEnumerator Stun_IsClearedWhenEnemyIsReused()
        {
            yield return null;
            var data = MakeEnemy(hp: 5, speed: 2f);
            var e = spawner.SpawnAt(data, new Vector2(8f, 0f));
            e.Stun(10f);
            e.TakeDamage(100f); // 사망 → 풀 반환

            var again = spawner.SpawnAt(data, new Vector2(8f, 0f));

            Assert.AreSame(e, again);
            Assert.IsFalse(again.IsStunned, "재사용되는 적은 기절 상태가 초기화");
        }

        [Test]
        public void Stun_IgnoredForDeadEnemy()
        {
            var e = spawner.SpawnAt(MakeEnemy(hp: 5), new Vector2(8f, 0f));
            e.TakeDamage(100f);
            e.Stun(5f);
            Assert.IsFalse(e.IsStunned);
        }

        // ───────────────────────── 스킬 ─────────────────────────

        [UnityTest]
        public IEnumerator Skill_GreenDragonSlash_DamagesOnlyEnemiesInRadius()
        {
            var skill = MakeSkill(SkillType.GreenDragonSlash);
            skill.radius = 6f; skill.damage = 90f;
            var sc = AddSkillController(skill);
            yield return null;

            var near = spawner.SpawnAt(MakeEnemy(hp: 50), new Vector2(2f, 0f));
            var far = spawner.SpawnAt(MakeEnemy(hp: 50), new Vector2(9f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(sc.Use());

            Assert.IsFalse(near.Alive, "범위 안의 적은 처치");
            Assert.IsTrue(far.Alive);
            Assert.AreEqual(50f, far.Hp, 0.001f, "범위 밖의 적은 피해 없음");
        }

        [UnityTest]
        public IEnumerator Skill_DamageScalesWithDamageMultiplier()
        {
            var skill = MakeSkill(SkillType.GreenDragonSlash);
            skill.radius = 6f; skill.damage = 40f;
            var sc = AddSkillController(skill);
            stats.AddPassive(MakePassive(PassiveType.Damage, 0.5f)); // ×1.5
            yield return null;

            var e = spawner.SpawnAt(MakeEnemy(hp: 1000), new Vector2(2f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            sc.Use();

            Assert.AreEqual(1000f - 60f, e.Hp, 0.01f);
        }

        [UnityTest]
        public IEnumerator Skill_Roar_StunsAndDamagesEnemiesInRadius()
        {
            var skill = MakeSkill(SkillType.Roar);
            skill.radius = 7f; skill.damage = 15f; skill.duration = 1f;
            var sc = AddSkillController(skill);
            yield return null;

            var near = spawner.SpawnAt(MakeEnemy(hp: 100), new Vector2(3f, 0f));
            var far = spawner.SpawnAt(MakeEnemy(hp: 100), new Vector2(11f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            sc.Use();

            Assert.IsTrue(near.IsStunned);
            Assert.AreEqual(85f, near.Hp, 0.001f);
            Assert.IsFalse(far.IsStunned);
        }

        [UnityTest]
        public IEnumerator Skill_Blessing_HealsAndAttractsAllGems()
        {
            var skill = MakeSkill(SkillType.Blessing);
            skill.power = 0.4f;
            var sc = AddSkillController(skill);
            yield return null;

            health.TakeDamage(60f);
            var farGem = gems.Spawn(new Vector2(12f, 0f), 2);
            Assert.IsFalse(farGem.Attracted);

            sc.Use();

            Assert.AreEqual(80f, health.Current, 0.001f, "최대 체력의 40% 회복");
            Assert.IsTrue(farGem.Attracted, "멀리 있는 보석도 끌려오기 시작");

            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, gems.ActiveCount, "모두 흡수");
            Assert.AreEqual(2, exp.Current);
        }

        [UnityTest]
        public IEnumerator Skill_Scheme_BuffsDamageAndCooldown_ForDuration()
        {
            var skill = MakeSkill(SkillType.Scheme);
            skill.power = 0.5f; skill.power2 = 0.3f; skill.duration = 0.6f;
            var sc = AddSkillController(skill);
            yield return null;

            sc.Use();
            Assert.AreEqual(1.5f, stats.DamageMultiplier, 0.0001f);
            Assert.AreEqual(0.7f, stats.CooldownMultiplier, 0.0001f);

            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(1f, stats.DamageMultiplier, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Skill_Warrior_GrantsInvulnerabilitySpeedAndAuraDamage()
        {
            var skill = MakeSkill(SkillType.Warrior);
            skill.radius = 2.6f; skill.damage = 10f; skill.power = 0.5f; skill.duration = 1f; skill.tickInterval = 0.2f;
            var sc = AddSkillController(skill);
            yield return null;

            var near = spawner.SpawnAt(MakeEnemy(hp: 1000), new Vector2(1.5f, 0f));
            var far = spawner.SpawnAt(MakeEnemy(hp: 1000), new Vector2(9f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            sc.Use();
            Assert.IsTrue(health.IsSkillInvulnerable);
            Assert.AreEqual(1.5f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.IsTrue(sc.AuraActive);

            yield return new WaitForSeconds(0.7f);

            Assert.LessOrEqual(near.Hp, 1000f - 20f, $"오라가 여러 번 피해를 줌 (HP {near.Hp})");
            Assert.AreEqual(1000f, far.Hp, 0.001f, "범위 밖은 영향 없음");

            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(sc.AuraActive);
            Assert.IsFalse(health.IsSkillInvulnerable);
            Assert.AreEqual(1f, stats.MoveSpeedMultiplier, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Skill_Cooldown_BlocksReuse_UntilElapsed()
        {
            var skill = MakeSkill(SkillType.Scheme, cooldown: 0.5f);
            skill.duration = 0.1f;
            var sc = AddSkillController(skill);
            yield return null;

            Assert.IsTrue(sc.IsReady);
            Assert.IsTrue(sc.Use());
            Assert.IsFalse(sc.IsReady);
            Assert.IsFalse(sc.Use(), "쿨다운 중에는 사용 불가");
            Assert.Greater(sc.CooldownRatio, 0.9f);

            yield return new WaitForSeconds(0.7f);

            Assert.IsTrue(sc.IsReady);
            Assert.AreEqual(0f, sc.CooldownRatio, 0.0001f);
            Assert.IsTrue(sc.Use());
        }

        [UnityTest]
        public IEnumerator Skill_Cooldown_RespectsCooldownPassive()
        {
            var skill = MakeSkill(SkillType.Scheme, cooldown: 10f);
            skill.duration = 0.1f;
            var sc = AddSkillController(skill);
            stats.AddPassive(MakePassive(PassiveType.Cooldown, 0.5f)); // ×0.5
            yield return null;

            sc.Use();

            Assert.AreEqual(5f, sc.CooldownLeft, 0.1f);
        }

        [UnityTest]
        public IEnumerator Skill_NotUsable_WithoutSkillOrWhenDead()
        {
            var sc = AddSkillController(null);
            yield return null;

            Assert.IsFalse(sc.HasSkill);
            Assert.IsFalse(sc.Use());

            var skill = MakeSkill(SkillType.Scheme);
            sc.SetSkill(skill);
            Assert.IsTrue(sc.IsReady);

            health.TakeDamage(1000f);
            Assert.IsFalse(sc.Use(), "죽은 뒤에는 사용 불가");
        }

        // ───────────────────────── 장수 선택 ─────────────────────────

        class FakeHeroView : IHeroSelectView
        {
            public int ShowCount, HideCount;
            public IReadOnlyList<HeroData> Heroes;
            public bool IsVisible { get; private set; }
            public void Show(IReadOnlyList<HeroData> heroes, Action<int> onChosen)
            {
                ShowCount++; Heroes = heroes; IsVisible = true;
            }
            public void Hide() { HideCount++; IsVisible = false; }
        }

        HeroCatalog MakeCatalog(params HeroData[] heroes)
        {
            var c = ScriptableObject.CreateInstance<HeroCatalog>();
            c.heroes.AddRange(heroes);
            toDestroy.Add(c);
            return c;
        }

        HeroSelectController MakeController(HeroCatalog catalog, IHeroSelectView view,
            out WeaponController wc, out SkillController sc)
        {
            wc = AddWeaponController();
            sc = AddSkillController(null);

            var go = new GameObject("TestHeroSelect");
            toDestroy.Add(go);
            go.SetActive(false);
            var c = go.AddComponent<HeroSelectController>();
            c.Catalog = catalog;
            c.Stats = stats;
            c.Health = health;
            c.Weapons = wc;
            c.Skills = sc;
            c.View = view;
            go.SetActive(true);
            return c;
        }

        [UnityTest]
        public IEnumerator HeroSelect_PausesAndShowsChoices_ThenAppliesChosenHero()
        {
            var weapon = MakeWeapon("쌍고검");
            var skill = MakeSkill(SkillType.Blessing);
            var a = MakeHero("유비", new Color(0.5f, 0.9f, 0.6f), weapon, skill);
            a.expMultiplier = 1.2f; a.maxHpBonus = 20f;
            var b = MakeHero("관우", new Color(0.2f, 0.6f, 0.3f), MakeWeapon("청룡언월도"), MakeSkill(SkillType.GreenDragonSlash));
            b.damageMultiplier = 1.15f;

            var view = new FakeHeroView();
            var ctrl = MakeController(MakeCatalog(a, b), view, out var wc, out var sc);
            HeroData reported = null;
            ctrl.HeroSelected += h => reported = h;

            yield return null; // Start → Begin

            Assert.IsTrue(ctrl.IsSelecting);
            Assert.AreEqual(1, view.ShowCount);
            Assert.AreEqual(2, view.Heroes.Count);
            Assert.AreEqual(0f, Time.timeScale, "선택 중에는 게임 정지");
            Assert.AreEqual(0, wc.Weapons.Count, "선택 전에는 무기 없음");

            ctrl.Choose(0);

            Assert.IsFalse(ctrl.IsSelecting);
            Assert.AreSame(a, ctrl.Current);
            Assert.AreSame(a, reported);
            Assert.AreSame(a, GameSession.SelectedHero);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(1, view.HideCount);
            Assert.AreEqual(1, wc.Weapons.Count);
            Assert.AreSame(weapon, wc.Weapons[0].Data, "시작 무기 지급");
            Assert.AreSame(skill, sc.Skill, "고유 스킬 장착");
            Assert.AreEqual(1.2f, stats.ExpMultiplier, 0.0001f);
            Assert.AreEqual(120f, health.Max, 0.001f);
            Assert.AreEqual(a.tint, body.color, "장수 색 적용");
        }

        [UnityTest]
        public IEnumerator HeroSelect_InvalidChoice_IsIgnored()
        {
            var view = new FakeHeroView();
            var a = MakeHero("유비", null, MakeWeapon("검"), MakeSkill(SkillType.Blessing));
            var ctrl = MakeController(MakeCatalog(a), view, out var wc, out _);

            ctrl.Choose(0); // 아직 선택 화면이 아님
            yield return null;
            ctrl.Choose(-1);
            ctrl.Choose(5);

            Assert.IsTrue(ctrl.IsSelecting);
            Assert.IsNull(ctrl.Current);
            Assert.AreEqual(0, wc.Weapons.Count);
        }

        [UnityTest]
        public IEnumerator HeroSelect_WithoutView_AutoAppliesLastOrFirstHero()
        {
            var a = MakeHero("유비", null, MakeWeapon("검"), MakeSkill(SkillType.Blessing));
            var b = MakeHero("관우", null, MakeWeapon("도"), MakeSkill(SkillType.GreenDragonSlash));
            GameSession.SelectedHero = b;

            var ctrl = MakeController(MakeCatalog(a, b), null, out var wc, out var sc);
            yield return null;

            Assert.AreSame(b, ctrl.Current, "화면이 없으면 마지막으로 고른 장수를 적용");
            Assert.IsFalse(ctrl.IsSelecting);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(1, wc.Weapons.Count);
        }

        [UnityTest]
        public IEnumerator HeroSelect_EmptyCatalog_DoesNothing()
        {
            var view = new FakeHeroView();
            var ctrl = MakeController(MakeCatalog(), view, out _, out _);
            yield return null;

            Assert.AreEqual(0, view.ShowCount);
            Assert.IsFalse(ctrl.IsSelecting);
            Assert.AreEqual(1f, Time.timeScale);
        }

        // ───────────────────────── UI ─────────────────────────

        [Test]
        public void HeroStatSummary_ListsOnlyNonDefaultStats()
        {
            var plain = MakeHero("평범");
            Assert.AreEqual("", plain.StatSummary());

            var h = MakeHero("관우");
            h.damageMultiplier = 1.15f; h.moveSpeedMultiplier = 0.95f; h.maxHpBonus = -20f; h.pickupRadiusMultiplier = 1.3f;
            string s = h.StatSummary();

            StringAssert.Contains("공격력 +15%", s);
            StringAssert.Contains("이동속도 -5%", s);
            StringAssert.Contains("체력 -20", s);
            StringAssert.Contains("획득 범위 +30%", s);
            StringAssert.DoesNotContain("경험치", s);
        }

        [Test]
        public void HeroSelectUI_ShowsHeroInfo_AndClickReportsIndex()
        {
            var uiGo = new GameObject("TestHeroUI");
            toDestroy.Add(uiGo);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(uiGo.transform);

            var cards = new HeroCardView[3];
            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject($"Card{i}", typeof(RectTransform));
                cardGo.transform.SetParent(panel.transform);
                var btn = cardGo.AddComponent<Button>();
                Text T(string n)
                {
                    var t = new GameObject(n, typeof(RectTransform)).AddComponent<Text>();
                    t.transform.SetParent(cardGo.transform);
                    return t;
                }
                var img = new GameObject("Portrait", typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(cardGo.transform);
                cards[i] = new HeroCardView
                {
                    button = btn, hotkey = T("H"), heroName = T("N"), title = T("T"), description = T("D"), portrait = img
                };
            }

            var ui = uiGo.AddComponent<HeroSelectUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel;
            var arr = so.FindProperty("cards");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = cards[i].button;
                el.FindPropertyRelative("hotkey").objectReferenceValue = cards[i].hotkey;
                el.FindPropertyRelative("heroName").objectReferenceValue = cards[i].heroName;
                el.FindPropertyRelative("title").objectReferenceValue = cards[i].title;
                el.FindPropertyRelative("description").objectReferenceValue = cards[i].description;
                el.FindPropertyRelative("portrait").objectReferenceValue = cards[i].portrait;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var skill = MakeSkill(SkillType.Roar);
            skill.displayName = "일갈"; skill.description = "적을 기절시킨다.";
            var hero = MakeHero("장비", new Color(0.4f, 0.4f, 0.6f), MakeWeapon("장팔사모"), skill);
            hero.title = "만인지적"; hero.description = "강인하다."; hero.maxHpBonus = 50f;
            var other = MakeHero("유비");

            int picked = -1;
            ui.Show(new List<HeroData> { hero, other }, i => picked = i);

            Assert.IsTrue(ui.IsVisible);
            Assert.AreEqual("[1]", cards[0].hotkey.text);
            Assert.AreEqual("장비", cards[0].heroName.text);
            Assert.AreEqual("만인지적", cards[0].title.text);
            StringAssert.Contains("체력 +50", cards[0].description.text);
            StringAssert.Contains("무기: 장팔사모", cards[0].description.text);
            StringAssert.Contains("스킬: 일갈", cards[0].description.text);
            Assert.AreEqual(hero.tint, cards[0].portrait.color);
            Assert.IsFalse(cards[2].button.gameObject.activeSelf, "장수가 2명이면 세 번째 카드는 숨김");

            cards[1].button.onClick.Invoke();
            Assert.AreEqual(1, picked);

            ui.Hide();
            Assert.IsFalse(ui.IsVisible);
        }

        // ───────────────────────── 실제 에셋 ─────────────────────────

        [Test]
        public void HeroCatalogAsset_HasFiveCompleteHeroes()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HeroCatalog>(HeroCatalogPath);
            Assert.IsNotNull(catalog, "장수 카탈로그가 없습니다. Samkuk > Step 8 를 먼저 실행하세요.");
            Assert.AreEqual(5, catalog.heroes.Count);

            var names = new HashSet<string>();
            var skillTypes = new HashSet<SkillType>();
            foreach (var h in catalog.heroes)
            {
                Assert.IsNotNull(h, "장수 데이터 누락");
                Assert.IsFalse(string.IsNullOrEmpty(h.displayName));
                Assert.IsNotNull(h.startingWeapon, $"{h.displayName}: 시작 무기 누락");
                Assert.IsNotNull(h.skill, $"{h.displayName}: 스킬 누락");
                Assert.Greater(h.skill.cooldown, 0f, $"{h.displayName}: 스킬 쿨다운");
                names.Add(h.displayName);
                skillTypes.Add(h.skill.type);
            }
            Assert.AreEqual(5, names.Count, "장수 이름은 서로 달라야 함");
            Assert.AreEqual(5, skillTypes.Count, "장수마다 다른 종류의 고유 스킬");
        }
    }
}
