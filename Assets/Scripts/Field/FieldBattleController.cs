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
    /// 키보드 조작(15-6): 정지 중에 선택이 없으면 첫 내 부대가 저절로 선택되고, W/S(↑/↓)로 부대를 바꾸고 Enter 로 세부 명령 메뉴(<see cref="KeyMode.Detail"/>:
    /// 전진·목표 정해 전진·대기·후퇴·같은 병과·전체)를 열어 ↑/↓·Enter 로 실행한다. 목표 지정은 적 목록을 ↑/↓ 로 고른다(<see cref="KeyMode.Target"/>).
    /// 같은 병과/전체 명령은 장수 부대를 뺀다(장수 부대의 후퇴 = 패배라 따로만 명령한다).
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
        /// <summary>키보드 조작 상태: 부대 목록 → (Enter) 세부 명령 → (목표 정해 전진) 적 목록.</summary>
        public enum KeyMode { List, Detail, Target }
        /// <summary>세부 명령 메뉴 항목 (순서 = 화면 순서).</summary>
        public enum DetailItem { Advance, AdvanceTo, Hold, Retreat, ClassAdvance, ClassHold, ClassRetreat, AllAdvance, AllHold, AllRetreat }
        public static readonly string[] DetailLabels =
        {
            "전진", "목표 정해 전진", "대기", "후퇴", "같은 병과 전진", "같은 병과 대기", "같은 병과 후퇴", "전체 전진", "전체 대기", "전체 후퇴"
        };

        [SerializeField, Tooltip("준비 화면의 장수 목록 (장수 부대 이름). 없어도 동작")] HeroCatalog heroCatalog;
        [SerializeField, Tooltip("야전 수치(병과 표·상성·AI). 없으면 코드 기본값 FieldRules.Default()")] FieldRulesData rulesData;
        [SerializeField, Tooltip("바닥 타일. CastleTerrain 순서 (Plain, Steppe, Mountain, River, Jungle, Loess)")] Sprite[] tileSprites = new Sprite[6];
        [SerializeField, Tooltip("부대 시트 8장. 순서 = (보병, 궁병, 기병, 장수) x (내 편, 적)")] Texture2D[] unitSheets = new Texture2D[8];

        public Phase CurrentPhase { get; private set; } = Phase.Prep;
        public FieldBattleModel Model { get; private set; }
        public FieldUnit Selected { get; private set; }
        public FieldBattleUI Ui { get; private set; }
        public CastleTerrain Terrain { get; private set; }
        /// <summary>이번 야전의 장수 (없으면 개성 없음).</summary>
        public HeroData Hero { get; private set; }
        public Camera Cam => cam;
        public IReadOnlyList<FieldUnitView> Views => views;
        public int TileCount => tiles.Count;
        public FieldFx Fx => fx;
        /// <summary>수치 에셋 (테스트가 바꿔 넣는다). 다음 StartBattle 부터 쓴다.</summary>
        public FieldRulesData RulesData { get => rulesData; set => rulesData = value; }
        /// <summary>지금 켜진 사거리 표시 칸 수 (테스트용).</summary>
        public int RangeMarkCount { get; private set; }
        public bool MenuOpen => Ui != null && Ui.MenuOpen;
        public KeyMode Mode { get; private set; } = KeyMode.List;
        /// <summary>세부 명령 메뉴의 커서 (<see cref="DetailItem"/>).</summary>
        public int DetailIndex { get; private set; }
        /// <summary>목표 고르기 커서: 살아 있는 적 부대 목록(모델 순서)에서의 번호.</summary>
        public int TargetCursor { get; private set; }
        /// <summary>목표 고르기 중 커서가 가리키는 적 부대 (아니면 null). 화면이 빨간 테로 미리 보여 준다.</summary>
        public FieldUnit TargetCursorUnit => Mode == KeyMode.Target ? EnemyAt(TargetCursor) : null;

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
                StartBattle(FieldSession.LastSetup, FieldSession.LastTerrain, FieldSession.LastHero);
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

        /// <summary>준비 화면 값으로 야전을 시작한다. 모든 부대는 대기, 정지 상태에서 시작해 명령을 준 뒤 Space 로 연다. hero 는 장수 개성(무기 연출), 없어도 된다.</summary>
        public void StartBattle(FieldSetup setup, CastleTerrain terrain, HeroData hero = null)
        {
            ClearField();
            if (hero != null) FieldHeroLink.Apply(setup, hero);   // 장수 개성: 어디서 불려도 능력치가 함께 간다 (출진 흐름 대비)
            FieldSession.LastSetup = setup;
            FieldSession.LastTerrain = terrain;
            FieldSession.LastHero = hero;
            Terrain = terrain;
            Hero = hero;
            Selected = null;
            Mode = KeyMode.List;
            finishClock = 0f;

            Model = new FieldBattleModel(setup, rulesData != null ? rulesData.rules : null);
            Model.AutoPaused += OnAutoPaused;
            Model.FinishedEvent += OnFinished;
            fx.Bind(Model, hero != null ? hero.startingWeapon : null);

            BuildTiles(terrain);
            foreach (var u in Model.Units) views.Add(CreateView(u));
            CurrentPhase = Phase.Battle;
            Ui.ShowBand(views);
            Ui.SetHint("W/S(↑/↓)로 부대를 고르고 Enter 로 명령을 준 뒤 Space 로 시작합니다. 마우스: 내 부대 클릭 = 선택, 적 클릭 = 목표.");
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
            EnsureSelection();
            // 목표 고르는 중엔 커서가 가리키는 적을 빨간 테로 미리 보여 준다
            var target = Mode == KeyMode.Target ? TargetCursorUnit : (Selected != null ? Model.Find(Selected.TargetId) : null);
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
                // ESC: 세부/목표 고르기 중이면 한 단계 뒤로, 목록이면 메뉴
                if (kb.escapeKey.wasPressedThisFrame) { if (MenuOpen || Mode == KeyMode.List) ToggleMenu(); else Back(); }
                if (!MenuOpen && !Model.Finished)
                {
                    if (kb.spaceKey.wasPressedThisFrame) TogglePause();
                    bool up = kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
                    bool down = kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
                    if (up) MoveCursor(-1);
                    else if (down) MoveCursor(+1);
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
                    if (kb.backspaceKey.wasPressedThisFrame) Back();
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
            if (Selected != unit) Mode = KeyMode.List;   // 다른 부대를 고르면 세부 메뉴는 닫힌다
            Selected = unit;
            return true;
        }

        // ───────────────────────── 키보드 조작 ─────────────────────────

        /// <summary>정지 중에 살아 있는 선택이 없으면 첫 내 부대(장수 부대)를 고른다. 방금 고른 부대는 빼앗지 않는다.</summary>
        void EnsureSelection()
        {
            if (!Model.Paused || Model.Finished) return;
            if (Selected != null && Selected.Alive) return;
            Selected = PlayerAt(0);
            Mode = KeyMode.List;
        }

        /// <summary>살아 있는 내 부대 n 번째 (모델 순서 = 숫자 키 순서, 장수 부대가 0).</summary>
        public FieldUnit PlayerAt(int n) => AliveAt(FieldSide.Player, n);
        /// <summary>살아 있는 적 부대 n 번째 (모델 순서).</summary>
        public FieldUnit EnemyAt(int n) => AliveAt(FieldSide.Enemy, n);

        FieldUnit AliveAt(FieldSide side, int n)
        {
            if (Model == null || n < 0) return null;
            int k = 0;
            var units = Model.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Alive || u.Side != side) continue;
                if (k++ == n) return u;
            }
            return null;
        }

        /// <summary>살아 있는 같은 편 부대 중 몇 번째인가 (없으면 -1).</summary>
        public int AliveIndexOf(FieldUnit unit)
        {
            if (unit == null || Model == null) return -1;
            int k = 0;
            var units = Model.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Alive || u.Side != unit.Side) continue;
                if (u == unit) return k;
                k++;
            }
            return -1;
        }

        /// <summary>선택을 살아 있는 내 부대 목록에서 delta 만큼 옮긴다 (끝에서 반대쪽으로 돈다). 진행 중에도 된다.</summary>
        public bool MoveSelection(int delta)
        {
            int n = Model != null ? Model.CountAlive(FieldSide.Player) : 0;
            if (n == 0) return false;
            int idx = AliveIndexOf(Selected);
            idx = idx < 0 ? 0 : Wrap(idx + delta, n);
            return Select(PlayerAt(idx));
        }

        /// <summary>Enter: 선택한 부대의 세부 명령 메뉴를 연다. 정지 중에만.</summary>
        public bool OpenDetail()
        {
            if (Selected == null) return false;
            if (!Model.CanCommand)
            {
                Ui.SetHint(Model.Finished ? "야전이 끝났습니다." : "명령은 정지 중에만 줄 수 있습니다 (Space).");
                return false;
            }
            Mode = KeyMode.Detail;
            DetailIndex = 0;
            return true;
        }

        /// <summary>W/S·↑/↓: 목록이면 부대, 세부 메뉴면 항목, 목표 고르기면 적 부대를 옮긴다.</summary>
        public void MoveCursor(int delta)
        {
            switch (Mode)
            {
                case KeyMode.Detail: DetailIndex = Wrap(DetailIndex + delta, DetailLabels.Length); break;
                case KeyMode.Target:
                {
                    int n = Model.CountAlive(FieldSide.Enemy);
                    if (n > 0) TargetCursor = Wrap(TargetCursor + delta, n);
                    break;
                }
                default: MoveSelection(delta); break;
            }
        }

        /// <summary>Enter: 목록이면 세부 메뉴 열기, 세부 메뉴면 항목 실행, 목표 고르기면 그 적으로 전진.</summary>
        public bool Confirm()
        {
            switch (Mode)
            {
                case KeyMode.Detail: return ExecuteDetail((DetailItem)DetailIndex);
                case KeyMode.Target:
                {
                    var enemy = TargetCursorUnit;
                    Mode = KeyMode.List;
                    return SetTarget(enemy);
                }
                default: return OpenDetail();
            }
        }

        /// <summary>ESC/Backspace: 한 단계 뒤로 (목표 고르기 → 세부 메뉴 → 목록). 목록이면 아무 일도 없다.</summary>
        public bool Back()
        {
            switch (Mode)
            {
                case KeyMode.Target: Mode = KeyMode.Detail; return true;
                case KeyMode.Detail: Mode = KeyMode.List; return true;
                default: return false;
            }
        }

        bool ExecuteDetail(DetailItem item)
        {
            bool ok;
            switch (item)
            {
                case DetailItem.AdvanceTo:
                    if (Model.CountAlive(FieldSide.Enemy) == 0) { Mode = KeyMode.List; return false; }
                    Mode = KeyMode.Target;
                    // 이미 목표가 있으면 거기서 시작
                    int cur = Selected != null ? AliveIndexOf(Model.Find(Selected.TargetId)) : -1;
                    TargetCursor = cur < 0 ? 0 : cur;
                    return true;
                case DetailItem.Advance: ok = IssueOrder(UnitOrder.Advance); break;
                case DetailItem.Hold: ok = IssueOrder(UnitOrder.Hold); break;
                case DetailItem.Retreat: ok = IssueOrder(UnitOrder.Retreat); break;
                case DetailItem.ClassAdvance: ok = IssueGroupOrder(UnitOrder.Advance, sameClass: true); break;
                case DetailItem.ClassHold: ok = IssueGroupOrder(UnitOrder.Hold, sameClass: true); break;
                case DetailItem.ClassRetreat: ok = IssueGroupOrder(UnitOrder.Retreat, sameClass: true); break;
                case DetailItem.AllAdvance: ok = IssueGroupOrder(UnitOrder.Advance, sameClass: false); break;
                case DetailItem.AllHold: ok = IssueGroupOrder(UnitOrder.Hold, sameClass: false); break;
                default: ok = IssueGroupOrder(UnitOrder.Retreat, sameClass: false); break;
            }
            Mode = KeyMode.List;   // 실행했으면 다음 부대를 고를 수 있게 목록으로
            return ok;
        }

        /// <summary>
        /// 여러 부대에 한 번에 명령: sameClass 면 선택한 부대와 같은 병과의 내 병사 부대 전부, 아니면 내 병사 부대 전부.
        /// 장수 부대는 뺀다(후퇴하면 패배라 따로만 명령). 정지 중에만.
        /// </summary>
        public bool IssueGroupOrder(UnitOrder order, bool sameClass)
        {
            if (!Model.CanCommand)
            {
                Ui.SetHint(Model.Finished ? "야전이 끝났습니다." : "명령은 정지 중에만 줄 수 있습니다 (Space).");
                return false;
            }
            if (sameClass && Selected == null) return false;
            var cls = sameClass ? Selected.Class : default;
            int count = 0;
            var units = Model.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Alive || u.Side != FieldSide.Player || u.IsGeneral) continue;
                if (sameClass && u.Class != cls) continue;
                if (Model.SetOrder(u.Id, order, -1)) count++;
            }
            Ui.SetHint(count > 0
                ? $"{(sameClass ? FieldNames.Class(cls) + " 부대" : "병사 부대")} {count}개에 {FieldNames.Order(order)} 명령 (장수 부대는 따로)."
                : "명령을 줄 병사 부대가 없습니다.");
            return count > 0;
        }

        static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;

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
            if (!Model.Paused) Mode = KeyMode.List;   // 재개하면 세부 메뉴는 닫는다
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
            StartBattle(FieldSession.LastSetup, FieldSession.LastTerrain, FieldSession.LastHero);
        }

        public void BackToPrep() => ShowPrep();

        public void ToTitle() => SceneLoader?.Invoke(GameManager.TitleSceneName);
    }
}
