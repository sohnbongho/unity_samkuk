using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    /// <summary>내정에서 출진한 판: 지형 색, 결과 화면의 [내정으로], 타이틀 시작과의 구분.</summary>
    public class SortieTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp() => GameSession.SortieCastle = null;

        [TearDown]
        public void TearDown()
        {
            GameSession.SortieCastle = null;
            Audio.AudioManager.DestroyInstance();
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
        }

        CastleData MakeCastle(CastleTerrain terrain, string name = "성")
        {
            var c = ScriptableObject.CreateInstance<CastleData>();
            c.id = name; c.displayName = name; c.terrain = terrain;
            toDestroy.Add(c);
            return c;
        }

        // ───────────────────────── 전투 지형 ─────────────────────────

        [Test]
        public void BattleTerrain_EachTerrainHasItsOwnOverlayColor()
        {
            var seen = new HashSet<Color>();
            foreach (CastleTerrain t in System.Enum.GetValues(typeof(CastleTerrain)))
            {
                var c = BattleTerrain.Overlay(t);
                Assert.Greater(c.a, 0f, $"{t}: 덮개가 보여야 한다");
                Assert.IsTrue(seen.Add(c), $"{t}: 다른 지형과 색이 같다");
            }
        }

        [Test]
        public void BattleTerrain_ForCurrentSortie_NullWithoutCastle_AndFollowsTerrain()
        {
            Assert.IsNull(BattleTerrain.ForCurrentSortie(), "성 없이 시작한 판은 기본 배경");

            GameSession.SortieCastle = MakeCastle(CastleTerrain.Loess);
            Assert.AreEqual(BattleTerrain.Overlay(CastleTerrain.Loess), BattleTerrain.ForCurrentSortie());
        }

        GameObject MakeBackground(out InfiniteBackground bg)
        {
            var go = new GameObject("TestBg", typeof(SpriteRenderer));
            toDestroy.Add(go);
            bg = go.AddComponent<InfiniteBackground>(); // Awake 에서 출진한 성의 지형을 반영한다
            return go;
        }

        [UnityTest]
        public IEnumerator InfiniteBackground_AddsOverlay_OnlyWhenSortieCastleExists()
        {
            var plain = MakeBackground(out _);
            Assert.IsNull(plain.transform.Find("TerrainOverlay"), "성 없이 시작하면 덮개가 없다");

            GameSession.SortieCastle = MakeCastle(CastleTerrain.Mountain);
            var go = MakeBackground(out _);
            var overlay = go.transform.Find("TerrainOverlay");
            Assert.IsNotNull(overlay);
            var sr = overlay.GetComponent<SpriteRenderer>();
            Assert.AreEqual(BattleTerrain.Overlay(CastleTerrain.Mountain), sr.color);
            Assert.AreEqual(go.GetComponent<SpriteRenderer>().sortingOrder + 1, sr.sortingOrder, "배경 바로 위");
            yield return null;
        }

        [UnityTest]
        public IEnumerator InfiniteBackground_ApplyOverlay_ReplacesOrRemovesExisting()
        {
            GameSession.SortieCastle = MakeCastle(CastleTerrain.River);
            var go = MakeBackground(out var bg);

            bg.ApplyTerrainOverlay(BattleTerrain.Overlay(CastleTerrain.Jungle));
            Assert.AreEqual(1, go.transform.childCount, "덮개는 하나만 유지");
            Assert.AreEqual(BattleTerrain.Overlay(CastleTerrain.Jungle), go.transform.Find("TerrainOverlay").GetComponent<SpriteRenderer>().color);

            bg.ApplyTerrainOverlay(null);
            yield return null; // Destroy 는 한 프레임 뒤
            Assert.IsNull(go.transform.Find("TerrainOverlay"));
        }

        // ───────────────────────── 결과 화면 ─────────────────────────

        [Test]
        public void ResultUI_Format_ShowsCastleOnlyWhenSortied()
        {
            string plain = ResultUI.Format(new RunResult { seconds = 5f });
            StringAssert.DoesNotContain("출진 성", plain);

            string sortied = ResultUI.Format(new RunResult { seconds = 5f, castleName = "낙양" });
            StringAssert.Contains("출진 성", sortied);
            StringAssert.Contains("낙양", sortied);
        }

        [Test]
        public void ResultUI_StrategyButton_ShownOnlyWhenReturnIsSet_AndInvokesIt()
        {
            var go = new GameObject("TestResultUI");
            toDestroy.Add(go);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform);
            var title = new GameObject("T", typeof(RectTransform)).AddComponent<Text>();
            var details = new GameObject("D", typeof(RectTransform)).AddComponent<Text>();
            var retry = new GameObject("R", typeof(RectTransform)).AddComponent<Button>();
            var toTitle = new GameObject("TT", typeof(RectTransform)).AddComponent<Button>();
            var toStrategy = new GameObject("TS", typeof(RectTransform)).AddComponent<Button>();
            foreach (var c in new Component[] { title, details, retry, toTitle, toStrategy }) c.transform.SetParent(panel.transform);

            var ui = go.AddComponent<ResultUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("detailsLabel").objectReferenceValue = details;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("titleButton").objectReferenceValue = toTitle;
            so.FindProperty("strategyButton").objectReferenceValue = toStrategy;
            so.ApplyModifiedPropertiesWithoutUndo();

            ui.Show(new RunResult(), () => { }, () => { });
            Assert.IsFalse(toStrategy.gameObject.activeSelf, "성 없이 시작한 판은 [내정으로]가 없다");

            bool back = false;
            ui.SetStrategyReturn(() => back = true);
            ui.Show(new RunResult { castleName = "낙양" }, () => { }, () => { });
            Assert.IsTrue(toStrategy.gameObject.activeSelf);
            toStrategy.onClick.Invoke();
            Assert.IsTrue(back);

            ui.SetStrategyReturn(null);
            ui.Show(new RunResult(), () => { }, () => { });
            Assert.IsFalse(toStrategy.gameObject.activeSelf, "다시 성 없이 보여 주면 숨긴다");
        }

        // ───────────────────────── 타이틀 시작 ─────────────────────────

        [Test]
        public void Title_Start_ClearsSortieCastle()
        {
            GameSession.SortieCastle = MakeCastle(CastleTerrain.Plain);
            var go = new GameObject("TitleStartTest");
            go.SetActive(false);
            toDestroy.Add(go);
            var title = go.AddComponent<TitleController>();
            string loaded = null;
            title.SceneLoader = name => loaded = name;

            title.StartGame();
            Assert.IsNull(GameSession.SortieCastle, "타이틀의 [시작]은 성 없이 시작하는 판");
            Assert.AreEqual(GameManager.BattleSceneName, loaded);
        }
    }
}
