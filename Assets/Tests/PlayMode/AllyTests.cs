using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Allies;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Heroes;
using Samkuk.Player;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    /// <summary>아군(동행 장수): 따라다니기, 자동 공격, 적의 공격 대상, 쓰러짐/부활, 선택 화면.</summary>
    public class AllyTests
    {
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const string EnemyProjectilePrefabPath = "Assets/Prefabs/EnemyProjectile.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        GameObject root;
        GameObject playerGo;
        GameObject camGo;
        EnemyManager manager;
        EnemySpawner spawner;
        EnemyProjectileSystem projectiles;
        AllyManager allies;
        PlayerHealth health;
        PlayerStats stats;
        WeaponController playerWeapons;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.SelectedHero = null;
            GameSession.SetAllies(null);

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            var enemyProjectile = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyProjectilePrefabPath)?.GetComponent<EnemyProjectile>();
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)?.GetComponent<Projectile>();
            Assert.IsNotNull(enemyPrefab, "Enemy 프리팹이 없습니다. Step 3 를 먼저 실행하세요.");
            Assert.IsNotNull(enemyProjectile, "EnemyProjectile 프리팹이 없습니다. Step 8-3 를 먼저 실행하세요.");
            Assert.IsNotNull(projectile, "Projectile 프리팹이 없습니다. Step 5 를 먼저 실행하세요.");

            playerGo = new GameObject("TestPlayer");
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(playerGo.transform, false);
            bodyGo.AddComponent<SpriteRenderer>();
            playerGo.AddComponent<PlayerController>(); // Rigidbody2D 도 함께 붙는다
            stats = playerGo.AddComponent<PlayerStats>();
            health = playerGo.AddComponent<PlayerHealth>();
            playerWeapons = playerGo.AddComponent<WeaponController>();
            playerWeapons.ProjectilePrefab = projectile;

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
            allies = root.AddComponent<AllyManager>();
            manager.Target = playerGo.transform;
            manager.Projectiles = projectiles;
            spawner.Manager = manager;
            spawner.Cam = cam;
            spawner.EnemyPrefab = enemyPrefab;
            spawner.autoSpawn = false;
            projectiles.Prefab = enemyProjectile;
            projectiles.Target = playerGo.transform;
            allies.Player = playerGo.GetComponent<PlayerController>();
            allies.PlayerWeapons = playerWeapons;
            allies.Enemies = manager;
            root.SetActive(true);
            playerWeapons.EnemyManager = manager;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameSession.SelectedHero = null;
            GameSession.SetAllies(null);
            if (root != null) UnityEngine.Object.Destroy(root);
            if (playerGo != null) UnityEngine.Object.Destroy(playerGo);
            if (camGo != null) UnityEngine.Object.Destroy(camGo);
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        WeaponData MakeSlash(float damage = 20f)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.type = WeaponType.Slash;
            w.displayName = "테스트 검";
            w.damage = damage; w.cooldown = 0.3f; w.range = 2.4f; w.count = 1; w.duration = 0.1f; w.maxLevel = 8;
            toDestroy.Add(w);
            return w;
        }

        HeroData MakeHero(string name, bool withWeapon = true, float damageMultiplier = 1f, float maxHpBonus = 0f)
        {
            var h = ScriptableObject.CreateInstance<HeroData>();
            h.displayName = name;
            h.damageMultiplier = damageMultiplier;
            h.maxHpBonus = maxHpBonus;
            h.startingWeapon = withWeapon ? MakeSlash() : null;
            toDestroy.Add(h);
            return h;
        }

        EnemyData MakeEnemy(float speed, int contactDamage, int hp = 10)
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            d.maxHp = hp; d.moveSpeed = speed; d.contactDamage = contactDamage; d.colliderRadius = 0.3f;
            toDestroy.Add(d);
            return d;
        }

        AllyController SpawnOne(HeroData hero)
        {
            allies.Spawn(new List<HeroData> { hero });
            return allies.Allies[0];
        }

        void MovePlayerTo(Vector2 position)
        {
            playerGo.transform.position = position;
            playerGo.GetComponent<Rigidbody2D>().position = position;
        }

        // ───────────────────────── 규칙 / 수치 ─────────────────────────

        [Test]
        public void WeaponLevel_RisesEveryThreePlayerLevels()
        {
            Assert.AreEqual(1, AllyConfig.WeaponLevelFor(1));
            Assert.AreEqual(1, AllyConfig.WeaponLevelFor(3));
            Assert.AreEqual(2, AllyConfig.WeaponLevelFor(4));
            Assert.AreEqual(3, AllyConfig.WeaponLevelFor(7));
        }

        // ───────────────────────── 생성 ─────────────────────────

        [UnityTest]
        public IEnumerator Spawn_PlacesAlliesBesidePlayer_UpToTheLimit_AndSkipsDuplicates()
        {
            yield return null;
            var a = MakeHero("관우");
            var b = MakeHero("장비");
            var c = MakeHero("조조");

            allies.Spawn(new List<HeroData> { a, a, b, c });

            Assert.AreEqual(AllyConfig.MaxAllies, allies.Allies.Count, "정원까지만, 중복은 건너뜀");
            Assert.AreEqual(a, allies.Allies[0].Hero);
            Assert.AreEqual(b, allies.Allies[1].Hero);
            Assert.AreEqual(AllyConfig.MaxAllies, manager.ExtraTargets.Count, "적이 노릴 수 있도록 등록됨");

            for (int i = 0; i < allies.Allies.Count; i++)
            {
                Vector2 expected = (Vector2)playerGo.transform.position + AllyConfig.OffsetOf(i);
                Assert.AreEqual(expected.x, allies.Allies[i].Position.x, 0.01f);
                Assert.AreEqual(expected.y, allies.Allies[i].Position.y, 0.01f);
            }
        }

        [UnityTest]
        public IEnumerator Spawn_ReplacesPreviousAllies_AndClearUnregisters()
        {
            yield return null;
            allies.Spawn(new List<HeroData> { MakeHero("관우"), MakeHero("장비") });
            var first = allies.Allies[0];

            allies.Spawn(new List<HeroData> { MakeHero("조조") });
            yield return null;

            Assert.AreEqual(1, allies.Allies.Count);
            Assert.IsTrue(first == null, "이전 아군은 사라짐");
            Assert.AreEqual(1, manager.ExtraTargets.Count);

            allies.Clear();
            Assert.AreEqual(0, allies.Allies.Count);
            Assert.AreEqual(0, manager.ExtraTargets.Count);
        }

        [UnityTest]
        public IEnumerator Ally_HasHeroHp_AndShowsNoSheetFallback()
        {
            yield return null;
            var hero = MakeHero("장비", maxHpBonus: 30f);
            var ally = SpawnOne(hero);

            Assert.AreEqual(AllyConfig.BaseHp + 30f, ally.MaxHp, 0.001f);
            Assert.AreEqual(ally.MaxHp, ally.Hp, 0.001f);
            Assert.IsNotNull(ally.Weapon, "시작 무기를 가짐");
            Assert.AreEqual(hero.tint, ally.Body.color, "걷기 그림이 없으면 장수 색");
        }

        // ───────────────────────── 따라다니기 ─────────────────────────

        [UnityTest]
        public IEnumerator Ally_FollowsThePlayer_AndStandsStillWhenArrived()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", withWeapon: false));
            MovePlayerTo(new Vector2(6f, 0f));

            yield return new WaitForSeconds(2.5f);

            Vector2 goal = (Vector2)playerGo.transform.position + AllyConfig.OffsetOf(0);
            Assert.Less(Vector2.Distance(ally.Position, goal), 0.3f, "자기 자리로 따라붙음");
            yield return null;
            Assert.AreEqual(0, ally.WalkFrame, "도착해서 서 있으면 서 있는 자세");
        }

        [UnityTest]
        public IEnumerator Ally_TeleportsBesidePlayer_WhenLeftFarBehind()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", withWeapon: false));
            MovePlayerTo(new Vector2(40f, 0f));

            yield return null;
            yield return null;

            Assert.Less(Vector2.Distance(ally.Position, playerGo.transform.position), 2.5f);
        }

        // ───────────────────────── 공격 ─────────────────────────

        [UnityTest]
        public IEnumerator Ally_AttacksNearbyEnemies_WithItsOwnWeapon_AtReducedDamage()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", damageMultiplier: 1f));
            Assert.AreEqual(20f * AllyConfig.DamageFactor, ally.Weapon.Damage, 0.001f, "아군 공격력은 배율이 곱해진다");

            // 아군 바로 앞(오른쪽)에 서 있는 적
            var still = MakeEnemy(speed: 0f, contactDamage: 0, hp: 10);
            var target = spawner.SpawnAt(still, ally.Position + new Vector2(1.4f, 0f));

            yield return new WaitForSeconds(1.5f);

            Assert.IsFalse(target.Alive, "아군의 검에 쓰러짐 (12 피해 x 여러 번)");
        }

        [UnityTest]
        public IEnumerator DownedAlly_StopsAttacking()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우"));
            ally.ReviveDuration = 100f;
            ally.Down();

            var still = MakeEnemy(speed: 0f, contactDamage: 0, hp: 10);
            var target = spawner.SpawnAt(still, ally.Position + new Vector2(1.4f, 0f));
            yield return new WaitForSeconds(1.2f);

            Assert.IsTrue(target.Alive, "쓰러진 아군은 공격하지 못함");
            Assert.IsFalse(ally.Weapon.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Ally_WeaponLevelsUp_WithPlayerLevel()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우"));
            Assert.AreEqual(1, ally.Weapon.Level);

            ally.SetPlayerLevel(4);
            Assert.AreEqual(2, ally.Weapon.Level);
            ally.SetPlayerLevel(7);
            Assert.AreEqual(3, ally.Weapon.Level);
            ally.SetPlayerLevel(2);
            Assert.AreEqual(3, ally.Weapon.Level, "내려가지는 않음");
        }

        // ───────────────────────── 적의 공격 대상 ─────────────────────────

        [UnityTest]
        public IEnumerator Enemy_ChasesAndHurtsTheNearerAlly_NotThePlayer()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", withWeapon: false)); // 플레이어(0,0) 왼쪽 아래 (-1.4,-0.5)
            var attacker = MakeEnemy(speed: 2f, contactDamage: 10, hp: 1000);

            // 아군 쪽에서 오는 적: 플레이어보다 아군이 가깝다
            spawner.SpawnAt(attacker, new Vector2(-6f, -0.5f));
            yield return new WaitForSeconds(4f);

            Assert.Less(ally.Hp, ally.MaxHp, "아군이 접촉 피해를 입음");
            Assert.AreEqual(health.Max, health.Current, 0.001f, "플레이어는 맞지 않음");
        }

        [UnityTest]
        public IEnumerator Ally_TakesReducedDamage_AndIsInvulnerableBriefly()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", withWeapon: false));

            Assert.IsTrue(ally.TryContactDamage(10f));
            Assert.AreEqual(ally.MaxHp - 10f * AllyConfig.DamageTakenFactor, ally.Hp, 0.001f);
            Assert.IsTrue(ally.IsInvulnerable);
            Assert.IsFalse(ally.TryContactDamage(10f), "피격 직후 무적 시간에는 무시");
            Assert.AreEqual(ally.MaxHp - 10f * AllyConfig.DamageTakenFactor, ally.Hp, 0.001f);

            yield return new WaitForSeconds(AllyConfig.HitInvulnerableSeconds + 0.2f);
            Assert.IsTrue(ally.TryContactDamage(10f), "무적이 끝나면 다시 맞음");
        }

        [UnityTest]
        public IEnumerator DownedAlly_IsIgnored_SoEnemiesGoForThePlayer()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", withWeapon: false));
            ally.ReviveDuration = 100f;
            ally.Down();
            Assert.IsFalse(ally.IsTargetable);

            var attacker = MakeEnemy(speed: 2f, contactDamage: 10, hp: 1000);
            spawner.SpawnAt(attacker, new Vector2(-6f, -0.5f));
            yield return new WaitForSeconds(4f);

            Assert.Less(health.Current, health.Max, "쓰러진 아군은 건너뛰고 플레이어를 공격");
            Assert.AreEqual(0f, ally.Hp, 0.001f);
        }

        [UnityTest]
        public IEnumerator EnemyArrows_CanHitAnAlly()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우", withWeapon: false));

            var d = ScriptableObject.CreateInstance<EnemyData>();
            toDestroy.Add(d);
            d.role = EnemyRole.Archer;
            d.maxHp = 1000; d.moveSpeed = 0f; d.contactDamage = 0; d.colliderRadius = 0.3f;
            d.attackRange = 4f; d.fireInterval = 0.5f;
            d.projectileSpeed = 12f; d.projectileDamage = 6; d.projectileLifetime = 3f; d.projectileSize = 0.2f;

            // 아군(-1.4,-0.5)이 플레이어보다 가까운 쪽에 선 궁병
            spawner.SpawnAt(d, new Vector2(-4.5f, -0.5f));
            yield return new WaitForSeconds(2.5f);

            Assert.Less(ally.Hp, ally.MaxHp, "화살에 맞음");
            Assert.AreEqual(health.Max, health.Current, 0.001f, "플레이어는 맞지 않음");
        }

        // ───────────────────────── 쓰러짐 / 부활 ─────────────────────────

        [UnityTest]
        public IEnumerator Ally_GoesDownAtZeroHp_ThenRevivesBesideThePlayer()
        {
            yield return null;
            var ally = SpawnOne(MakeHero("관우"));
            ally.ReviveDuration = 0.5f;
            int downs = 0, revives = 0;
            ally.Downed += _ => downs++;
            ally.Revived += _ => revives++;

            ally.TakeDamage(ally.MaxHp + 1f);

            Assert.IsTrue(ally.IsDowned);
            Assert.AreEqual(0f, ally.Hp, 0.001f);
            Assert.IsFalse(ally.IsTargetable);
            Assert.IsFalse(ally.TryContactDamage(10f), "쓰러진 아군은 더 맞지 않음");
            Assert.AreEqual(1, downs);

            MovePlayerTo(new Vector2(3f, 0f)); // 쓰러진 사이 플레이어는 이동
            yield return new WaitForSeconds(0.9f);

            Assert.IsFalse(ally.IsDowned);
            Assert.AreEqual(1, revives);
            Assert.AreEqual(ally.MaxHp * AllyConfig.ReviveHpRatio, ally.Hp, 0.5f, "체력 일부만 회복");
            Assert.IsTrue(ally.IsInvulnerable, "되살아난 직후 잠깐 무적");
            Assert.IsTrue(ally.IsTargetable);
            Assert.IsTrue(ally.Weapon.gameObject.activeSelf);
            Assert.Less(Vector2.Distance(ally.Position, playerGo.transform.position), 3f, "플레이어 곁에서 부활");
        }

        // ───────────────────────── 아군 선택 화면 ─────────────────────────

        class Fixture
        {
            public AllySelectUI ui;
            public GameObject panel;
            public AllyCardView[] cards;
            public Text count;
            public Button confirm;
        }

        Fixture BuildAllyUi(int cardCount)
        {
            var uiGo = new GameObject("TestAllyUI");
            toDestroy.Add(uiGo);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(uiGo.transform);

            var f = new Fixture { panel = panel, cards = new AllyCardView[cardCount] };
            for (int i = 0; i < cardCount; i++)
            {
                var cardGo = new GameObject($"Card{i}", typeof(RectTransform));
                cardGo.transform.SetParent(panel.transform);
                Text T(string n)
                {
                    var t = new GameObject(n, typeof(RectTransform)).AddComponent<Text>();
                    t.transform.SetParent(cardGo.transform);
                    return t;
                }
                var img = new GameObject("Portrait", typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(cardGo.transform);
                var mark = new GameObject("Mark");
                mark.transform.SetParent(cardGo.transform);
                f.cards[i] = new AllyCardView
                {
                    button = cardGo.AddComponent<Button>(), hotkey = T("H"), heroName = T("N"), title = T("T"),
                    description = T("D"), portrait = img, selectedMark = mark
                };
            }
            f.count = new GameObject("Count", typeof(RectTransform)).AddComponent<Text>();
            f.count.transform.SetParent(panel.transform);
            f.confirm = new GameObject("Confirm", typeof(RectTransform)).AddComponent<Button>();
            f.confirm.transform.SetParent(panel.transform);

            uiGo.SetActive(false);
            f.ui = uiGo.AddComponent<AllySelectUI>();
            var so = new SerializedObject(f.ui);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("countLabel").objectReferenceValue = f.count;
            so.FindProperty("confirmButton").objectReferenceValue = f.confirm;
            var arr = so.FindProperty("cards");
            arr.arraySize = cardCount;
            for (int i = 0; i < cardCount; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = f.cards[i].button;
                el.FindPropertyRelative("hotkey").objectReferenceValue = f.cards[i].hotkey;
                el.FindPropertyRelative("heroName").objectReferenceValue = f.cards[i].heroName;
                el.FindPropertyRelative("title").objectReferenceValue = f.cards[i].title;
                el.FindPropertyRelative("description").objectReferenceValue = f.cards[i].description;
                el.FindPropertyRelative("portrait").objectReferenceValue = f.cards[i].portrait;
                el.FindPropertyRelative("selectedMark").objectReferenceValue = f.cards[i].selectedMark;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            uiGo.SetActive(true);
            return f;
        }

        // 카드 상태는 직렬화된 복사본이므로 UI 오브젝트에서 다시 읽는다
        static bool MarkOn(Fixture f, int i) => f.cards[i].selectedMark.activeSelf;

        [Test]
        public void SelectUI_ShowsCandidates_TogglesAndKeepsTheLimit()
        {
            var f = BuildAllyUi(4);
            var heroes = new List<HeroData> { MakeHero("관우"), MakeHero("장비"), MakeHero("조조") };
            IReadOnlyList<HeroData> result = null;

            f.ui.Show(heroes, 2, null, r => result = r);

            Assert.IsTrue(f.ui.IsVisible);
            Assert.AreEqual("관우", f.cards[0].heroName.text);
            Assert.AreEqual("[2]", f.cards[1].hotkey.text);
            Assert.IsTrue(f.cards[2].button.gameObject.activeSelf);
            Assert.IsFalse(f.cards[3].button.gameObject.activeSelf, "후보가 3명이면 네 번째 카드는 숨김");
            StringAssert.Contains("(0/2)", f.count.text);

            f.cards[0].button.onClick.Invoke();
            f.cards[1].button.onClick.Invoke();
            Assert.IsTrue(MarkOn(f, 0) && MarkOn(f, 1));
            StringAssert.Contains("(2/2)", f.count.text);

            f.cards[2].button.onClick.Invoke(); // 정원 초과: 먼저 고른 장수가 빠지고 새로 들어옴
            Assert.IsFalse(MarkOn(f, 0));
            Assert.IsTrue(MarkOn(f, 1) && MarkOn(f, 2));

            f.cards[1].button.onClick.Invoke(); // 다시 누르면 해제
            Assert.IsFalse(MarkOn(f, 1));
            Assert.AreEqual(1, f.ui.PickedCount);

            f.confirm.onClick.Invoke();
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("조조", result[0].displayName);
        }

        [Test]
        public void SelectUI_PreselectsPreviousAllies_AndAllowsZero()
        {
            var f = BuildAllyUi(4);
            var a = MakeHero("관우"); var b = MakeHero("장비"); var c = MakeHero("조조");
            IReadOnlyList<HeroData> result = null;

            f.ui.Show(new List<HeroData> { a, b, c }, 2, new List<HeroData> { c, MakeHero("목록에 없음") }, r => result = r);

            Assert.IsTrue(MarkOn(f, 2), "지난번에 고른 장수는 미리 선택됨");
            Assert.AreEqual(1, f.ui.PickedCount, "목록에 없는 장수는 무시");

            f.cards[2].button.onClick.Invoke();
            f.confirm.onClick.Invoke();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "0명으로도 출발할 수 있음");
        }

        // ───────────────────────── 장수 선택 → 아군 선택 흐름 ─────────────────────────

        class FakeHeroView : IHeroSelectView
        {
            public bool IsVisible { get; private set; }
            public Action<int> OnChosen;
            public void Show(IReadOnlyList<HeroData> heroes, Action<int> onChosen) { IsVisible = true; OnChosen = onChosen; }
            public void Hide() => IsVisible = false;
        }

        class FakeAllyView : IAllySelectView
        {
            public bool IsVisible { get; private set; }
            public IReadOnlyList<HeroData> Candidates;
            public IReadOnlyList<HeroData> Preselected;
            public int MaxPick;
            public Action<IReadOnlyList<HeroData>> OnConfirmed;
            public void Show(IReadOnlyList<HeroData> candidates, int maxPick, IReadOnlyList<HeroData> preselected,
                Action<IReadOnlyList<HeroData>> onConfirmed)
            {
                IsVisible = true; Candidates = candidates; MaxPick = maxPick; Preselected = preselected; OnConfirmed = onConfirmed;
            }
            public void Hide() => IsVisible = false;
        }

        HeroSelectController MakeController(HeroCatalog catalog, IHeroSelectView view, IAllySelectView allyView, AllyManager am)
        {
            var go = new GameObject("TestHeroSelect");
            toDestroy.Add(go);
            go.SetActive(false);
            var c = go.AddComponent<HeroSelectController>();
            c.Catalog = catalog;
            c.Stats = stats;
            c.Health = health;
            c.Weapons = playerWeapons;
            c.View = view;
            c.AllyView = allyView;
            c.Allies = am;
            go.SetActive(true);
            return c;
        }

        HeroCatalog MakeCatalog(params HeroData[] heroes)
        {
            var c = ScriptableObject.CreateInstance<HeroCatalog>();
            c.heroes.AddRange(heroes);
            toDestroy.Add(c);
            return c;
        }

        [UnityTest]
        public IEnumerator HeroSelect_ThenAllySelect_PausesUntilConfirmed_AndSpawnsTheChosenAllies()
        {
            var liu = MakeHero("유비"); var guan = MakeHero("관우"); var zhang = MakeHero("장비");
            var heroView = new FakeHeroView();
            var allyView = new FakeAllyView();
            var ctrl = MakeController(MakeCatalog(liu, guan, zhang), heroView, allyView, allies);
            IReadOnlyList<HeroData> reported = null;
            ctrl.AlliesSelected += r => reported = r;
            yield return null;

            Assert.AreEqual(0f, Time.timeScale);
            heroView.OnChosen(0); // 유비 선택

            Assert.IsFalse(heroView.IsVisible);
            Assert.IsTrue(allyView.IsVisible, "이어서 아군 선택 화면");
            Assert.AreEqual(0f, Time.timeScale, "아군을 고르는 동안은 계속 멈춤");
            Assert.IsTrue(ctrl.IsSelecting);
            CollectionAssert.AreEqual(new[] { guan, zhang }, allyView.Candidates, "고른 장수는 후보에서 빠짐");
            Assert.AreEqual(AllyConfig.MaxAllies, allyView.MaxPick);

            heroView.OnChosen(1); // 아군 선택 중에는 장수를 다시 고를 수 없다
            Assert.AreEqual(liu, ctrl.Current);

            allyView.OnConfirmed(new List<HeroData> { zhang });

            Assert.IsFalse(allyView.IsVisible);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(ctrl.IsSelecting);
            Assert.AreEqual(1, allies.Allies.Count);
            Assert.AreEqual(zhang, allies.Allies[0].Hero);
            CollectionAssert.AreEqual(new[] { zhang }, GameSession.SelectedAllies, "다음 판을 위해 기억");
            Assert.AreEqual(1, reported.Count);
        }

        [UnityTest]
        public IEnumerator HeroSelect_ShowsPreviousAlliesPreselected_OnTheNextRun()
        {
            var liu = MakeHero("유비"); var guan = MakeHero("관우"); var zhang = MakeHero("장비");
            GameSession.SetAllies(new List<HeroData> { guan });
            var heroView = new FakeHeroView();
            var allyView = new FakeAllyView();
            MakeController(MakeCatalog(liu, guan, zhang), heroView, allyView, allies);
            yield return null;

            heroView.OnChosen(0);

            CollectionAssert.AreEqual(new[] { guan }, allyView.Preselected);
        }

        [UnityTest]
        public IEnumerator HeroSelect_WithoutAllyView_StartsImmediately()
        {
            var liu = MakeHero("유비"); var guan = MakeHero("관우");
            var heroView = new FakeHeroView();
            var ctrl = MakeController(MakeCatalog(liu, guan), heroView, null, allies);
            yield return null;

            heroView.OnChosen(0);

            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(ctrl.IsSelecting);
            Assert.AreEqual(0, allies.Allies.Count);
        }

        [UnityTest]
        public IEnumerator HeroSelect_WithoutViews_TakesLastAlliesAlong_ExceptTheChosenHero()
        {
            var liu = MakeHero("유비"); var guan = MakeHero("관우"); var zhang = MakeHero("장비");
            GameSession.SelectedHero = liu;
            GameSession.SetAllies(new List<HeroData> { liu, guan, zhang });

            MakeController(MakeCatalog(liu, guan, zhang), null, null, allies); // 화면 없이 바로 시작
            yield return null;

            Assert.AreEqual(2, allies.Allies.Count);
            Assert.AreEqual(guan, allies.Allies[0].Hero, "고른 장수(유비)는 아군에서 빠짐");
            Assert.AreEqual(zhang, allies.Allies[1].Hero);
        }
    }
}
