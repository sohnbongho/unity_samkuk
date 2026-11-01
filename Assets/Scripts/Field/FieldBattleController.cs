using System;
using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 씬의 진행: 준비 화면 → 야전 → 결과창. 모델(<see cref="FieldBattleModel"/>)을 돌리고 바닥 타일·부대 그림(<see cref="FieldUnitView"/>)·
    /// 화면(<see cref="FieldBattleUI"/>)을 맞추며 입력(클릭 선택, 적 클릭 = 목표, Space 정지/재개, 숫자 키 선택, ESC 메뉴)을 받는다.
    /// 씬에는 카메라·조명·이벤트 시스템·이 컴포넌트(그림 참조)만 있고 나머지는 실행 중에 코드로 만든다(맵 편집기와 같은 방식).
    /// 그림 참조가 비어 있으면(테스트, 셋업 전) 코드로 만든 색 덩어리로 대신한다.
    /// 정지는 모델의 깃발이고 ESC 메뉴도 모델을 정지시킬 뿐 timeScale 은 건드리지 않는다.
    /// </summary>
    public class FieldBattleController : MonoBehaviour
    {
        /// <summary>카메라 y: 전장(세로 7.875)이 아래 명령 띠(3.375) 위에 들어오고 위에 0.75 유닛 여유가 남는 자리.</summary>
        public const float CameraY = -0.9375f;
        /// <summary>승패가 난 뒤 결과창까지의 시간.</summary>
        public const float FinishDelay = 1f;
        /// <summary>클릭이 칸을 빗나갔을 때 그림으로 집는 반지름 (유닛). 그림이 마름모보다 위로 길다.</summary>
        public const float PickRadius = 0.9f;

        public enum Phase { Prep, Battle, Result }

        [SerializeField, Tooltip("준비 화면의 장수 목록 (장수 부대 이름). 없어도 동작")] HeroCatalog heroCatalog;
        [SerializeField, Tooltip("야전 수치(병과 표·상성·AI). 없으면 코드 기본값 FieldRules.Default()")] FieldRulesData rulesData;
        [SerializeField, Tooltip("바닥 타일. CastleTerrain 순서 (Plain, Steppe, Mountain, River, Jungle, Loess)")] Sprite[] tileSprites = new Sprite[6];
        [SerializeField, Tooltip("부대 시트 8장. 순서 = (보병, 궁병, 기병, 장수) x (내 편, 적)")] Texture2D[] unitSheets = new Texture2D[8];

        public Phase CurrentPhase { get; private set; } = Phase.Prep;
        public FieldBattleModel Model { get; private set; }
        public FieldUnit Selected { get; private set; }
        public FieldBattleUI Ui { get; private set; }
        public CastleTerrain Terrain { get; private set; }
        public Camera Cam => cam;
        public IReadOnlyList<FieldUnitView> Views => views;
        public int TileCount => tiles.Count;
        public FieldFx Fx => fx;
        /// <summary>수치 에셋 (테스트가 바꿔 넣는다). 다음 StartBattle 부터 쓴다.</summary>
        public FieldRulesData RulesData { get => rulesData; set => rulesData = value; }
        /// <summary>지금 켜진 사거리 표시 칸 수 (테스트용).</summary>
        public int RangeMarkCount { get; private set; }
        public bool MenuOpen => Ui != null && Ui.MenuOpen;

        /// <summary>씬 이동 (테스트에서 가짜로 바꾼다).</summary>
        public Action<string> SceneLoader { get; set; } = name => SceneManager.LoadScene(name);

        Camera cam;
        Transform fieldRoot;
        readonly List<FieldUnitView> views = new List<FieldUnitView>(20);
        readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>(FieldRules.Cols * FieldRules.Rows);
        readonly Dictionary<FieldUnit, FieldUnitView> viewOf = new Dictionary<FieldUnit, FieldUnitView>();
        static Sprite ringSprite;      // 코드로 만든 스프라이트는 씬을 다시 열어도 하나만 (새지 않게)
        static Sprite fallbackTile;
        static Sprite rangeSprite;
        readonly List<SpriteRenderer> rangeMarks = new List<SpriteRenderer>(28);
        FieldFx fx;
        FieldUnit rangeFor;
        bool rangeShown;
        float finishClock;

        /// <summary>시트 배열 번호: (보병 0, 궁병 1, 기병 2, 장수 3) x 2 + (내 편 0, 적 1).</summary>
        public static int SheetIndex(UnitClass cls, bool general, FieldSide side) => (general ? 3 : (int)cls) * 2 + (int)side;

        // ───────────────────────── 시작 ─────────────────────────

        void Awake()
        {
            SetupCamera();
            EnsureEventSystem();
            fieldRoot = new GameObject("Field").transform;
            fieldRoot.SetParent(transform, false);
            if (ringSprite == null) ringSprite = FieldSpriteSet.Diamond(Color.white, false);
            if (rangeSprite == null) rangeSprite = FieldSpriteSet.Diamond(new Color(1f, 0.9f, 0.4f, 0.28f), true);
            fx = new GameObject("FieldFx").AddComponent<FieldFx>();
            fx.transform.SetParent(transform, false);
            fx.ViewOf = ViewOf;
            Ui = FieldBattleUI.Create(this, heroCatalog);
        }

        void Start()
        {
            if (FieldSession.StartImmediately && FieldSession.LastSetup != null)
            {
                FieldSession.StartImmediately = false;
                StartBattle(FieldSession.LastSetup, FieldSession.LastTerrain);
            }
            else ShowPrep();
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, CameraY, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.08f);
            // Pixel Perfect Camera(셋업이 붙임)가 있으면 그쪽이 크기를 정한다
            if (cam.GetComponent<BattlePixelCamera>() == null) cam.orthographicSize = PixelArt.OrthographicSize;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ───────────────────────── 흐름 ─────────────────────────

        public void ShowPrep()
        {
            ClearField();
            Model = null;
            Selected = null;
            CurrentPhase = Phase.Prep;
            Ui.ShowPrep();
        }

        /// <summary>준비 화면 값으로 야전을 시작한다. 모든 부대는 대기, 정지 상태에서 시작해 명령을 준 뒤 Space 로 연다.</summary>
        public void StartBattle(FieldSetup setup, CastleTerrain terrain)
        {
            ClearField();
            FieldSession.LastSetup = setup;
            FieldSession.LastTerrain = terrain;
            Terrain = terrain;
            Selected = null;
            finishClock = 0f;

            Model = new FieldBattleModel(setup, rulesData != null ? rulesData.rules : null);
            Model.AutoPaused += OnAutoPaused;
            Model.FinishedEvent += OnFinished;
            fx.Bind(Model);

            BuildTiles(terrain);
            foreach (var u in Model.Units) views.Add(CreateView(u));
            CurrentPhase = Phase.Battle;
            Ui.ShowBand(views);
            Ui.SetHint("부대를 고르고(클릭, 숫자 키) 명령을 준 뒤 Space 로 시작합니다. 적을 찍으면 그 부대로 전진합니다.");
            SyncViews(0f);
        }

        void ClearField()
        {
            if (Model != null)
            {
                Model.AutoPaused -= OnAutoPaused;
                Model.FinishedEvent -= OnFinished;
            }
            foreach (var v in views) if (v != null) Destroy(v.gameObject);
            views.Clear();
            viewOf.Clear();
            foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
            tiles.Clear();
            fx.Unbind();
            HideRange();
        }

        void BuildTiles(CastleTerrain terrain)
        {
            int idx = (int)terrain;
            var sprite = tileSprites != null && idx >= 0 && idx < tileSprites.Length ? tileSprites[idx] : null;
            if (sprite == null)
            {
                if (fallbackTile == null) fallbackTile = FieldSpriteSet.Diamond(new Color(0.28f, 0.38f, 0.24f), true);
                sprite = fallbackTile;
            }
            for (int c = 0; c < FieldRules.Cols; c++)
                for (int r = 0; r < FieldRules.Rows; r++)
                {
                    var go = new GameObject($"Tile_{c}_{r}");
                    go.transform.SetParent(fieldRoot, false);
                    IsoGrid.CellToWorld(c, r, out float x, out float y);
                    go.transform.localPosition = new Vector3(x, y, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingLayerName = GameLayers.Sorting.Background;
                    sr.sortingOrder = 0;
                    tiles.Add(sr);
                }
        }

        FieldUnitView CreateView(FieldUnit u)
        {
            var go = new GameObject(u.Name);
            go.transform.SetParent(fieldRoot, false);
            var view = go.AddComponent<FieldUnitView>();
            int idx = SheetIndex(u.Class, u.IsGeneral, u.Side);
            var sheet = unitSheets != null && idx < unitSheets.Length ? unitSheets[idx] : null;
            var fallback = FieldSpriteSet.Placeholder(u.Side == FieldSide.Player ? new Color(0.3f, 0.45f, 0.9f) : new Color(0.85f, 0.3f, 0.3f));
            view.Init(u, FieldSpriteSet.Get(sheet), fallback, ringSprite);
            viewOf[u] = view;
            return view;
        }

        public FieldUnitView ViewOf(FieldUnit u) => u != null && viewOf.TryGetValue(u, out var v) ? v : null;

        // ───────────────────────── 진행 ─────────────────────────

        void Update()
        {
            if (CurrentPhase != Phase.Battle) return;
            HandleInput();
            Step(Time.deltaTime);
        }

        /// <summary>한 틱: 모델을 돌리고 그림·라벨·띠를 맞춘다. 승패 뒤 1초가 지나면 결과창.</summary>
        public void Step(float dt)
        {
            if (CurrentPhase != Phase.Battle || Model == null) return;
            Model.Tick(dt);
            if (!Model.Paused) fx.Tick(dt);   // 정지면 화살도 멈춘다
            SyncViews(dt);
            if (Model.Finished)
            {
                finishClock += dt;
                if (finishClock >= FinishDelay) ShowResult();
            }
        }

        void SyncViews(float dt)
        {
            var target = Selected != null ? Model.Find(Selected.TargetId) : null;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                v.Selected = v.Unit == Selected;
                v.Targeted = target != null && v.Unit == target;
                v.Sync(dt);
            }
            UpdateRange();
            Ui.Refresh(Model, Selected, cam);
        }

        // ───────────────────────── 사거리 표시 ─────────────────────────

        /// <summary>정지 중에 고른 부대의 공격 범위(맨해튼 거리 ≤ 사거리)를 옅은 마름모로 보여 준다. 궁병은 3칸, 나머지는 인접 칸.</summary>
        void UpdateRange()
        {
            bool show = Selected != null && Selected.Alive && Model.Paused && !Model.Finished;
            if (!show) { if (rangeShown) HideRange(); return; }
            if (rangeShown && rangeFor == Selected && !Selected.Moving) return;

            HideRange();
            rangeFor = Selected;
            rangeShown = true;
            int range = Model.Rules.Stats(Selected.Class).range;
            int k = 0;
            for (int dc = -range; dc <= range; dc++)
                for (int dr = -range; dr <= range; dr++)
                {
                    int d = Mathf.Abs(dc) + Mathf.Abs(dr);
                    if (d == 0 || d > range) continue;
                    int c = Selected.Col + dc, r = Selected.Row + dr;
                    if (!FieldBattleModel.InBounds(c, r)) continue;
                    var sr = RangeMark(k++);
                    IsoGrid.CellToWorld(c, r, out float x, out float y);
                    sr.transform.localPosition = new Vector3(x, y, 0f);
                    sr.enabled = true;
                }
            RangeMarkCount = k;
        }

        SpriteRenderer RangeMark(int i)
        {
            while (rangeMarks.Count <= i)
            {
                var go = new GameObject("Range");
                go.transform.SetParent(fieldRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = rangeSprite;
                sr.sortingLayerName = GameLayers.Sorting.Background;
                sr.sortingOrder = 3;   // 타일(0) 위, 선택 테(5) 아래
                sr.enabled = false;
                rangeMarks.Add(sr);
            }
            return rangeMarks[i];
        }

        void HideRange()
        {
            foreach (var m in rangeMarks) if (m != null) m.enabled = false;
            rangeShown = false;
            rangeFor = null;
            RangeMarkCount = 0;
        }

        void OnAutoPaused(FieldUnit u) => Ui.SetHint($"{u.Name}이(가) 무너졌습니다 — 정지. 남은 부대의 명령을 정하고 Space 로 재개하세요.");

        void OnFinished(BattleOutcome outcome) => Ui.SetHint(outcome == BattleOutcome.Victory ? "적 장수 부대가 무너졌습니다 — 승리!" : "내 장수 부대가 무너졌습니다 — 패배…");

        void ShowResult()
        {
            CurrentPhase = Phase.Result;
            Ui.ShowResult(Model);
        }

        // ───────────────────────── 입력 ─────────────────────────

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) ToggleMenu();
                if (!MenuOpen && !Model.Finished)
                {
                    if (kb.spaceKey.wasPressedThisFrame) TogglePause();
                    if (kb.digit1Key.wasPressedThisFrame) SelectPlayerIndex(0);
                    else if (kb.digit2Key.wasPressedThisFrame) SelectPlayerIndex(1);
                    else if (kb.digit3Key.wasPressedThisFrame) SelectPlayerIndex(2);
                    else if (kb.digit4Key.wasPressedThisFrame) SelectPlayerIndex(3);
                    else if (kb.digit5Key.wasPressedThisFrame) SelectPlayerIndex(4);
                    else if (kb.digit6Key.wasPressedThisFrame) SelectPlayerIndex(5);
                    else if (kb.digit7Key.wasPressedThisFrame) SelectPlayerIndex(6);
                    else if (kb.digit8Key.wasPressedThisFrame) SelectPlayerIndex(7);
                    else if (kb.digit9Key.wasPressedThisFrame) SelectPlayerIndex(8);
                    else if (kb.digit0Key.wasPressedThisFrame) SelectPlayerIndex(9);
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !MenuOpen && !PointerOverUi())
            {
                Vector2 s = mouse.position.ReadValue();
                Vector3 w = cam.ScreenToWorldPoint(new Vector3(s.x, s.y, 10f));
                HandleClick(w);
            }
        }

        static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>전장 클릭: 내 부대면 선택, 적 부대면 선택한 부대의 전진 목표(정지 중에만), 빈 곳이면 그대로.</summary>
        public void HandleClick(Vector2 world)
        {
            var unit = PickUnitAt(world);
            if (unit == null) return;
            if (unit.Side == FieldSide.Player) Select(unit);
            else if (Selected != null) SetTarget(unit);
        }

        /// <summary>월드 좌표에 있는 살아 있는 부대: 그 칸의 부대, 없으면 그림이 가까운 부대(반지름 안).</summary>
        public FieldUnit PickUnitAt(Vector2 world)
        {
            if (Model == null) return null;
            if (IsoGrid.WorldToCell(world.x, world.y, out int col, out int row))
            {
                var at = Model.UnitAt(col, row);
                if (at != null && at.Alive) return at;
            }
            FieldUnit best = null;
            float bestD = PickRadius * PickRadius;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (!v.Unit.Alive) continue;
                float d = ((Vector2)v.transform.position - world).sqrMagnitude;
                if (d < bestD) { bestD = d; best = v.Unit; }
            }
            return best;
        }

        public bool Select(FieldUnit unit)
        {
            if (unit == null || !unit.Alive || unit.Side != FieldSide.Player) return false;
            Selected = unit;
            return true;
        }

        /// <summary>살아 있는 내 부대 중 n 번째(0부터)를 고른다 (숫자 키).</summary>
        public bool SelectPlayerIndex(int n)
        {
            int k = 0;
            foreach (var u in Model.Units)
            {
                if (!u.Alive || u.Side != FieldSide.Player) continue;
                if (k++ == n) return Select(u);
            }
            return false;
        }

        /// <summary>선택한 부대에 명령. 전진은 목표 없이(가장 가까운 적). 정지 중에만 통한다.</summary>
        public bool IssueOrder(UnitOrder order)
        {
            if (Selected == null) return false;
            bool ok = Model.SetOrder(Selected.Id, order, -1);
            if (!ok) Ui.SetHint(Model.Finished ? "야전이 끝났습니다." : "명령은 정지 중에만 줄 수 있습니다 (Space).");
            return ok;
        }

        /// <summary>선택한 부대를 그 적 부대로 전진시킨다 (목표 지정).</summary>
        public bool SetTarget(FieldUnit enemy)
        {
            if (Selected == null || enemy == null || enemy.Side == FieldSide.Player) return false;
            bool ok = Model.SetOrder(Selected.Id, UnitOrder.Advance, enemy.Id);
            if (!ok) Ui.SetHint("목표는 정지 중에만 정할 수 있습니다 (Space).");
            return ok;
        }

        public void TogglePause()
        {
            if (Model == null || Model.Finished) return;
            Model.TogglePause();
        }

        public void ToggleMenu()
        {
            if (Ui.MenuOpen) Ui.CloseMenu();
            else
            {
                Model?.Pause();
                Ui.OpenMenu();
            }
        }

        // ───────────────────────── 결과창 / 메뉴 버튼 ─────────────────────────

        public void Restart()
        {
            if (FieldSession.LastSetup == null) { ShowPrep(); return; }
            StartBattle(FieldSession.LastSetup, FieldSession.LastTerrain);
        }

        public void BackToPrep() => ShowPrep();

        public void ToTitle() => SceneLoader?.Invoke(GameManager.TitleSceneName);
    }
}
