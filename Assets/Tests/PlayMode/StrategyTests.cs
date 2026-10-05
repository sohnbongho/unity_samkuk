using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.Strategy;
using Samkuk.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    /// <summary>내정 화면(Step 12-2): 성 선택/입장/이동 규칙과 지도 UI.</summary>
    public class StrategyTests
    {
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();
        string savePath;

        [SetUp]
        public void SetUp()
        {
            // 내 성 등을 저장하므로 실제 저장 파일을 건드리지 않도록 임시 경로를 쓴다
            savePath = Path.Combine(Application.temporaryCachePath, $"test_strategy_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
            StrategySession.LastCastleId = null;
            GameSession.SortieCastle = null;
        }

        [TearDown]
        public void TearDown()
        {
            StrategySession.LastCastleId = null;
            GameSession.SortieCastle = null;
            GameSession.SortieOrigin = null;
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            AudioManager.DestroyInstance();
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
            toDestroy.Clear();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        CastleData MakeCastle(string id, float x, float y)
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.name = id; c.id = id; c.displayName = "성" + id; c.hanja = "城";
            c.mapPosition = new Vector2(x, y);
            toDestroy.Add(c);
            return c;
        }

        static void Link(CastleData a, CastleData b)
        {
            a.neighbors.Add(b);
            b.neighbors.Add(a);
        }

        /// <summary>A - B - C 가 이어지고 D 는 외톨이인 작은 지도.</summary>
        CastleCatalog MakeCatalog(out CastleData a, out CastleData b, out CastleData c, out CastleData d)
        {
            a = MakeCastle("A", 0.2f, 0.3f);
            b = MakeCastle("B", 0.5f, 0.5f);
            c = MakeCastle("C", 0.8f, 0.3f);
            d = MakeCastle("D", 0.5f, 0.9f);
            Link(a, b); Link(b, c);
            var catalog = ScriptableObject.CreateInstance<CastleCatalog>();
            catalog.castles.AddRange(new[] { a, b, c, d });
            toDestroy.Add(catalog);
            return catalog;
        }

        StrategyUI MakeUi(CastleCatalog catalog, Sprite map = null)
        {
            var go = new GameObject("StrategyTestUi", typeof(RectTransform));
            toDestroy.Add(go);
            var ui = go.AddComponent<StrategyUI>();
            ui.Configure(catalog, map);
            return ui;
        }

        static Button Find(StrategyUI ui, string name)
        {
            foreach (var b in ui.GetComponentsInChildren<Button>(true))
                if (b.name == name) return b;
            return null;
        }

        // ───────────────────────── 모델 ─────────────────────────

        [Test]
        public void Model_Select_AcceptsCatalogCastle_RejectsNullAndForeign()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);
            var foreign = MakeCastle("X", 0.1f, 0.1f);

            Assert.IsFalse(model.Select(null));
            Assert.IsFalse(model.Select(foreign));
            Assert.IsNull(model.Selected);

            Assert.IsTrue(model.Select(a));
            Assert.AreSame(a, model.Selected);
            Assert.IsFalse(model.InCastle, "선택만으로는 성 안으로 들어가지 않는다");
        }

        [Test]
        public void Model_Enter_NeedsSelection_AndLeaveReturnsToMap()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);

            Assert.IsFalse(model.Enter(), "선택한 성이 없으면 들어갈 수 없다");
            Assert.IsFalse(model.Leave(), "성 안이 아니면 나올 것도 없다");

            model.Select(a);
            Assert.IsTrue(model.Enter());
            Assert.IsTrue(model.InCastle);
            Assert.IsFalse(model.Enter(), "이미 성 안이면 다시 들어가지 않는다");

            Assert.IsTrue(model.Leave());
            Assert.IsFalse(model.InCastle);
            Assert.AreSame(a, model.Selected, "지도로 돌아와도 고른 성은 유지된다");
        }

        [Test]
        public void Model_MoveTo_OnlyToAdjacentCastle_WhileInCastle()
        {
            var catalog = MakeCatalog(out var a, out var b, out var c, out var d);
            var model = new StrategyModel(catalog);
            model.Select(a);

            Assert.IsFalse(model.MoveTo(b), "성 밖(지도)에서는 이동하지 않는다");
            model.Enter();
            Assert.IsFalse(model.MoveTo(c), "인접하지 않은 성");
            Assert.IsFalse(model.MoveTo(d), "연결 없는 성");
            Assert.AreSame(a, model.Selected);

            Assert.IsTrue(model.MoveTo(b));
            Assert.AreSame(b, model.Selected);
            Assert.IsTrue(model.InCastle, "이동해도 성 화면은 유지된다");
            Assert.IsTrue(model.MoveTo(c));
        }

        [Test]
        public void Model_Changed_FiresOnlyOnRealChanges()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);
            int count = 0;
            model.Changed += () => count++;

            model.Select(null);
            model.Enter();
            model.Leave();
            Assert.AreEqual(0, count, "거절된 동작은 알리지 않는다");

            model.Select(a); model.Enter(); model.Leave();
            Assert.AreEqual(3, count);
        }

        [Test]
        public void Model_RemembersLastCastle_ButStartsOnMap()
        {
            var catalog = MakeCatalog(out _, out var b, out _, out _);
            var first = new StrategyModel(catalog);
            first.Select(b);
            first.Enter();

            var second = new StrategyModel(catalog); // 타이틀에 다녀온 뒤 다시 열었다고 보면
            Assert.AreSame(b, second.Selected);
            Assert.IsFalse(second.InCastle);
        }

        // ───────────────────────── 내 성 / 출진 (모델) ─────────────────────────

        [Test]
        public void Model_StartHere_SetsHome_Saves_AndEntersCastle()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);
            Assert.IsFalse(model.HasHome);
            Assert.IsFalse(model.StartHere(), "고른 성이 없으면 시작할 수 없다");

            model.Select(a);
            Assert.IsTrue(model.StartHere());
            Assert.AreSame(a, model.Home);
            Assert.IsTrue(model.IsHome(a));
            Assert.IsTrue(model.InCastle, "시작 성을 고르면 그 성 안으로 들어간다");
            Assert.AreEqual("A", SaveSystem.Current.homeCastleId);

            SaveSystem.ResetCache(); // 파일에서 다시 읽어도 유지
            Assert.AreEqual("A", SaveSystem.Current.homeCastleId);
        }

        [Test]
        public void Model_StartHere_IgnoredOnceHomeExists()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var model = new StrategyModel(catalog);
            model.Select(a);
            model.StartHere();
            model.Leave();

            model.Select(b);
            Assert.IsFalse(model.StartHere(), "내 성이 이미 있으면 다시 정하지 않는다");
            Assert.AreSame(a, model.Home);
        }

        [Test]
        public void Model_Home_PersistsAcrossModels_AndStartsSelectedOnMap()
        {
            var catalog = MakeCatalog(out _, out var b, out _, out _);
            var first = new StrategyModel(catalog);
            first.Select(b);
            first.StartHere();

            StrategySession.LastCastleId = null; // 앱을 다시 켠 것처럼
            var second = new StrategyModel(catalog);
            Assert.AreSame(b, second.Home);
            Assert.AreSame(b, second.Selected, "보던 성이 없으면 내 성이 선택된 채로 열린다");
            Assert.IsFalse(second.InCastle, "성 안이 아니라 지도에서 시작");
        }

        [Test]
        public void Model_ClearHome_ForgetsHome_AndLeavesCastle()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);
            Assert.IsFalse(model.ClearHome(), "내 성이 없으면 할 일이 없다");

            model.Select(a);
            model.StartHere();
            Assert.IsTrue(model.ClearHome());
            Assert.IsFalse(model.HasHome);
            Assert.IsFalse(model.InCastle);
            Assert.IsTrue(string.IsNullOrEmpty(SaveSystem.Current.homeCastleId));
        }

        // ───────────────────────── 영토 / 공격 (모델) ─────────────────────────

        [Test]
        public void Model_StartHere_AddsHomeToTerritory()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var model = new StrategyModel(catalog);
            Assert.AreEqual(0, model.OwnedCount);

            model.Select(a);
            model.StartHere();
            Assert.IsTrue(model.IsOwned(a));
            Assert.IsFalse(model.IsOwned(b));
            Assert.AreEqual(1, model.OwnedCount);
            Assert.AreEqual(4, model.TotalCount);
            Assert.IsFalse(model.IsUnified);
        }

        [Test]
        public void Model_AttackTargets_AreEnemyNeighborsOfSelectedOwnedCastle()
        {
            var catalog = MakeCatalog(out var a, out var b, out var c, out var d);
            var model = new StrategyModel(catalog);
            model.Select(a);
            model.StartHere(); // A 소유. 이웃은 B 뿐

            CollectionAssert.AreEqual(new[] { b }, model.AttackTargets());
            Assert.IsTrue(model.IsAttackable(b));
            Assert.IsFalse(model.IsAttackable(c), "우리 영토와 이어지지 않은 성은 공격할 수 없다");
            Assert.IsFalse(model.IsAttackable(d));
            Assert.IsFalse(model.IsAttackable(a), "내 성은 공격 대상이 아니다");

            model.Select(b); // 적 성을 보고 있을 때는 목록이 비어 있다
            CollectionAssert.IsEmpty(model.AttackTargets());
        }

        [Test]
        public void Model_Sortie_NeedsOwnedCastle_AndAdjacentEnemyTarget()
        {
            var catalog = MakeCatalog(out var a, out var b, out var c, out _);
            var model = new StrategyModel(catalog);
            CastleData sortied = null;
            model.SortieStarted += t => sortied = t;

            model.Select(a);
            model.StartHere(); // A 소유, 성 안
            Assert.IsTrue(model.CanSortie);

            Assert.IsFalse(model.Sortie(null));
            Assert.IsFalse(model.Sortie(a), "내 성은 공격할 수 없다");
            Assert.IsFalse(model.Sortie(c), "이웃하지 않은 성");
            Assert.IsNull(GameSession.SortieCastle);

            model.Leave();
            Assert.IsFalse(model.CanSortie, "지도에서는 출진할 수 없다");
            Assert.IsFalse(model.Sortie(b));

            model.Enter();
            Assert.IsTrue(model.Sortie(b));
            Assert.AreSame(b, GameSession.SortieCastle, "전투가 벌어지는 성은 공격 대상");
            Assert.AreSame(a, GameSession.SortieOrigin, "출발한 성");
            Assert.AreSame(b, sortied);
        }

        [Test]
        public void Model_CannotSortieFromEnemyCastle()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var model = new StrategyModel(catalog);
            model.Select(a);
            model.StartHere();
            model.MoveTo(b); // 적 성 안을 구경 중

            Assert.IsFalse(model.CanSortie);
            Assert.IsFalse(model.Sortie(a));
        }

        [Test]
        public void Model_AfterConquest_NewCastleBecomesSortiePoint()
        {
            var catalog = MakeCatalog(out var a, out var b, out var c, out _);
            var model = new StrategyModel(catalog);
            model.Select(a);
            model.StartHere();

            // 전투에서 이겨 B 를 차지했다고 보면 (정산은 ResultController 가 저장)
            Territory.Conquer(SaveSystem.Current, "B");
            SaveSystem.SaveCurrent();

            Assert.IsTrue(model.IsOwned(b));
            Assert.AreEqual(2, model.OwnedCount);
            Assert.IsTrue(model.IsAttackable(c), "B 와 이웃한 C 가 공격 대상이 된다");

            model.MoveTo(b);
            Assert.IsTrue(model.CanSortie, "차지한 성에서도 출진한다");
            CollectionAssert.AreEqual(new[] { c }, model.AttackTargets(), "이미 우리 성인 A 는 대상이 아니다");
        }

        [Test]
        public void Model_ClearHome_BlockedOnceAnotherCastleIsConquered()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);
            model.Select(a);
            model.StartHere();
            Territory.Conquer(SaveSystem.Current, "B");

            Assert.IsFalse(model.ClearHome(), "정복한 영토가 사라지므로 시작 성을 바꿀 수 없다");
            Assert.IsTrue(model.HasHome);
            Assert.AreEqual(2, model.OwnedCount);
        }

        [Test]
        public void Model_Unified_WhenEveryCastleIsOwned()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var model = new StrategyModel(catalog);
            model.Select(a);
            model.StartHere();
            foreach (var id in new[] { "B", "C", "D" }) Territory.Conquer(SaveSystem.Current, id);

            Assert.IsTrue(model.IsUnified);
        }

        [Test]
        public void Model_LegacySaveWithHomeButNoTerritory_CountsHomeAsOwned()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            SaveSystem.Current.homeCastleId = "A"; // 영토 기능 이전의 저장
            SaveSystem.Current.ownedCastleIds.Clear();

            var model = new StrategyModel(catalog);
            Assert.AreSame(a, model.Home);
            Assert.IsTrue(model.IsOwned(a));
            Assert.AreEqual(1, model.OwnedCount);
        }

        [Test]
        public void Territory_Conquer_IsIdempotent_AndReleaseRemoves()
        {
            var save = new SaveData();
            Assert.IsTrue(Territory.Conquer(save, "X"));
            Assert.IsFalse(Territory.Conquer(save, "X"), "이미 내 성");
            Assert.IsFalse(Territory.Conquer(save, ""), "빈 아이디는 무시");
            Assert.AreEqual(1, Territory.Count(save));
            Assert.IsTrue(Territory.IsOwned(save, "X"));
            Assert.IsTrue(Territory.Release(save, "X"));
            Assert.IsFalse(Territory.IsOwned(save, "X"));
        }

        // ───────────────────────── 화면 ─────────────────────────

        [UnityTest]
        public IEnumerator Ui_BuildsMarkerPerCastle_AndStartsOnMap()
        {
            var catalog = MakeCatalog(out _, out _, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            Assert.AreEqual(4, ui.MarkerCount);
            Assert.IsTrue(ui.IsMapVisible);
            Assert.IsFalse(ui.IsCastleVisible);
            Assert.IsNotNull(Find(ui, "Marker_A"));
            Assert.IsFalse(Find(ui, "EnterButton").interactable, "고른 성이 없으면 들어가기 버튼은 꺼져 있다");
        }

        [UnityTest]
        public IEnumerator Ui_ClickMarker_SelectsThenEnters()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            Find(ui, "Marker_A").onClick.Invoke();
            Assert.AreSame(a, ui.Model.Selected);
            Assert.IsTrue(ui.IsMapVisible, "처음 누르면 선택만 한다");
            Assert.IsTrue(Find(ui, "EnterButton").interactable);

            Find(ui, "Marker_A").onClick.Invoke();
            Assert.IsTrue(ui.IsCastleVisible, "선택한 성을 다시 누르면 들어간다");
            Assert.IsFalse(ui.IsMapVisible);
        }

        [UnityTest]
        public IEnumerator Ui_EnterButton_EntersSelectedCastle_AndMapButtonLeaves()
        {
            var catalog = MakeCatalog(out _, out var b, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(b);
            Find(ui, "EnterButton").onClick.Invoke();
            Assert.IsTrue(ui.IsCastleVisible);

            Find(ui, "MapButton").onClick.Invoke();
            Assert.IsTrue(ui.IsMapVisible);
            Assert.IsFalse(ui.IsCastleVisible);
        }

        [UnityTest]
        public IEnumerator Ui_CastleScreen_ShowsNeighborButtons_ThatMoveBetweenCastles()
        {
            var catalog = MakeCatalog(out _, out var b, out var c, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(b);
            ui.Model.Enter();
            yield return null;

            Assert.IsNotNull(Find(ui, "Neighbor_A"));
            Assert.IsNotNull(Find(ui, "Neighbor_C"));
            Assert.IsNull(Find(ui, "Neighbor_D"), "연결 없는 성의 버튼은 없다");

            Find(ui, "Neighbor_C").onClick.Invoke();
            yield return null; // 이전 버튼은 Destroy 로 한 프레임 뒤에 사라진다
            Assert.AreSame(c, ui.Model.Selected);
            Assert.IsNotNull(Find(ui, "Neighbor_B"), "옮겨 간 성의 인접 성으로 버튼이 바뀐다");
            Assert.IsNull(Find(ui, "Neighbor_C"));
        }

        [UnityTest]
        public IEnumerator Ui_BackToTitle_LoadsTitleScene()
        {
            var catalog = MakeCatalog(out _, out _, out _, out _);
            var ui = MakeUi(catalog);
            string loaded = null;
            ui.SceneLoader = name => loaded = name;
            yield return null;

            Find(ui, "BackButton").onClick.Invoke();
            Assert.AreEqual(GameManager.TitleSceneName, loaded);
        }

        [UnityTest]
        public IEnumerator Ui_RealCatalog_HasMarkerForEveryCastle_AndMapSprite()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CastleCatalog>("Assets/ScriptableObjects/CastleCatalog.asset");
            Assert.IsNotNull(catalog, "CastleCatalog 가 없습니다. 메뉴 Samkuk > Step 12-1 을 먼저 실행하세요.");
            var map = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Strategy/StrategyMap.png");
            Assert.IsNotNull(map, "전략 지도 그림이 없습니다. tools/castle_art/generate.ps1 -Only Map 후 Step 12-2 를 실행하세요.");
            Assert.AreEqual(16f / 9f, map.rect.width / map.rect.height, 0.01f, "전략 지도는 16:9 여야 합니다");

            var ui = MakeUi(catalog, map);
            yield return null;

            Assert.AreEqual(catalog.castles.Count, ui.MarkerCount);
            CollectionAssert.IsEmpty(ui.MissingReferences());
        }

        // ───────────────────────── 내 성 / 출진 (화면) ─────────────────────────

        [UnityTest]
        public IEnumerator Ui_WithoutHome_PrimaryButtonStartsAtSelectedCastle()
        {
            var catalog = MakeCatalog(out _, out var b, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(b);
            Assert.AreEqual("이 성에서 시작", Find(ui, "EnterButton").GetComponentInChildren<Text>().text);
            Assert.IsFalse(Find(ui, "ChangeHomeButton").gameObject.activeSelf, "내 성이 없으면 [시작 성 변경]은 숨김");

            Find(ui, "EnterButton").onClick.Invoke();
            Assert.AreSame(b, ui.Model.Home);
            Assert.IsTrue(ui.IsCastleVisible, "시작 성을 고르면 그 성 화면으로 들어간다");
        }

        [UnityTest]
        public IEnumerator Ui_SecondClickOnMarker_StartsHereWhenNoHome_ThenEntersAfterwards()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            Find(ui, "Marker_A").onClick.Invoke();
            Find(ui, "Marker_A").onClick.Invoke(); // 같은 성을 다시: 시작 성으로 정하고 들어간다
            Assert.AreSame(a, ui.Model.Home);
            Assert.IsTrue(ui.IsCastleVisible);

            ui.Model.Leave();
            Find(ui, "Marker_B").onClick.Invoke();
            Find(ui, "Marker_B").onClick.Invoke(); // 내 성이 이미 있으므로 들어가기만 한다
            Assert.AreSame(a, ui.Model.Home, "다른 성을 눌러도 내 성은 바뀌지 않는다");
            Assert.AreSame(b, ui.Model.Selected);
            Assert.IsTrue(ui.IsCastleVisible);
            Assert.AreEqual("성에 들어가기", Find(ui, "EnterButton").GetComponentInChildren<Text>().text);
        }

        [UnityTest]
        public IEnumerator Ui_SortieButton_OnlyInOwnedCastle()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(a);
            ui.Model.StartHere();
            Assert.IsTrue(Find(ui, "SortieButton").gameObject.activeSelf, "우리 성에서는 [출진]이 보인다");

            ui.Model.MoveTo(b);
            Assert.IsFalse(Find(ui, "SortieButton").gameObject.activeSelf, "적 성에서는 숨김");
        }

        [UnityTest]
        public IEnumerator Ui_Sortie_OpensTargetPanel_AndPickingTargetLoadsBattle()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var ui = MakeUi(catalog);
            string loaded = null;
            ui.SceneLoader = name => loaded = name;
            yield return null;

            ui.Model.Select(a);
            ui.Model.StartHere();
            Assert.IsFalse(ui.IsTargetPanelVisible);

            Find(ui, "SortieButton").onClick.Invoke();
            Assert.IsTrue(ui.IsTargetPanelVisible, "[출진]은 공격 대상 선택창을 연다");
            Assert.IsNull(loaded, "대상을 고르기 전에는 전투로 가지 않는다");
            Assert.IsNotNull(Find(ui, "Target_B"), "이웃한 적 성 B 가 목록에 있다");

            Find(ui, "Target_B").onClick.Invoke();
            Assert.AreEqual(GameManager.BattleSceneName, loaded);
            Assert.AreSame(b, GameSession.SortieCastle);
            Assert.AreSame(a, GameSession.SortieOrigin);
        }

        [UnityTest]
        public IEnumerator Ui_TargetPanel_ListsOnlyEnemyNeighbors_AndCancelCloses()
        {
            var catalog = MakeCatalog(out _, out var b, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(b);
            ui.Model.StartHere(); // B 소유: 이웃은 A, C
            Territory.Conquer(SaveSystem.Current, "A"); // A 는 이미 우리 성

            Find(ui, "SortieButton").onClick.Invoke();
            Assert.IsNull(Find(ui, "Target_A"), "이미 우리 성은 대상이 아니다");
            Assert.IsNotNull(Find(ui, "Target_C"));

            Find(ui, "TargetCancelButton").onClick.Invoke();
            Assert.IsFalse(ui.IsTargetPanelVisible);
            Assert.IsTrue(ui.IsCastleVisible, "취소하면 성 화면으로 돌아온다");
        }

        [UnityTest]
        public IEnumerator Ui_TargetPanel_ShowsMessage_WhenNoTargets()
        {
            var catalog = MakeCatalog(out _, out _, out _, out var d);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(d);
            ui.Model.StartHere(); // D 는 이웃이 없다
            Find(ui, "SortieButton").onClick.Invoke();

            Assert.IsTrue(ui.IsTargetPanelVisible);
            StringAssert.Contains("공격할 수 없습니다", ui.TargetPanelTitle);
        }

        [UnityTest]
        public IEnumerator Ui_ChangingState_ClosesTargetPanel()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(a);
            ui.Model.StartHere();
            Find(ui, "SortieButton").onClick.Invoke();
            Assert.IsTrue(ui.IsTargetPanelVisible);

            ui.Model.Leave();
            Assert.IsFalse(ui.IsTargetPanelVisible, "성 화면을 나가면 선택창도 닫힌다");
        }

        [UnityTest]
        public IEnumerator Ui_ChangeHomeButton_HiddenAfterConquest()
        {
            var catalog = MakeCatalog(out var a, out _, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(a);
            ui.Model.StartHere();
            ui.Model.Leave();
            Assert.IsTrue(Find(ui, "ChangeHomeButton").gameObject.activeSelf);

            Territory.Conquer(SaveSystem.Current, "B");
            ui.Model.Select(a); // 화면을 다시 그리게
            Assert.IsFalse(Find(ui, "ChangeHomeButton").gameObject.activeSelf, "다른 성을 차지했으면 시작 성을 바꿀 수 없다");
        }

        [UnityTest]
        public IEnumerator Ui_ChangeHomeButton_ClearsHome_AndAllowsPickingAgain()
        {
            var catalog = MakeCatalog(out var a, out var b, out _, out _);
            var ui = MakeUi(catalog);
            yield return null;

            ui.Model.Select(a);
            ui.Model.StartHere();
            ui.Model.Leave();
            Assert.IsTrue(Find(ui, "ChangeHomeButton").gameObject.activeSelf);

            Find(ui, "ChangeHomeButton").onClick.Invoke();
            Assert.IsFalse(ui.Model.HasHome);
            Assert.IsFalse(Find(ui, "ChangeHomeButton").gameObject.activeSelf);

            ui.Model.Select(b);
            Find(ui, "EnterButton").onClick.Invoke();
            Assert.AreSame(b, ui.Model.Home, "다른 성을 새 시작 성으로 고를 수 있다");
        }

        // ───────────────────────── 타이틀 / 씬 ─────────────────────────

        [Test]
        public void Title_OpenStrategy_LoadsStrategyScene()
        {
            var go = new GameObject("TitleTest");
            go.SetActive(false); // Awake 가 돌지 않게 (필수 참조 없이 동작만 확인)
            toDestroy.Add(go);
            var title = go.AddComponent<TitleController>();
            string loaded = null;
            title.SceneLoader = name => loaded = name;

            title.OpenStrategy();
            Assert.AreEqual(GameManager.StrategySceneName, loaded);
        }

        [Test]
        public void StrategyScene_ExistsAndIsInBuildSettings_AfterTitleAndGame()
        {
            const string path = "Assets/Scenes/StrategyScene.unity";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), "StrategyScene 이 없습니다. 메뉴 Samkuk > Step 12-2 를 실행하세요.");

            var paths = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) paths.Add(s.path);
            CollectionAssert.Contains(paths, path);
            Assert.AreEqual("Assets/Scenes/TitleScene.unity", paths[0], "타이틀이 0번");
            Assert.AreEqual("Assets/Scenes/BattleScene.unity", paths[1], "전투가 1번");
        }
    }
}
