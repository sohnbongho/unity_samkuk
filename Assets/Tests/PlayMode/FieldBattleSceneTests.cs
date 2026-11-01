using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Field;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>
    /// 야전 화면(Step 15-3, `FieldBattleController`/`FieldBattleUI`): 그림 참조 없이도 준비 화면 → 야전(타일 108, 부대 그림 14) → 결과창이 돌고,
    /// 클릭 집기·선택·명령·목표·정지 규칙이 화면에서 모델로 이어지는지. 실제 씬은 로드하지 않는다(SceneLoader 를 가짜로).
    /// </summary>
    public class FieldBattleSceneTests
    {
        readonly List<GameObject> gos = new List<GameObject>();
        string loaded;

        [TearDown]
        public void TearDown()
        {
            foreach (var g in gos) if (g != null) Object.Destroy(g);
            gos.Clear();
            FieldSession.LastSetup = null;
            FieldSession.StartImmediately = false;
            var cam = Camera.main;
            if (cam != null) Object.Destroy(cam.gameObject);
        }

        FieldBattleController Make()
        {
            var go = new GameObject("FieldBattleTest");
            gos.Add(go);
            var c = go.AddComponent<FieldBattleController>();
            c.SceneLoader = name => loaded = name;
            return c;
        }

        static FieldSetup Quiet() => new FieldSetup { enemyAi = false };

        [Test]
        public void Awake_BuildsUi_AndStartsInPrep()
        {
            var c = Make();
            Assert.IsNotNull(c.Ui);
            Assert.IsNotNull(c.Cam, "카메라가 없으면 만든다");
            Assert.AreEqual(FieldBattleController.CameraY, c.Cam.transform.position.y, 1e-4f);
            c.ShowPrep();
            Assert.AreEqual(FieldBattleController.Phase.Prep, c.CurrentPhase);
            Assert.IsTrue(c.Ui.PrepVisible);
            Assert.IsFalse(c.Ui.BandVisible);
        }

        [Test]
        public void StartBattle_CreatesTilesAndViews_PausedWithBand()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Mountain);
            Assert.AreEqual(FieldBattleController.Phase.Battle, c.CurrentPhase);
            Assert.AreEqual(FieldRules.Cols * FieldRules.Rows, c.TileCount);
            Assert.AreEqual(14, c.Views.Count);
            Assert.IsTrue(c.Model.Paused);
            Assert.IsTrue(c.Ui.BandVisible);
            Assert.IsFalse(c.Ui.PrepVisible);
            Assert.AreEqual(CastleTerrain.Mountain, FieldSession.LastTerrain, "다시 열 때를 위해 기억");

            // 그림이 없어도 모든 부대에 스프라이트가 있고 자리가 칸 가운데
            foreach (var v in c.Views)
            {
                Assert.IsNotNull(v.Renderer.sprite, v.name);
                IsoGrid.CellToWorld(v.Unit.Col, v.Unit.Row, out float x, out float y);
                Assert.AreEqual(x, v.transform.position.x, 1e-4f);
                Assert.AreEqual(y, v.transform.position.y, 1e-4f);
                Assert.AreEqual(GameLayers.Sorting.World, v.Renderer.sortingLayerName);
            }
        }

        [Test]
        public void PickUnitAt_FindsUnitByCell_AndNearby()
        {
            var c = Make();
            // 장수 부대만 두어 그 위 칸이 비어 있게 한다 (칸에 부대가 있으면 그쪽이 먼저 집힌다)
            c.StartBattle(new FieldSetup { enemyAi = false, playerCounts = new[] { 0, 0, 0 }, enemyCounts = new[] { 0, 0, 0 } }, CastleTerrain.Plain);
            var gen = c.Model.PlayerGeneral;
            IsoGrid.CellToWorld(gen.Col, gen.Row, out float x, out float y);
            Assert.AreSame(gen, c.PickUnitAt(new Vector2(x, y)));
            Assert.AreSame(gen, c.PickUnitAt(new Vector2(x, y + 0.6f)), "그림 위쪽(빈 칸)을 찍어도 가까운 부대가 집힌다");

            IsoGrid.CellToWorld(5, 4, out float ex, out float ey);   // 가운데 빈 칸
            Assert.IsNull(c.PickUnitAt(new Vector2(ex, ey)));
        }

        [Test]
        public void Click_SelectsPlayer_EnemyClickSetsTarget_OnlyWhilePaused()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Plain);
            var mine = c.Model.PlayerGeneral;
            var foe = c.Model.EnemyGeneral;
            IsoGrid.CellToWorld(mine.Col, mine.Row, out float mx, out float my);
            IsoGrid.CellToWorld(foe.Col, foe.Row, out float fx, out float fy);

            c.HandleClick(new Vector2(fx, fy));
            Assert.IsNull(c.Selected, "선택 없이 적을 찍으면 아무 일도 없다");
            c.HandleClick(new Vector2(mx, my));
            Assert.AreSame(mine, c.Selected);
            c.HandleClick(new Vector2(fx, fy));
            Assert.AreEqual(UnitOrder.Advance, mine.Order);
            Assert.AreEqual(foe.Id, mine.TargetId);

            Assert.IsTrue(c.IssueOrder(UnitOrder.Hold));
            Assert.AreEqual(UnitOrder.Hold, mine.Order);
            Assert.AreEqual(-1, mine.TargetId);

            c.TogglePause();
            Assert.IsFalse(c.Model.Paused);
            Assert.IsFalse(c.IssueOrder(UnitOrder.Advance), "진행 중엔 명령 못 함");
            Assert.AreEqual(UnitOrder.Hold, mine.Order);
            Assert.IsFalse(c.SetTarget(foe));
            StringAssert.Contains("정지", c.Ui.Hint);
        }

        [Test]
        public void SelectPlayerIndex_UsesNumberOrder()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Plain);
            Assert.IsTrue(c.SelectPlayerIndex(0));
            Assert.AreSame(c.Model.PlayerGeneral, c.Selected, "0번은 장수 부대(맨 앞)");
            Assert.IsTrue(c.SelectPlayerIndex(6));
            Assert.AreEqual(FieldSide.Player, c.Selected.Side);
            Assert.IsFalse(c.SelectPlayerIndex(7), "내 부대는 7개");
        }

        [Test]
        public void Step_MovesViews_AndUpdatesLabels()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Plain);
            FieldUnit cav = null;   // 내 편 병사 부대 하나를 전진시킨다
            foreach (var u in c.Model.Units) if (u.Side == FieldSide.Player && !u.IsGeneral) { cav = u; break; }
            c.Select(cav);
            c.IssueOrder(UnitOrder.Advance);
            var view = c.ViewOf(cav);
            float before = view.transform.position.x;
            c.TogglePause();
            for (int i = 0; i < 40; i++) c.Step(0.05f);
            Assert.Greater(view.transform.position.x, before, "전진하면 그림이 오른쪽 위로 간다");
            Assert.AreEqual(IsoFacing.UpRight, view.Facing);
            Assert.IsTrue(view.Renderer.sprite != null);
        }

        [Test]
        public void Finish_ShowsResult_AfterDelay_AndButtonsNavigate()
        {
            var c = Make();
            var setup = Quiet();
            c.StartBattle(setup, CastleTerrain.Plain);
            c.StartBattle(setup, CastleTerrain.Plain);   // 같은 조건으로 다시 시작해도 뷰 수가 유지된다 (지난 그림을 지움)
            Assert.AreEqual(14, c.Views.Count);

            // 진행해서 끝까지: 내 부대 전부 전진(장수 부대 포함), 적은 대기만 하므로 어느 쪽 장수 부대든 무너져 끝난다
            foreach (var u in c.Model.Units) if (u.Side == FieldSide.Player) c.Model.SetOrder(u.Id, UnitOrder.Advance);
            c.TogglePause();
            int guard = 0;
            while (!c.Model.Finished && guard++ < 20000)
            {
                if (c.Model.Paused) c.Model.Resume();
                c.Step(0.1f);
            }
            Assert.IsTrue(c.Model.Finished);
            Assert.AreEqual(FieldBattleController.Phase.Battle, c.CurrentPhase, "1초 전엔 아직 결과창 아님");
            for (int i = 0; i < 12; i++) c.Step(0.1f);
            Assert.AreEqual(FieldBattleController.Phase.Result, c.CurrentPhase);
            Assert.IsTrue(c.Ui.ResultVisible);

            c.Restart();
            Assert.AreEqual(FieldBattleController.Phase.Battle, c.CurrentPhase);
            Assert.AreEqual(14, c.Views.Count);
            c.BackToPrep();
            Assert.AreEqual(FieldBattleController.Phase.Prep, c.CurrentPhase);
            c.ToTitle();
            Assert.AreEqual(GameManager.TitleSceneName, loaded);
        }

        [Test]
        public void Menu_PausesModel_AndDoesNotResumeOnClose()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Plain);
            c.TogglePause();
            Assert.IsFalse(c.Model.Paused);
            c.ToggleMenu();
            Assert.IsTrue(c.MenuOpen);
            Assert.IsTrue(c.Model.Paused);
            c.ToggleMenu();
            Assert.IsFalse(c.MenuOpen);
            Assert.IsTrue(c.Model.Paused, "메뉴를 닫아도 정지 상태 그대로");
        }

        [Test]
        public void Fx_HitsMakeParticlesArrowsAndFlash_WhileRunning()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Plain);
            foreach (var u in c.Model.Units) if (u.Side == FieldSide.Player) c.Model.SetOrder(u.Id, UnitOrder.Advance);
            c.TogglePause();
            int arrowsSeen = 0;
            bool flashSeen = false;
            for (int i = 0; i < 200 && c.Fx.HitCount == 0; i++) c.Step(0.05f);   // 닿을 때까지
            Assert.Greater(c.Fx.HitCount, 0, "교전이 일어난다");
            for (int i = 0; i < 40; i++)
            {
                c.Step(0.05f);
                if (c.Fx.ActiveArrows > 0) arrowsSeen++;
                foreach (var v in c.Views) if (v.IsFlashing) flashSeen = true;
            }
            Assert.Greater(c.Fx.Burst.CreatedCount, 0, "불꽃 입자");
            Assert.IsTrue(flashSeen, "맞은 부대가 번쩍인다");
            Assert.Greater(arrowsSeen, 0, "궁병이 화살을 날린다");
        }

        [Test]
        public void RangeMarks_ShowForSelected_OnlyWhilePaused()
        {
            var c = Make();
            c.StartBattle(Quiet(), CastleTerrain.Plain);
            FieldUnit archer = null;
            foreach (var u in c.Model.Units) if (u.Side == FieldSide.Player && u.Class == UnitClass.Archer) { archer = u; break; }
            c.Select(archer);
            c.Step(0f);
            Assert.Greater(c.RangeMarkCount, 4, "궁병은 3칸 사거리");
            Assert.IsTrue(c.Ui.PauseBannerVisible);
            c.Select(c.Model.PlayerGeneral);
            c.Step(0f);
            Assert.AreEqual(4, c.RangeMarkCount, "백병전은 인접 4칸 (격자 안)");
            c.TogglePause();
            c.Step(0.05f);
            Assert.AreEqual(0, c.RangeMarkCount, "진행 중엔 숨긴다");
            Assert.IsFalse(c.Ui.PauseBannerVisible);
        }

        [Test]
        public void PrepSetup_RandomEnemyFormation_AndHeroName()
        {
            var c = Make();
            c.ShowPrep();
            var s = c.Ui.BuildSetup();
            Assert.AreEqual(300, s.soldiers);
            Assert.AreEqual(2, s.playerCounts[0]);
            Assert.IsFalse(string.IsNullOrEmpty(s.playerGeneralName));
        }
    }
}
