using System;
using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Data;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 화면 전체를 실행 중에 코드로 만든다 (내정 화면·맵 편집기와 같은 방식): 준비 화면(편성·배치·지형 고르기), 아래 명령 띠(선택 부대 정보,
    /// [전진][대기][후퇴], [정지/재개], 시간, 안내), 부대 위 병사 수 라벨, 결과창, ESC 메뉴. 규칙은 전혀 없고 <see cref="FieldBattleController"/> 를 부른다.
    /// 기준 해상도 1920x1080, 명령 띠 높이 324 = 가상 해상도 640x360 의 108 픽셀 (ADR 0003).
    /// </summary>
    public class FieldBattleUI : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public const float BandHeight = 324f;
        static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.09f, 0.82f);
        static readonly Color PlayerLabel = new Color(0.6f, 0.78f, 1f);
        static readonly Color EnemyLabel = new Color(1f, 0.55f, 0.5f);
        static readonly int[] SoldierSteps = { 100, 200, 300, 500, 800 };

        FieldBattleController ctrl;
        HeroCatalog heroes;

        RectTransform root, prep, band, result, menu, labelsRoot;
        Text infoText, hintText, timeText, pauseLabel, resultTitle, resultBody, pauseBanner;
        readonly List<Action> prepRefreshers = new List<Action>();

        // 준비 화면에서 고치는 값
        FieldSetup editing = new FieldSetup();
        int heroIndex;
        CastleTerrain terrain = CastleTerrain.Plain;
        int enemyFormationChoice;   // 0 = 무작위, 1~4 = 배치
        readonly List<Text> labels = new List<Text>(20);
        readonly List<FieldUnitView> labelViews = new List<FieldUnitView>(20);
        readonly List<int> lastSoldiers = new List<int>(20);
        readonly List<UnitOrder> lastOrders = new List<UnitOrder>(20);
        int lastSecond = -1;
        bool lastPaused;
        FieldUnit lastSelected;
        int lastSelectedSoldiers = -1;
        int lastSelectedTarget = -2;
        UnitOrder lastSelectedOrder;

        public bool MenuOpen => menu != null && menu.gameObject.activeSelf;
        public bool PrepVisible => prep != null && prep.gameObject.activeSelf;
        public bool BandVisible => band != null && band.gameObject.activeSelf;
        public bool ResultVisible => result != null && result.gameObject.activeSelf;
        public string Hint => hintText != null ? hintText.text : "";
        public FieldSetup Editing => editing;
        public CastleTerrain Terrain => terrain;

        public static FieldBattleUI Create(FieldBattleController controller, HeroCatalog heroCatalog)
        {
            var go = new GameObject("FieldBattleCanvas", typeof(RectTransform));
            go.transform.SetParent(controller.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<GraphicRaycaster>();
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var ui = go.AddComponent<FieldBattleUI>();
            ui.ctrl = controller;
            ui.heroes = heroCatalog;
            ui.root = (RectTransform)go.transform;
            ui.Build();
            return ui;
        }

        void Build()
        {
            // 마지막 준비 값으로 시작 (다시 열어도 같은 조건)
            if (FieldSession.LastSetup != null) editing = Clone(FieldSession.LastSetup);
            terrain = FieldSession.LastTerrain;
            heroIndex = FieldSession.LastHeroIndex;
            enemyFormationChoice = FieldSession.EnemyFormationRandom ? 0 : (int)editing.enemyFormation + 1;

            labelsRoot = NewRect("Labels", root);
            Stretch(labelsRoot);
            BuildBand();
            BuildPrep();
            BuildResult();
            BuildMenu();
            HideAll();
        }

        static FieldSetup Clone(FieldSetup s) => new FieldSetup
        {
            playerCounts = (int[])s.playerCounts.Clone(), enemyCounts = (int[])s.enemyCounts.Clone(), soldiers = s.soldiers,
            playerFormation = s.playerFormation, enemyFormation = s.enemyFormation, playerMirror = s.playerMirror, enemyMirror = s.enemyMirror,
            enemyAi = s.enemyAi, playerGeneralName = s.playerGeneralName, enemyGeneralName = s.enemyGeneralName,
        };

        void HideAll()
        {
            prep.gameObject.SetActive(false);
            band.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            menu.gameObject.SetActive(false);
            labelsRoot.gameObject.SetActive(false);
            if (pauseBanner != null) pauseBanner.gameObject.SetActive(false);
        }

        public bool PauseBannerVisible => pauseBanner != null && pauseBanner.gameObject.activeSelf;

        // ───────────────────────── 준비 화면 ─────────────────────────

        void BuildPrep()
        {
            prep = NewPanel("Prep", root, new Color(0.07f, 0.05f, 0.09f, 0.97f));
            Center(prep, new Vector2(1180f, 960f));
            var title = NewText("Title", prep, 48, TextAnchor.MiddleCenter, "야전 테스트");
            UiSkin.StyleTitle(title);
            TopLeft(title.rectTransform, new Vector2(0f, -20f), new Vector2(1180f, 70f));

            var list = NewRect("Rows", prep);
            TopLeft(list, new Vector2(90f, -110f), new Vector2(1000f, 740f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            Row(list, "장수 (장수 부대)", () => HeroName(heroIndex), d => { heroIndex = Wrap(heroIndex + d, HeroCount()); });
            Row(list, "내 보병 부대", () => editing.playerCounts[0].ToString(), d => editing.playerCounts[0] = Clamp(editing.playerCounts[0] + d, 0, FieldSetup.MaxPerClass));
            Row(list, "내 궁병 부대", () => editing.playerCounts[1].ToString(), d => editing.playerCounts[1] = Clamp(editing.playerCounts[1] + d, 0, FieldSetup.MaxPerClass));
            Row(list, "내 기병 부대", () => editing.playerCounts[2].ToString(), d => editing.playerCounts[2] = Clamp(editing.playerCounts[2] + d, 0, FieldSetup.MaxPerClass));
            Row(list, "적 보병 부대", () => editing.enemyCounts[0].ToString(), d => editing.enemyCounts[0] = Clamp(editing.enemyCounts[0] + d, 0, FieldSetup.MaxPerClass));
            Row(list, "적 궁병 부대", () => editing.enemyCounts[1].ToString(), d => editing.enemyCounts[1] = Clamp(editing.enemyCounts[1] + d, 0, FieldSetup.MaxPerClass));
            Row(list, "적 기병 부대", () => editing.enemyCounts[2].ToString(), d => editing.enemyCounts[2] = Clamp(editing.enemyCounts[2] + d, 0, FieldSetup.MaxPerClass));
            Row(list, "부대 병사 수", () => editing.soldiers.ToString(), d => editing.soldiers = SoldierSteps[Wrap(Array.IndexOf(SoldierSteps, editing.soldiers) + d, SoldierSteps.Length)]);
            Row(list, "내 배치", () => FieldNames.Formation(editing.playerFormation) + (editing.playerMirror ? " (반전)" : ""), d =>
            {
                // 배치 4종 x 반전 = 8 가지를 한 줄로 돈다
                int v = (int)editing.playerFormation * 2 + (editing.playerMirror ? 1 : 0);
                v = Wrap(v + d, 8);
                editing.playerFormation = (FormationKind)(v / 2);
                editing.playerMirror = v % 2 == 1;
            });
            Row(list, "적 배치", () => enemyFormationChoice == 0 ? "무작위" : FieldNames.Formation((FormationKind)(enemyFormationChoice - 1)), d => enemyFormationChoice = Wrap(enemyFormationChoice + d, 5));
            Row(list, "적 AI", () => editing.enemyAi ? "켬" : "끔 (모두 대기)", d => editing.enemyAi = !editing.enemyAi);
            Row(list, "지형 (바닥)", () => CastleData.TerrainLabel(terrain), d => terrain = (CastleTerrain)Wrap((int)terrain + d, 6));

            var start = NewButton("StartButton", prep, "시작", new Vector2(300f, 80f), 34);
            BottomCenter(start.GetComponent<RectTransform>(), new Vector2(-180f, 40f));
            Bind(start, OnStartPressed);
            var back = NewButton("TitleButton", prep, "타이틀로", new Vector2(300f, 80f), 34);
            BottomCenter(back.GetComponent<RectTransform>(), new Vector2(180f, 40f));
            Bind(back, () => ctrl.ToTitle());
        }

        int HeroCount() => heroes != null && heroes.heroes.Count > 0 ? heroes.heroes.Count : 1;
        string HeroName(int i)
        {
            if (heroes == null || heroes.heroes.Count == 0) return "주인공";
            var h = heroes.heroes[Wrap(i, heroes.heroes.Count)];
            return h != null ? h.displayName : "주인공";
        }

        /// <summary>"이름 ◀ 값 ▶" 한 줄. 값은 바뀔 때마다 다시 읽는다.</summary>
        void Row(RectTransform parent, string label, Func<string> value, Action<int> delta)
        {
            var row = NewRect(label, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            var name = NewText("Name", row, 30, TextAnchor.MiddleLeft, label);
            Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f));
            var left = NewButton("Left", row, "◀", new Vector2(70f, 50f), 30);
            var lrt = left.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.47f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            var val = NewText("Value", row, 30, TextAnchor.MiddleCenter, value());
            Anchor(val.rectTransform, new Vector2(0.55f, 0f), new Vector2(0.86f, 1f));
            var right = NewButton("Right", row, "▶", new Vector2(70f, 50f), 30);
            var rrt = right.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.88f, 0.5f);
            rrt.pivot = new Vector2(0f, 0.5f);
            rrt.anchoredPosition = Vector2.zero;

            Action refresh = () => val.text = value();
            prepRefreshers.Add(refresh);
            Bind(left, () => { delta(-1); refresh(); });
            Bind(right, () => { delta(+1); refresh(); });
        }

        void OnStartPressed()
        {
            var setup = BuildSetup();
            FieldSession.LastHeroIndex = heroIndex;
            FieldSession.EnemyFormationRandom = enemyFormationChoice == 0;
            ctrl.StartBattle(setup, terrain);
        }

        /// <summary>준비 화면 값으로 시작 조건을 만든다 (적 배치 무작위는 여기서 뽑는다).</summary>
        public FieldSetup BuildSetup()
        {
            var s = Clone(editing);
            s.playerGeneralName = HeroName(heroIndex);
            s.enemyGeneralName = "적장";
            if (enemyFormationChoice == 0)
            {
                s.enemyFormation = (FormationKind)UnityEngine.Random.Range(0, 4);
                s.enemyMirror = UnityEngine.Random.value < 0.5f;
            }
            else s.enemyFormation = (FormationKind)(enemyFormationChoice - 1);
            return s;
        }

        public void ShowPrep()
        {
            HideAll();
            foreach (var r in prepRefreshers) r();
            prep.gameObject.SetActive(true);
        }

        // ───────────────────────── 명령 띠 ─────────────────────────

        void BuildBand()
        {
            band = NewPanel("Band", root, PanelColor);
            Anchor(band, new Vector2(0f, 0f), new Vector2(1f, 0f));
            band.pivot = new Vector2(0.5f, 0f);
            band.sizeDelta = new Vector2(0f, BandHeight);
            band.anchoredPosition = Vector2.zero;

            infoText = NewText("Info", band, 30, TextAnchor.UpperLeft, "");
            TopLeft(infoText.rectTransform, new Vector2(40f, -24f), new Vector2(600f, 200f));

            var orders = NewRect("Orders", band);
            TopLeft(orders, new Vector2(680f, -40f), new Vector2(600f, 90f));
            var h = orders.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 20f;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleCenter;
            Bind(NewButton("AdvanceButton", orders, "전진", new Vector2(180f, 80f), 34), () => ctrl.IssueOrder(UnitOrder.Advance));
            Bind(NewButton("HoldButton", orders, "대기", new Vector2(180f, 80f), 34), () => ctrl.IssueOrder(UnitOrder.Hold));
            Bind(NewButton("RetreatButton", orders, "후퇴", new Vector2(180f, 80f), 34), () => ctrl.IssueOrder(UnitOrder.Retreat));

            var pause = NewButton("PauseButton", band, "재개 (Space)", new Vector2(360f, 80f), 34);
            var prt = pause.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-40f, -40f);
            pauseLabel = pause.GetComponentInChildren<Text>();
            Bind(pause, () => ctrl.TogglePause());

            var menuBtn = NewButton("MenuButton", band, "메뉴 (ESC)", new Vector2(360f, 64f), 28);
            var mrt = menuBtn.GetComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
            mrt.pivot = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-40f, -136f);
            Bind(menuBtn, () => ctrl.ToggleMenu());

            timeText = NewText("Time", band, 30, TextAnchor.MiddleRight, "0:00");
            var trt = timeText.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 1f);
            trt.anchoredPosition = new Vector2(-40f, -214f);
            trt.sizeDelta = new Vector2(360f, 50f);

            // 화면 위 가운데의 정지 배너 (전장 위에 겹침)
            pauseBanner = NewText("PauseBanner", root, 34, TextAnchor.UpperCenter, "정지 중 — 명령을 주고 Space 로 재개");
            UiSkin.StyleTitle(pauseBanner);
            var brt = pauseBanner.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -16f);
            brt.sizeDelta = new Vector2(1200f, 50f);
            pauseBanner.gameObject.SetActive(false);

            hintText = NewText("Hint", band, 26, TextAnchor.LowerLeft, "");
            hintText.color = new Color(1f, 0.9f, 0.6f);
            Anchor(hintText.rectTransform, new Vector2(0f, 0f), new Vector2(0.78f, 0f));
            hintText.rectTransform.pivot = new Vector2(0f, 0f);
            hintText.rectTransform.anchoredPosition = new Vector2(40f, 20f);
            hintText.rectTransform.sizeDelta = new Vector2(-80f, 90f);
        }

        /// <summary>야전 시작: 띠와 부대 라벨을 보이고 라벨을 부대 그림에 묶는다.</summary>
        public void ShowBand(IReadOnlyList<FieldUnitView> views)
        {
            HideAll();
            band.gameObject.SetActive(true);
            labelsRoot.gameObject.SetActive(true);
            BindLabels(views);
            lastSecond = -1;
            lastSelected = null;
            lastSelectedSoldiers = -1;
            lastSelectedTarget = -2;
            lastPaused = false;   // 모델은 정지 상태로 시작하므로 첫 Refresh 에서 버튼 글자가 "재개" 로 맞춰진다
            infoText.text = "부대를 고르세요 (클릭 또는 숫자 키 1~0)";
        }

        void BindLabels(IReadOnlyList<FieldUnitView> views)
        {
            foreach (var l in labels) if (l != null) Destroy(l.gameObject);
            labels.Clear();
            labelViews.Clear();
            lastSoldiers.Clear();
            lastOrders.Clear();
            foreach (var v in views)
            {
                var t = NewText("Label", labelsRoot, 26, TextAnchor.MiddleCenter, "");
                t.color = v.Unit.Side == FieldSide.Player ? PlayerLabel : EnemyLabel;
                t.fontStyle = FontStyle.Bold;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(160f, 36f);
                labels.Add(t);
                labelViews.Add(v);
                lastSoldiers.Add(-1);
                lastOrders.Add(UnitOrder.Hold);
            }
        }

        public void SetHint(string text) { if (hintText != null) hintText.text = text; }

        /// <summary>매 틱: 라벨 위치/숫자, 선택 부대 정보, 정지 버튼 글자, 시간. 바뀐 것만 글자를 다시 만든다.</summary>
        public void Refresh(FieldBattleModel model, FieldUnit selected, Camera cam)
        {
            if (model == null || !band.gameObject.activeSelf) return;

            for (int i = 0; i < labels.Count; i++)
            {
                var v = labelViews[i];
                var t = labels[i];
                bool show = v.Unit.Alive;
                if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
                if (!show) continue;
                int n = Mathf.CeilToInt(v.Unit.Soldiers);
                if (n != lastSoldiers[i] || v.Unit.Order != lastOrders[i])
                {
                    lastSoldiers[i] = n;
                    lastOrders[i] = v.Unit.Order;
                    t.text = OrderMark(v.Unit.Order) + n;
                }
                if (cam != null)
                {
                    Vector2 screen = cam.WorldToScreenPoint(v.LabelWorldPosition);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(labelsRoot, screen, null, out Vector2 local);
                    t.rectTransform.anchoredPosition = local;
                }
            }

            int sec = (int)model.Elapsed;
            if (sec != lastSecond) { lastSecond = sec; timeText.text = $"{sec / 60}:{sec % 60:00}"; }
            if (model.Paused != lastPaused)
            {
                lastPaused = model.Paused;
                pauseLabel.text = model.Paused ? "재개 (Space)" : "정지 (Space)";
            }
            bool banner = model.Paused && !model.Finished && !MenuOpen;
            if (pauseBanner.gameObject.activeSelf != banner) pauseBanner.gameObject.SetActive(banner);

            if (selected != lastSelected || (selected != null && (Mathf.CeilToInt(selected.Soldiers) != lastSelectedSoldiers || selected.Order != lastSelectedOrder || selected.TargetId != lastSelectedTarget)))
            {
                lastSelected = selected;
                if (selected == null) infoText.text = "부대를 고르세요 (클릭 또는 숫자 키 1~0)";
                else
                {
                    lastSelectedSoldiers = Mathf.CeilToInt(selected.Soldiers);
                    lastSelectedOrder = selected.Order;
                    lastSelectedTarget = selected.TargetId;
                    var target = model.Find(selected.TargetId);
                    string order = FieldNames.Order(selected.Order) + (target != null && selected.Order == UnitOrder.Advance ? " → " + target.Name : "");
                    infoText.text = $"{selected.Name}  ({(selected.IsGeneral ? "장수 부대·기병" : FieldNames.Class(selected.Class))})\n병사 {lastSelectedSoldiers} / {selected.MaxSoldiers}\n명령: {order}" + (selected.Alive ? "" : "\n(무너짐)");
                }
            }
        }

        static string OrderMark(UnitOrder o)
        {
            switch (o)
            {
                case UnitOrder.Advance: return "▲";
                case UnitOrder.Retreat: return "▼";
                default: return "■";
            }
        }

        // ───────────────────────── 결과창 / 메뉴 ─────────────────────────

        void BuildResult()
        {
            result = NewPanel("Result", root, new Color(0.07f, 0.05f, 0.09f, 0.97f));
            Center(result, new Vector2(900f, 600f));
            resultTitle = NewText("Title", result, 56, TextAnchor.MiddleCenter, "");
            UiSkin.StyleTitle(resultTitle);
            TopLeft(resultTitle.rectTransform, new Vector2(0f, -30f), new Vector2(900f, 90f));
            resultBody = NewText("Body", result, 30, TextAnchor.UpperCenter, "");
            TopLeft(resultBody.rectTransform, new Vector2(60f, -150f), new Vector2(780f, 280f));

            var again = NewButton("AgainButton", result, "다시", new Vector2(240f, 76f), 32);
            BottomCenter(again.GetComponent<RectTransform>(), new Vector2(-280f, 40f));
            Bind(again, () => ctrl.Restart());
            var prepBtn = NewButton("PrepButton", result, "준비 화면", new Vector2(240f, 76f), 32);
            BottomCenter(prepBtn.GetComponent<RectTransform>(), new Vector2(0f, 40f));
            Bind(prepBtn, () => ctrl.BackToPrep());
            var title = NewButton("TitleButton", result, "타이틀", new Vector2(240f, 76f), 32);
            BottomCenter(title.GetComponent<RectTransform>(), new Vector2(280f, 40f));
            Bind(title, () => ctrl.ToTitle());
        }

        public void ShowResult(FieldBattleModel model)
        {
            menu.gameObject.SetActive(false);
            result.gameObject.SetActive(true);
            bool win = model.Outcome == BattleOutcome.Victory;
            resultTitle.text = win ? "승리" : "패배";
            int mine = 0, foe = 0, mineUnits = 0, foeUnits = 0;
            foreach (var u in model.Units)
            {
                if (!u.Alive) continue;
                if (u.Side == FieldSide.Player) { mine += Mathf.CeilToInt(u.Soldiers); mineUnits++; }
                else { foe += Mathf.CeilToInt(u.Soldiers); foeUnits++; }
            }
            int sec = (int)model.Elapsed;
            resultBody.text = (win ? "적 장수 부대가 무너졌습니다." : "내 장수 부대가 무너지거나 전장을 떠났습니다.") +
                              $"\n\n남은 내 부대 {mineUnits} (병사 {mine})\n남은 적 부대 {foeUnits} (병사 {foe})\n걸린 시간 {sec / 60}:{sec % 60:00}";
        }

        void BuildMenu()
        {
            menu = NewPanel("Menu", root, new Color(0.07f, 0.05f, 0.09f, 0.97f));
            Center(menu, new Vector2(560f, 420f));
            var title = NewText("Title", menu, 40, TextAnchor.MiddleCenter, "메뉴");
            UiSkin.StyleTitle(title);
            TopLeft(title.rectTransform, new Vector2(0f, -24f), new Vector2(560f, 70f));
            var cont = NewButton("ContinueButton", menu, "계속 (정지 상태)", new Vector2(400f, 70f), 30);
            Center(cont.GetComponent<RectTransform>(), new Vector2(400f, 70f), new Vector2(0f, 40f));
            Bind(cont, () => ctrl.ToggleMenu());
            var prepBtn = NewButton("PrepButton", menu, "준비 화면", new Vector2(400f, 70f), 30);
            Center(prepBtn.GetComponent<RectTransform>(), new Vector2(400f, 70f), new Vector2(0f, -50f));
            Bind(prepBtn, () => { CloseMenu(); ctrl.BackToPrep(); });
            var titleBtn = NewButton("TitleButton", menu, "타이틀", new Vector2(400f, 70f), 30);
            Center(titleBtn.GetComponent<RectTransform>(), new Vector2(400f, 70f), new Vector2(0f, -140f));
            Bind(titleBtn, () => ctrl.ToTitle());
        }

        public void OpenMenu() => menu.gameObject.SetActive(true);
        public void CloseMenu() => menu.gameObject.SetActive(false);

        // ───────────────────────── 헬퍼 ─────────────────────────

        static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        static void Bind(Button button, Action action)
        {
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                action();
            });
        }

        static RectTransform NewPanel(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            UiSkin.StylePanel(img);
            img.color = color;
            return rt;
        }

        static Button NewButton(string name, Transform parent, string label, Vector2 size, int fontSize)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            var img = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            UiSkin.StyleButton(btn);
            var text = NewText("Label", rt, fontSize, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform);
            return btn;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text NewText(string name, Transform parent, int size, TextAnchor anchor, string value)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont.Get();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = value;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt) => Anchor(rt, Vector2.zero, Vector2.one);

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Center(RectTransform rt, Vector2 size) => Center(rt, size, Vector2.zero);

        static void Center(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        static void BottomCenter(RectTransform rt, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
        }
    }
}
