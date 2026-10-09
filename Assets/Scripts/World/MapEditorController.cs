using System;
using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samkuk.World
{
    /// <summary>맵 편집기의 마지막 위치 (전투 테스트에서 돌아왔을 때 같은 성을 다시 연다).</summary>
    public static class MapEditorSession
    {
        public static string LastCastleId;
    }

    /// <summary>
    /// 맵 편집기(맵툴) 화면. 성마다 전투 맵을 직접 고친다: 소품/연못/깃발/바닥 얼룩/강을 놓고, 옮기고, 지우고, 회전/크기를 바꾸고,
    /// 바닥 지형을 바꾼다. 고친 칸(12x12)은 자동 생성 대신 고친 내용을 쓰고, 나머지 칸은 평소처럼 자동 생성된다.
    /// 저장하면 전투가 같은 맵을 쓴다(<see cref="MapStore"/>). 화면은 전부 코드로 만든다(씬에는 이 컴포넌트만 있으면 된다).
    /// 편집 규칙은 <see cref="MapEditModel"/>.
    ///   좌클릭: 배치/선택(도구에 따라)   우클릭 드래그 또는 WASD: 화면 이동   휠: 확대/축소
    ///   1/2/3 도구   Q/E 회전   [ ] 크기   F 반전   Ctrl+D 복제   Del 삭제   Ctrl+Z/Y 실행취소/다시   Ctrl+S 저장   G 격자   ESC 나가기
    /// </summary>
    public class MapEditorController : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public enum Tool { Place, Select, Erase }
        public enum EraseTarget { All, Props, Patches, Water }

        const float MinZoom = 3f, MaxZoom = 28f;
        const float LeftPanelWidth = 250f, RightPanelWidth = 400f, TopBarHeight = 74f, BottomBarHeight = 44f;
        static readonly float[] SnapSteps = { 0f, 0.25f, 0.5f, 1f };
        static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.09f, 0.88f);
        static readonly Color Gold = new Color(1f, 0.86f, 0.4f);

        [SerializeField, Tooltip("성 목록 (CastleCatalog). 없으면 '자유 전투' 맵만 고칠 수 있다")] CastleCatalog castleCatalog;

        /// <summary>씬 로드 방법 (테스트에서 대체 가능).</summary>
        public Action<string> SceneLoader { get; set; } = name => SceneManager.LoadScene(name);

        // ───────────────────────── 상태 ─────────────────────────

        public MapEditModel Model { get; private set; }
        public TerrainMap Map => Model != null ? Model.Map : null;
        public CastleData CurrentCastle { get; private set; }
        public Tool CurrentTool { get; private set; } = Tool.Place;
        public EraseTarget CurrentEraseTarget { get; private set; } = EraseTarget.All;
        public PaletteEntry SelectedEntry { get; private set; }
        public ItemRef Selected { get; private set; } = ItemRef.None;
        public CastleTerrain? TerrainOverride { get; private set; }
        public float BrushRadius { get; private set; } = 2f;
        public int SnapIndex { get; private set; }
        public bool ShowGrid { get; private set; } = true;
        public string Message { get; private set; } = "";
        public IReadOnlyList<PaletteEntry> Palette => palette;
        public Camera Cam => cam;

        TerrainThemeCatalog themes;
        Camera cam;
        InfiniteBackground background;
        TerrainPropSpawner spawner;
        readonly List<CastleData> castles = new List<CastleData>();
        int castleIndex;
        List<PaletteEntry> palette = new List<PaletteEntry>();
        List<string> groups = new List<string>();
        string currentGroup;
        readonly System.Random rng = new System.Random(12345);

        bool strokeActive, dragging, moveStarted;
        Vector2 dragOffset, lastStamp;
        bool hasLastStamp;
        float messageLeft;
        bool exitArmed;
        bool built;

        // 화면 위에 그리는 것들
        static Sprite whiteSprite, ringSprite, groundFallback;
        SpriteRenderer ghost, selectionRing, brushRing, startRing;
        readonly List<SpriteRenderer> gridLines = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> chunkMarks = new List<SpriteRenderer>();

        // UI
        RectTransform paletteContent, tabRow;
        readonly List<GameObject> paletteCells = new List<GameObject>();
        readonly List<Image> paletteCellBackgrounds = new List<Image>();
        readonly List<PaletteEntry> paletteCellEntries = new List<PaletteEntry>();
        readonly List<Button> tabButtons = new List<Button>();
        readonly List<string> tabNames = new List<string>();
        readonly Dictionary<Tool, Button> toolButtons = new Dictionary<Tool, Button>();
        Text castleLabel, statusLabel, selectionLabel, terrainLabel, snapLabel, gridLabel, brushLabel, eraseLabel, paletteHint, saveLabel;
        Button undoButton, redoButton;
        Color buttonBase = Color.white;

        // ───────────────────────── 시작 ─────────────────────────

        void Start() => EnsureBuilt();

        /// <summary>화면을 한 번만 만든다.</summary>
        public void EnsureBuilt()
        {
            if (built) return;
            built = true;

            Time.timeScale = 1f;
            GameSession.MapTest = false;
            themes = TerrainThemeCatalog.Load();
            SetupCamera();
            EnsureEventSystem();
            BuildBackground();
            BuildOverlays();
            palette = MapPalette.Build(themes);
            groups = MapPalette.Groups(palette);
            BuildCastleList();
            BuildUi();
            UiFont.Apply(gameObject);

            int start = 0;
            if (!string.IsNullOrEmpty(MapEditorSession.LastCastleId))
                for (int i = 0; i < castles.Count; i++)
                    if (castles[i].id == MapEditorSession.LastCastleId) { start = i; break; }
            LoadCastle(start);
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
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.09f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void BuildBackground()
        {
            // 꺼 둔 채로 만들어 Awake 의 자동 적용(전투용)이 돌지 않게 하고, 준비를 마친 뒤 켠다
            var go = new GameObject("Background");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, 0f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GroundFallback();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(160f, 100f);   // 타일(4유닛)의 배수. 최대 축소(세로 56유닛)에서도 화면을 덮는다
            sr.sortingLayerName = GameLayers.Sorting.Background;
            background = go.AddComponent<InfiniteBackground>();
            background.FollowTarget = cam.transform;
            background.LightingAllowed = false;   // 편집 화면은 밝고 일정하게. 조명은 전투 테스트에서 본다
            go.SetActive(true);
        }

        void BuildCastleList()
        {
            castles.Clear();
            castles.Add(TerrainMap.FreeBattleCastle);   // 맨 앞: 성 없이 시작한 판의 맵
            if (castleCatalog != null)
                foreach (var c in castleCatalog.castles)
                    if (c != null) castles.Add(c);
        }

        // ───────────────────────── 성/맵 불러오기, 저장 ─────────────────────────

        public int CastleCount => castles.Count;
        public int CastleIndex => castleIndex;

        /// <summary>성을 바꿔 그 성의 맵(고쳐 둔 것이 있으면 그것)을 연다. 저장하지 않은 변경이 있으면 먼저 저장한다.</summary>
        public void LoadCastle(int index)
        {
            if (castles.Count == 0) return;
            if (Model != null && Model.Unsaved) Save();

            castleIndex = (index % castles.Count + castles.Count) % castles.Count;
            CurrentCastle = castles[castleIndex];
            MapEditorSession.LastCastleId = CurrentCastle.id;

            var layout = MapStore.Load(CurrentCastle.id);
            TerrainOverride = TerrainMap.ParseTerrain(layout != null ? layout.terrain : null);
            var map = TerrainMap.Create(CurrentCastle, themes, TerrainOverride);
            if (map == null)
            {
                Flash("지형 그림이 없습니다. 메뉴 Samkuk > Step 12-6 - Terrain Themes 를 먼저 실행하세요.");
                return;
            }
            if (layout != null) map.ImportLayout(layout);
            ApplyMap(map);

            Deselect();
            cam.transform.position = new Vector3(0f, 0f, -10f);
            ShowGroup(MapPalette.TerrainLabel(map.Terrain));
            Flash(layout != null ? $"{CurrentCastle.displayName}: 저장해 둔 맵을 열었습니다" : $"{CurrentCastle.displayName}: 자동 생성 맵입니다 (고치면 그 칸부터 직접 고친 맵이 됩니다)");
            RefreshUi();
        }

        void ApplyMap(TerrainMap map)
        {
            if (Model == null) Model = new MapEditModel(map);
            else Model.SetMap(map);
            background.ApplyMap(map);
            spawner = background.Props;
            if (spawner != null) spawner.SetFollow(cam.transform);
        }

        /// <summary>지금 맵을 저장한다. 고친 내용이 없으면 저장 파일을 지워 자동 생성 맵으로 둔다.</summary>
        public bool Save()
        {
            if (Model == null || CurrentCastle == null) return false;
            var layout = Map.ExportLayout();
            if (layout.chunks.Count == 0 && string.IsNullOrEmpty(layout.terrain))
            {
                MapStore.Delete(CurrentCastle.id);
                Model.MarkSaved();
                Flash("고친 내용이 없어 자동 생성 맵 그대로 둡니다 (저장 파일 삭제)");
                return true;
            }
            if (!MapStore.Save(layout, out string path))
            {
                Flash("저장에 실패했습니다 (콘솔 확인)");
                return false;
            }
            Model.MarkSaved();
            Flash($"저장했습니다: {CurrentCastle.displayName} ({layout.chunks.Count}칸, 물건 {layout.ItemCount}개)");
            AudioManager.Play(SfxId.Click);
            return true;
        }

        /// <summary>저장하지 않은 변경을 버리고 저장된 상태(없으면 자동 생성)로 다시 연다.</summary>
        public void ReloadFromDisk()
        {
            if (Model != null) Model.MarkSaved();   // LoadCastle 이 자동 저장하지 않게
            LoadCastle(castleIndex);
        }

        /// <summary>이 성의 고친 내용을 모두 버리고 자동 생성 맵으로 (한 단계 실행 취소 가능).</summary>
        public void ResetAll()
        {
            if (Model == null) return;
            bool changed = Model.ResetAll();
            if (TerrainOverride.HasValue) { SetTerrain(null); changed = true; }
            Deselect();
            Flash(changed ? "전부 자동 생성 상태로 되돌렸습니다 (Ctrl+Z 로 취소)" : "되돌릴 내용이 없습니다");
        }

        /// <summary>고친 맵으로 전투를 해 본다 (저장 후 전투 씬. 전투 안에서 M 으로 돌아온다).</summary>
        public void TestBattle()
        {
            if (!Save()) return;
            GameSession.SortieCastle = CurrentCastle != null && CurrentCastle.id != MapStore.FreeBattleId ? CurrentCastle : null;
            GameSession.SortieOrigin = null;
            GameSession.MapTest = true;
            SceneLoader?.Invoke(GameManager.BattleSceneName);
        }

        public void ExitToTitle()
        {
            GameSession.MapTest = false;
            SceneLoader?.Invoke(GameManager.TitleSceneName);
        }

        // ───────────────────────── 바닥 지형 ─────────────────────────

        static readonly CastleTerrain[] TerrainCycle = { CastleTerrain.Plain, CastleTerrain.Steppe, CastleTerrain.Mountain, CastleTerrain.River, CastleTerrain.Jungle, CastleTerrain.Loess };

        /// <summary>바닥 지형을 순환한다: 성 기본 → 평야 → 초원 → 산악 → 강변 → 남방 → 황토 → 성 기본.</summary>
        public void CycleTerrain()
        {
            if (TerrainOverride == null) { SetTerrain(TerrainCycle[0]); return; }
            int i = Array.IndexOf(TerrainCycle, TerrainOverride.Value);
            SetTerrain(i + 1 < TerrainCycle.Length ? TerrainCycle[i + 1] : (CastleTerrain?)null);
        }

        /// <summary>바닥 지형(타일, 소품 종류, 강 모양)을 바꾼다. 직접 고친 칸의 내용은 그대로 두고 나머지 칸이 새 지형으로 다시 만들어진다.</summary>
        public void SetTerrain(CastleTerrain? terrain)
        {
            if (Model == null || CurrentCastle == null) return;
            var layout = Map.ExportLayout();
            var map = TerrainMap.Create(CurrentCastle, themes, terrain);
            if (map == null) { Flash("그 지형의 그림이 없습니다"); return; }
            map.ImportLayout(layout);
            TerrainOverride = terrain;
            ApplyMap(map);
            Model.MarkChanged();
            ShowGroup(MapPalette.TerrainLabel(map.Terrain));
            Deselect();
            Flash(terrain.HasValue ? $"바닥 지형: {MapPalette.TerrainLabel(terrain.Value)} (저장하면 전투에도 적용)" : "바닥 지형: 성의 원래 지형");
            RefreshUi();
        }

        // ───────────────────────── 도구 ─────────────────────────

        public void SetTool(Tool tool)
        {
            CurrentTool = tool;
            if (tool != Tool.Select) Deselect();
            hasLastStamp = false;
            RefreshUi();
        }

        public void SelectEntry(PaletteEntry entry)
        {
            SelectedEntry = entry;
            if (entry != null) SetTool(Tool.Place);
            RefreshUi();
        }

        public void CycleEraseTarget()
        {
            CurrentEraseTarget = (EraseTarget)(((int)CurrentEraseTarget + 1) % 4);
            RefreshUi();
        }

        public void ChangeBrush(float delta)
        {
            BrushRadius = Mathf.Clamp(BrushRadius + delta, 0.5f, 8f);
            RefreshUi();
        }

        public void CycleSnap()
        {
            SnapIndex = (SnapIndex + 1) % SnapSteps.Length;
            RefreshUi();
        }

        public void ToggleGrid()
        {
            ShowGrid = !ShowGrid;
            RefreshUi();
        }

        public Vector2 Snap(Vector2 p)
        {
            float step = SnapSteps[SnapIndex];
            return step <= 0f ? p : new Vector2(Mathf.Round(p.x / step) * step, Mathf.Round(p.y / step) * step);
        }

        // ───────────────────────── 배치 / 선택 / 지우기 ─────────────────────────

        /// <summary>팔레트에서 고른 것을 한 개 놓는다 (강은 물과 강둑을 함께). 놓은 물건의 참조를 돌려준다.</summary>
        public ItemRef PlaceStamp(Vector2 world, float rotation = 0f)
        {
            if (Model == null || SelectedEntry == null || SelectedEntry.Sprite == null) return ItemRef.None;
            var pos = Snap(world);
            ItemRef last = ItemRef.None;
            if (SelectedEntry.companion != null) Model.Add(SelectedEntry.companion.Create(pos, rng, rotation));
            last = Model.Add(SelectedEntry.Create(pos, rng, rotation));
            AudioManager.Play(SfxId.Click);
            return last;
        }

        public bool SelectAt(Vector2 world)
        {
            if (Model != null && Model.FindNearest(world, out var r))
            {
                Selected = r;
                RefreshUi();
                return true;
            }
            Deselect();
            return false;
        }

        public void Deselect()
        {
            Selected = ItemRef.None;
            dragging = false;
            if (built) RefreshUi();
        }

        public int EraseAt(Vector2 world)
        {
            if (Model == null) return 0;
            Func<PropPlacement, bool> filter = null;
            switch (CurrentEraseTarget)
            {
                case EraseTarget.Props: filter = p => TerrainMap.KindOf(p) == MapItemKind.Prop; break;
                case EraseTarget.Patches: filter = p => TerrainMap.KindOf(p) == MapItemKind.Patch; break;
                case EraseTarget.Water: filter = p => { var k = TerrainMap.KindOf(p); return k == MapItemKind.Water || k == MapItemKind.Bank || k == MapItemKind.Pond; }; break;
            }
            return Model.EraseCircle(world, BrushRadius, filter);
        }

        /// <summary>선택한 물건을 바꾼다 (실행 취소 한 번).</summary>
        public bool ModifySelected(Func<PropPlacement, PropPlacement> change)
        {
            if (Model == null || !Model.TryGet(Selected, out var p)) return false;
            Model.BeginStroke();
            Selected = Model.Replace(Selected, change(p));
            Model.EndStroke();
            RefreshUi();
            return true;
        }

        public void RotateSelected(float degrees) => ModifySelected(p =>
        {
            if (MapEditModel.CanRotate(p)) p.rotation += degrees;   // 서 있는 소품은 돌리지 않는다
            return p;
        });

        public void ScaleSelected(float factor) => ModifySelected(p => { p.scale = Mathf.Clamp(p.scale * factor, 0.25f, 5f); return p; });

        public void FlipSelected() => ModifySelected(p => { p.flipX = !p.flipX; return p; });

        public void DeleteSelected()
        {
            if (Model == null || !Model.Remove(Selected)) return;
            Deselect();
        }

        public void DuplicateSelected()
        {
            if (Model == null || !Model.TryGet(Selected, out var p)) return;
            p.position += new Vector2(1f, -0.6f);
            Model.BeginStroke();
            Selected = Model.Add(p);
            Model.EndStroke();
            RefreshUi();
        }

        public void ResetCurrentChunk()
        {
            if (Model == null) return;
            var chunk = TerrainMap.ChunkOf(CameraCenter());
            Flash(Model.ResetChunk(chunk) ? $"칸 {chunk.x},{chunk.y}: 자동 생성으로 되돌렸습니다" : "이 칸은 아직 고치지 않았습니다");
            Deselect();
        }

        public void ClearCurrentChunk()
        {
            if (Model == null) return;
            var chunk = TerrainMap.ChunkOf(CameraCenter());
            Model.ClearChunk(chunk);
            Flash($"칸 {chunk.x},{chunk.y}: 비웠습니다");
            Deselect();
        }

        public void Undo()
        {
            if (Model != null && Model.Undo()) { Deselect(); Flash("실행 취소"); }
        }

        public void Redo()
        {
            if (Model != null && Model.Redo()) { Deselect(); Flash("다시 실행"); }
        }

        Vector2 CameraCenter() => cam != null ? (Vector2)cam.transform.position : Vector2.zero;

        // ───────────────────────── 입력 ─────────────────────────

        void Update()
        {
            if (!built) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (messageLeft > 0f) messageLeft -= Time.unscaledDeltaTime;

            if (kb != null && mouse != null)
            {
                bool overUi = PointerOverUi();
                HandleCamera(kb, mouse, overUi);
                HandleHotkeys(kb);
                HandleMouse(kb, mouse, overUi);
            }

            ApplyDirtyChunks();
            UpdateOverlays();
            UpdateStatus();
        }

        static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        Vector2 MouseWorld(Mouse mouse)
        {
            Vector2 s = mouse.position.ReadValue();
            return cam.ScreenToWorldPoint(new Vector3(s.x, s.y, -cam.transform.position.z));
        }

        void HandleCamera(Keyboard kb, Mouse mouse, bool overUi)
        {
            bool ctrl = kb.ctrlKey.isPressed;
            var dir = Vector2.zero;
            if (!ctrl)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            }
            if (dir != Vector2.zero)
                cam.transform.position += (Vector3)(dir.normalized * cam.orthographicSize * 1.8f * Time.unscaledDeltaTime);

            if (mouse.rightButton.isPressed && !overUi || mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float unitsPerPixel = cam.orthographicSize * 2f / Screen.height;
                cam.transform.position -= (Vector3)(delta * unitsPerPixel);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (!overUi && Mathf.Abs(scroll) > 0.01f)
            {
                float steps = Mathf.Abs(scroll) >= 5f ? scroll / 120f : scroll;   // 장치에 따라 한 칸이 120 이거나 1
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Pow(0.9f, steps), MinZoom, MaxZoom);
            }
        }

        void HandleHotkeys(Keyboard kb)
        {
            bool ctrl = kb.ctrlKey.isPressed, shift = kb.shiftKey.isPressed;
            if (ctrl)
            {
                if (kb.zKey.wasPressedThisFrame) { if (shift) Redo(); else Undo(); }
                else if (kb.yKey.wasPressedThisFrame) Redo();
                else if (kb.sKey.wasPressedThisFrame) Save();
                else if (kb.dKey.wasPressedThisFrame) DuplicateSelected();
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame) SetTool(Tool.Place);
            else if (kb.digit2Key.wasPressedThisFrame) SetTool(Tool.Select);
            else if (kb.digit3Key.wasPressedThisFrame) SetTool(Tool.Erase);
            else if (kb.gKey.wasPressedThisFrame) ToggleGrid();
            else if (kb.qKey.wasPressedThisFrame) RotateSelected(shift ? -1f : -15f);
            else if (kb.eKey.wasPressedThisFrame) RotateSelected(shift ? 1f : 15f);
            else if (kb.leftBracketKey.wasPressedThisFrame) ScaleSelected(1f / 1.1f);
            else if (kb.rightBracketKey.wasPressedThisFrame) ScaleSelected(1.1f);
            else if (kb.fKey.wasPressedThisFrame) FlipSelected();
            else if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) DeleteSelected();
            else if (kb.escapeKey.wasPressedThisFrame) OnEscape();
        }

        void OnEscape()
        {
            if (Selected.IsValid) { Deselect(); return; }
            if (Model != null && Model.Unsaved && !exitArmed)
            {
                exitArmed = true;
                Flash("저장하지 않은 변경이 있습니다. 한 번 더 누르면 저장하고 나갑니다");
                return;
            }
            if (Model != null && Model.Unsaved) Save();
            ExitToTitle();
        }

        void HandleMouse(Keyboard kb, Mouse mouse, bool overUi)
        {
            Vector2 world = MouseWorld(mouse);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                strokeActive = !overUi;
                if (strokeActive) OnPress(world);
            }
            else if (mouse.leftButton.isPressed && strokeActive) OnDrag(world);

            if (mouse.leftButton.wasReleasedThisFrame && strokeActive) OnRelease();
        }

        void OnPress(Vector2 world)
        {
            exitArmed = false;
            if (Model == null) return;
            switch (CurrentTool)
            {
                case Tool.Place:
                    if (SelectedEntry == null) { Flash("오른쪽 목록에서 놓을 그림을 먼저 고르세요"); strokeActive = false; return; }
                    Model.BeginStroke();
                    PlaceStamp(world);
                    lastStamp = world;
                    hasLastStamp = true;
                    break;
                case Tool.Select:
                    if (SelectAt(world) && Model.TryGet(Selected, out var p))
                    {
                        dragOffset = p.position - world;   // 실제로 움직일 때 실행 취소 지점을 만든다 (그냥 눌러서 선택만 하면 기록이 쌓이지 않게)
                        dragging = true;
                        moveStarted = false;
                    }
                    break;
                case Tool.Erase:
                    Model.BeginStroke();
                    EraseAt(world);
                    break;
            }
        }

        void OnDrag(Vector2 world)
        {
            if (Model == null) return;
            switch (CurrentTool)
            {
                case Tool.Place:
                    if (SelectedEntry == null || !hasLastStamp) break;
                    if (Vector2.Distance(world, lastStamp) >= SelectedEntry.spacing)
                    {
                        Vector2 move = world - lastStamp;
                        float angle = SelectedEntry.alignToStroke ? Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg : 0f;
                        PlaceStamp(world, angle);
                        lastStamp = world;
                    }
                    break;
                case Tool.Select:
                    if (dragging && Model.TryGet(Selected, out var p))
                    {
                        var target = Snap(world + dragOffset);
                        if (target == p.position) break;
                        if (!moveStarted) { Model.BeginStroke(); moveStarted = true; }
                        p.position = target;
                        Selected = Model.Replace(Selected, p);
                    }
                    break;
                case Tool.Erase:
                    EraseAt(world);
                    break;
            }
        }

        void OnRelease()
        {
            strokeActive = false;
            dragging = false;
            hasLastStamp = false;
            if (Model != null) Model.EndStroke();
        }

        void ApplyDirtyChunks()
        {
            if (Model == null || spawner == null) return;
            foreach (var chunk in Model.ConsumeDirtyChunks())
            {
                spawner.RebuildChunk(chunk);
                background.Collision?.Invalidate(chunk);   // 보이는 것과 막는 것이 어긋나지 않게 (전투 테스트는 어차피 새로 만든다)
            }
        }

        // ───────────────────────── 화면 위 표시 ─────────────────────────

        static Sprite WhiteSprite()
        {
            if (whiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            return whiteSprite;
        }

        /// <summary>지형 그림이 아직 없을 때의 임시 바닥 (4x4 유닛 단색, 타일링용 FullRect).</summary>
        static Sprite GroundFallback()
        {
            if (groundFallback == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(40, 60, 40, 255);
                tex.SetPixels32(px);
                tex.Apply();
                groundFallback = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                groundFallback.hideFlags = HideFlags.HideAndDontSave;
            }
            return groundFallback;
        }

        /// <summary>얇은 고리 (지름 2유닛).</summary>
        static Sprite RingSprite()
        {
            if (ringSprite == null)
            {
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.04f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
                ringSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            return ringSprite;
        }

        SpriteRenderer NewOverlay(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerName = GameLayers.Sorting.Effect;
            sr.sortingOrder = order;
            return sr;
        }

        void BuildOverlays()
        {
            ghost = NewOverlay("Ghost", null, new Color(1f, 1f, 1f, 0.55f), 20);
            selectionRing = NewOverlay("SelectionRing", RingSprite(), new Color(1f, 0.9f, 0.3f, 1f), 21);
            brushRing = NewOverlay("BrushRing", RingSprite(), new Color(1f, 0.4f, 0.35f, 1f), 21);
            startRing = NewOverlay("StartArea", RingSprite(), new Color(1f, 1f, 1f, 0.5f), 2);
            startRing.transform.localScale = Vector3.one * TerrainMap.ClearRadius;
            selectionRing.gameObject.SetActive(false);
            brushRing.gameObject.SetActive(false);
            ghost.gameObject.SetActive(false);
        }

        SpriteRenderer Pooled(List<SpriteRenderer> pool, int index, string name, Color color, int order)
        {
            while (pool.Count <= index) pool.Add(NewOverlay(name, WhiteSprite(), color, order));
            var sr = pool[index];
            sr.gameObject.SetActive(true);
            return sr;
        }

        static void HideFrom(List<SpriteRenderer> pool, int from)
        {
            for (int i = from; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        void UpdateOverlays()
        {
            if (cam == null || Model == null) return;
            var mouse = Mouse.current;
            bool overUi = PointerOverUi();
            Vector2 world = mouse != null ? MouseWorld(mouse) : Vector2.zero;

            // 고른 그림을 놓기 전 미리 보기
            bool showGhost = CurrentTool == Tool.Place && SelectedEntry != null && SelectedEntry.Sprite != null && !overUi && mouse != null;
            ghost.gameObject.SetActive(showGhost);
            if (showGhost)
            {
                ghost.sprite = SelectedEntry.Sprite;
                var c = SelectedEntry.tinted ? SelectedEntry.tint : Color.white;
                ghost.color = new Color(c.r, c.g, c.b, SelectedEntry.tinted ? Mathf.Max(c.a, 0.35f) : 0.55f);
                float scale = (SelectedEntry.scaleMin + SelectedEntry.scaleMax) * 0.5f;
                float stretch = (SelectedEntry.stretchMin + SelectedEntry.stretchMax) * 0.5f;
                ghost.transform.position = Snap(world);
                ghost.transform.localScale = new Vector3(scale, scale * stretch, 1f);
            }

            // 지우개 반경
            bool showBrush = CurrentTool == Tool.Erase && !overUi && mouse != null;
            brushRing.gameObject.SetActive(showBrush);
            if (showBrush)
            {
                brushRing.transform.position = world;
                brushRing.transform.localScale = Vector3.one * BrushRadius;
            }

            // 선택 표시
            bool hasSel = CurrentTool == Tool.Select && Model.TryGet(Selected, out var sel);
            selectionRing.gameObject.SetActive(hasSel);
            if (hasSel)
            {
                Model.TryGet(Selected, out var p);
                selectionRing.transform.position = MapEditModel.VisualCenter(p);
                selectionRing.transform.localScale = Vector3.one * Mathf.Max(0.5f, MapEditModel.PickRadius(p));
            }

            DrawGridAndMarks();
        }

        void DrawGridAndMarks()
        {
            float size = TerrainMap.ChunkSize;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            float line = cam.orthographicSize * 0.006f;

            int lines = 0;
            if (ShowGrid)
            {
                int x0 = Mathf.FloorToInt((c.x - halfW) / size), x1 = Mathf.CeilToInt((c.x + halfW) / size);
                int y0 = Mathf.FloorToInt((c.y - halfH) / size), y1 = Mathf.CeilToInt((c.y + halfH) / size);
                var color = new Color(1f, 1f, 1f, 0.25f);
                for (int x = x0; x <= x1; x++)
                {
                    var sr = Pooled(gridLines, lines++, "GridLine", color, 3);
                    sr.transform.position = new Vector3(x * size, c.y, 0f);
                    sr.transform.localScale = new Vector3(line, halfH * 2f + size, 1f);
                }
                for (int y = y0; y <= y1; y++)
                {
                    var sr = Pooled(gridLines, lines++, "GridLine", color, 3);
                    sr.transform.position = new Vector3(c.x, y * size, 0f);
                    sr.transform.localScale = new Vector3(halfW * 2f + size, line, 1f);
                }
            }
            HideFrom(gridLines, lines);
            startRing.gameObject.SetActive(ShowGrid);

            // 직접 고친 칸 표시 (연한 초록 덮개)
            int marks = 0;
            if (ShowGrid)
                foreach (var chunk in Map.CustomChunks)
                {
                    var sr = Pooled(chunkMarks, marks++, "CustomChunk", new Color(0.35f, 1f, 0.45f, 0.10f), 1);
                    sr.transform.position = new Vector3((chunk.x + 0.5f) * size, (chunk.y + 0.5f) * size, 0f);
                    sr.transform.localScale = new Vector3(size, size, 1f);
                }
            HideFrom(chunkMarks, marks);
        }

        // ───────────────────────── 상태 표시 ─────────────────────────

        void Flash(string message)
        {
            Message = message;
            messageLeft = 6f;
            if (statusLabel != null) statusLabel.text = message;
        }

        void UpdateStatus()
        {
            if (statusLabel == null || Model == null) return;
            if (messageLeft > 0f) { statusLabel.text = Message; return; }

            Vector2 pos = Mouse.current != null ? MouseWorld(Mouse.current) : CameraCenter();
            var chunk = TerrainMap.ChunkOf(pos);
            statusLabel.text = $"({pos.x:0.0}, {pos.y:0.0})  칸 {chunk.x},{chunk.y} [{(Map.IsCustom(chunk) ? "직접 고친 칸" : "자동 생성")}]   " +
                               "좌클릭 배치/선택 · 우클릭 드래그 이동 · 휠 확대 · 1 배치 2 선택 3 지우개 · Q/E 회전 · [ ] 크기 · F 반전 · Del 삭제 · Ctrl+Z/Y · Ctrl+S 저장";
            undoButton.interactable = Model.CanUndo;
            redoButton.interactable = Model.CanRedo;
        }

        // ───────────────────────── UI 만들기 ─────────────────────────

        void BuildUi()
        {
            var canvasGo = new GameObject("MapEditorCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            BuildTopBar(root);
            BuildLeftPanel(root);
            BuildRightPanel(root);
            BuildBottomBar(root);
        }

        void BuildTopBar(RectTransform root)
        {
            var bar = NewPanel("TopBar", root, PanelColor);
            Anchor(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            bar.sizeDelta = new Vector2(0f, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;

            var left = NewRow("Left", bar, 8f);
            Anchor(left, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(0f, 0.5f));
            left.offsetMin = new Vector2(10f, 8f);
            left.offsetMax = new Vector2(0f, -8f);
            Bind(NewButton("PrevCastle", left, "◀", 64f, 26), () => LoadCastle(castleIndex - 1));
            castleLabel = NewText("CastleName", left, 28, TextAnchor.MiddleCenter, "");
            castleLabel.color = Gold;
            castleLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 250f;
            Bind(NewButton("NextCastle", left, "▶", 64f, 26), () => LoadCastle(castleIndex + 1));
            var terrainButton = NewButton("TerrainButton", left, "", 290f, 22);
            terrainLabel = terrainButton.GetComponentInChildren<Text>();
            Bind(terrainButton, CycleTerrain);
            var gridButton = NewButton("GridButton", left, "", 130f, 24);
            gridLabel = gridButton.GetComponentInChildren<Text>();
            Bind(gridButton, ToggleGrid);
            var snapButton = NewButton("SnapButton", left, "", 150f, 24);
            snapLabel = snapButton.GetComponentInChildren<Text>();
            Bind(snapButton, CycleSnap);
            undoButton = NewButton("UndoButton", left, "↶", 64f, 28);
            Bind(undoButton, Undo);
            redoButton = NewButton("RedoButton", left, "↷", 64f, 28);
            Bind(redoButton, Redo);

            var right = NewRow("Right", bar, 8f);
            Anchor(right, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            right.offsetMin = new Vector2(0f, 8f);
            right.offsetMax = new Vector2(-10f, -8f);
            right.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            var saveButton = NewButton("SaveButton", right, "저장 (Ctrl+S)", 190f, 24);
            saveLabel = saveButton.GetComponentInChildren<Text>();
            Bind(saveButton, () => Save());
            Bind(NewButton("TestButton", right, "▶ 전투 테스트", 190f, 24), TestBattle);
            Bind(NewButton("TitleButton", right, "타이틀 (Esc)", 160f, 24), OnEscape);
        }

        void BuildLeftPanel(RectTransform root)
        {
            var panel = NewPanel("LeftPanel", root, PanelColor);
            Anchor(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            panel.offsetMin = new Vector2(0f, BottomBarHeight);
            panel.offsetMax = new Vector2(LeftPanelWidth, -TopBarHeight);

            var col = NewColumn("Column", panel, 6f);
            Stretch(col);
            col.offsetMin = new Vector2(8f, 8f);
            col.offsetMax = new Vector2(-8f, -8f);

            AddLabel(col, "도구", 22, Gold);
            toolButtons[Tool.Place] = NewButton("ToolPlace", col, "1  배치", 0f, 24, 52f);
            toolButtons[Tool.Select] = NewButton("ToolSelect", col, "2  선택/이동", 0f, 24, 52f);
            toolButtons[Tool.Erase] = NewButton("ToolErase", col, "3  지우개", 0f, 24, 52f);
            Bind(toolButtons[Tool.Place], () => SetTool(Tool.Place));
            Bind(toolButtons[Tool.Select], () => SetTool(Tool.Select));
            Bind(toolButtons[Tool.Erase], () => SetTool(Tool.Erase));

            var brushRow = NewRow("BrushRow", col, 6f);
            brushRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            Bind(NewButton("BrushMinus", brushRow, "－", 56f, 24), () => ChangeBrush(-0.5f));
            brushLabel = NewText("BrushLabel", brushRow, 20, TextAnchor.MiddleCenter, "");
            brushLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Bind(NewButton("BrushPlus", brushRow, "＋", 56f, 24), () => ChangeBrush(0.5f));
            var eraseButton = NewButton("EraseTarget", col, "", 0f, 20, 44f);
            eraseLabel = eraseButton.GetComponentInChildren<Text>();
            Bind(eraseButton, CycleEraseTarget);

            AddLabel(col, "선택한 물건", 22, Gold);
            selectionLabel = NewText("SelectionLabel", col, 18, TextAnchor.UpperLeft, "");
            selectionLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

            var grid = NewRect("EditGrid", col);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(110f, 44f);
            gl.spacing = new Vector2(6f, 6f);
            grid.gameObject.AddComponent<LayoutElement>().preferredHeight = 4 * 44f + 3 * 6f;
            AddGridButton(grid, "크기 －", () => ScaleSelected(1f / 1.1f));
            AddGridButton(grid, "크기 ＋", () => ScaleSelected(1.1f));
            AddGridButton(grid, "회전 ↺", () => RotateSelected(-15f));
            AddGridButton(grid, "회전 ↻", () => RotateSelected(15f));
            AddGridButton(grid, "좌우 반전", FlipSelected);
            AddGridButton(grid, "복제", DuplicateSelected);
            AddGridButton(grid, "삭제", DeleteSelected);
            AddGridButton(grid, "선택 해제", Deselect);

            AddLabel(col, "칸 / 전체 (카메라 가운데 칸)", 18, Gold);
            Bind(NewButton("ResetChunk", col, "이 칸 자동 생성으로", 0f, 20, 44f), ResetCurrentChunk);
            Bind(NewButton("ClearChunk", col, "이 칸 비우기", 0f, 20, 44f), ClearCurrentChunk);
            Bind(NewButton("ReloadButton", col, "저장본 다시 열기", 0f, 20, 44f), ReloadFromDisk);
            Bind(NewButton("ResetAll", col, "전부 자동 생성으로", 0f, 20, 44f), ResetAll);
        }

        void AddGridButton(RectTransform grid, string label, UnityEngine.Events.UnityAction action)
        {
            var b = NewButton("Btn", grid, label, 0f, 20);
            Bind(b, action);
        }

        void BuildRightPanel(RectTransform root)
        {
            var panel = NewPanel("RightPanel", root, PanelColor);
            Anchor(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            panel.offsetMin = new Vector2(-RightPanelWidth, BottomBarHeight);
            panel.offsetMax = new Vector2(0f, -TopBarHeight);

            AddCornerLabel(panel, "그림 (눌러서 고르기)", 22, new Vector2(12f, -8f));

            tabRow = NewRect("Tabs", panel);
            Anchor(tabRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tabRow.offsetMin = new Vector2(8f, -190f);
            tabRow.offsetMax = new Vector2(-8f, -44f);
            var tg = tabRow.gameObject.AddComponent<GridLayoutGroup>();
            tg.cellSize = new Vector2(88f, 44f);
            tg.spacing = new Vector2(6f, 6f);
            foreach (var group in groups)
            {
                var name = group;
                var tab = NewButton("Tab_" + name, tabRow, name, 0f, 20);
                Bind(tab, () => ShowGroup(name));
                tabButtons.Add(tab);
                tabNames.Add(name);
            }

            var scroll = NewRect("Scroll", panel);
            Anchor(scroll, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            scroll.offsetMin = new Vector2(8f, 70f);
            scroll.offsetMax = new Vector2(-8f, -196f);
            var scrollImage = scroll.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0.25f);

            var viewport = NewRect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            paletteContent = NewRect("Content", viewport);
            paletteContent.anchorMin = new Vector2(0f, 1f);
            paletteContent.anchorMax = new Vector2(1f, 1f);
            paletteContent.pivot = new Vector2(0.5f, 1f);
            paletteContent.anchoredPosition = Vector2.zero;
            paletteContent.sizeDelta = Vector2.zero;
            var cg = paletteContent.gameObject.AddComponent<GridLayoutGroup>();
            cg.cellSize = new Vector2(112f, 112f);
            cg.spacing = new Vector2(8f, 8f);
            cg.padding = new RectOffset(8, 8, 8, 8);
            var fitter = paletteContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = paletteContent;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;

            paletteHint = NewText("PaletteHint", panel, 20, TextAnchor.MiddleLeft, "");
            Anchor(paletteHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            paletteHint.rectTransform.offsetMin = new Vector2(12f, 8f);
            paletteHint.rectTransform.offsetMax = new Vector2(-12f, 66f);
        }

        void BuildBottomBar(RectTransform root)
        {
            var bar = NewPanel("BottomBar", root, PanelColor);
            Anchor(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            bar.sizeDelta = new Vector2(0f, BottomBarHeight);
            bar.anchoredPosition = Vector2.zero;
            statusLabel = NewText("Status", bar, 19, TextAnchor.MiddleLeft, "");
            statusLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(statusLabel.rectTransform);
            statusLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            statusLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
        }

        // ───────────────────────── 팔레트 ─────────────────────────

        /// <summary>팔레트에 한 그룹(지형)의 그림만 보여 준다.</summary>
        public void ShowGroup(string group)
        {
            if (paletteContent == null) return;
            if (string.IsNullOrEmpty(group) || !groups.Contains(group)) group = groups.Count > 0 ? groups[0] : null;
            currentGroup = group;

            foreach (var cell in paletteCells) { cell.SetActive(false); Destroy(cell); }
            paletteCells.Clear();
            paletteCellBackgrounds.Clear();
            paletteCellEntries.Clear();

            foreach (var entry in palette)
            {
                if (entry.group != group) continue;
                var captured = entry;
                var cell = NewRect("Cell_" + entry.label, paletteContent);
                var bg = cell.gameObject.AddComponent<Image>();
                bg.color = new Color(0.16f, 0.13f, 0.2f, 1f);
                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); SelectEntry(captured); });

                var thumb = NewRect("Thumb", cell);
                Anchor(thumb, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                thumb.offsetMin = new Vector2(8f, 26f);
                thumb.offsetMax = new Vector2(-8f, -8f);
                var img = thumb.gameObject.AddComponent<Image>();
                img.sprite = entry.Sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                if (entry.tinted)
                {
                    // 얼룩은 반투명 흰 그림이라 어두운 바탕에서 색이 보이게 알파를 올려 보여 준다
                    img.color = new Color(entry.tint.r, entry.tint.g, entry.tint.b, 1f);
                }

                var label = NewText("Label", cell, 16, TextAnchor.LowerCenter, entry.label);
                Anchor(label.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f));
                label.rectTransform.offsetMin = new Vector2(2f, 2f);
                label.rectTransform.offsetMax = new Vector2(-2f, 28f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.font = UiFont.Get();

                paletteCells.Add(cell.gameObject);
                paletteCellBackgrounds.Add(bg);
                paletteCellEntries.Add(entry);
            }

            RefreshUi();
        }

        // ───────────────────────── UI 갱신 ─────────────────────────

        void RefreshUi()
        {
            if (!built || castleLabel == null) return;

            castleLabel.text = CurrentCastle != null ? $"{CurrentCastle.displayName}{(Model != null && Model.Unsaved ? " *" : "")}" : "";
            if (CurrentCastle != null && CurrentCastle.id == MapStore.FreeBattleId) castleLabel.text = "자유 전투" + (Model != null && Model.Unsaved ? " *" : "");
            if (terrainLabel != null)
                terrainLabel.text = TerrainOverride.HasValue ? $"지형: {MapPalette.TerrainLabel(TerrainOverride.Value)} (변경)" : $"지형: {(Map != null ? MapPalette.TerrainLabel(Map.Terrain) : "-")} (기본)";
            if (gridLabel != null) gridLabel.text = ShowGrid ? "격자 켜짐" : "격자 꺼짐";
            if (snapLabel != null) snapLabel.text = SnapSteps[SnapIndex] <= 0f ? "맞춤: 없음" : $"맞춤: {SnapSteps[SnapIndex]:0.##}";
            if (brushLabel != null) brushLabel.text = $"지우개 반경 {BrushRadius:0.0}";
            if (eraseLabel != null) eraseLabel.text = "지울 것: " + EraseLabel(CurrentEraseTarget);
            if (saveLabel != null) saveLabel.text = Model != null && Model.Unsaved ? "저장 * (Ctrl+S)" : "저장 (Ctrl+S)";

            foreach (var kv in toolButtons)
                SetHighlight(kv.Value, kv.Key == CurrentTool);
            for (int i = 0; i < tabButtons.Count; i++)
                SetHighlight(tabButtons[i], tabNames[i] == currentGroup);
            for (int i = 0; i < paletteCellBackgrounds.Count; i++)
                paletteCellBackgrounds[i].color = paletteCellEntries[i] == SelectedEntry ? new Color(0.62f, 0.5f, 0.15f, 1f) : new Color(0.16f, 0.13f, 0.2f, 1f);
            if (paletteHint != null)
                paletteHint.text = SelectedEntry != null ? $"고른 그림: {SelectedEntry.label}" + (SelectedEntry.alignToStroke ? "\n끌면서 칠하면 이어서 강이 그려집니다" : "") : "그림을 고르고 맵을 누르세요";

            if (selectionLabel != null)
            {
                if (Model != null && Model.TryGet(Selected, out var p))
                    selectionLabel.text = p.prop != null && p.prop.standing
                        ? $"{TerrainMap.KeyOf(p.prop)}\n크기 {p.scale:0.00}  서 있는 소품 (회전 없음)"
                        : $"{TerrainMap.KeyOf(p.prop)}\n크기 {p.scale:0.00}  회전 {p.rotation:0}°";
                else selectionLabel.text = CurrentTool == Tool.Select ? "맵의 물건을 누르면 선택됩니다" : "(선택/이동 도구에서 고르세요)";
            }
        }

        static string EraseLabel(EraseTarget t)
        {
            switch (t)
            {
                case EraseTarget.Props: return "소품만";
                case EraseTarget.Patches: return "얼룩만";
                case EraseTarget.Water: return "강/연못만";
                default: return "전부";
            }
        }

        void SetHighlight(Button button, bool on)
        {
            if (button == null) return;
            var img = button.targetGraphic as Image;
            if (img == null) return;
            if (buttonBase == Color.white) buttonBase = img.color;
            img.color = on ? new Color(0.85f, 0.65f, 0.2f, 1f) : buttonBase;
        }

        // ───────────────────────── UI 헬퍼 ─────────────────────────

        void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                action();
            });
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform NewPanel(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        static RectTransform NewRow(string name, Transform parent, float spacing)
        {
            var rt = NewRect(name, parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return rt;
        }

        static RectTransform NewColumn(string name, Transform parent, float spacing)
        {
            var rt = NewRect(name, parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return rt;
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

        static void AddLabel(Transform parent, string text, int size, Color color)
        {
            var t = NewText("Label", parent, size, TextAnchor.MiddleLeft, text);
            t.color = color;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = size + 14f;
        }

        static void AddCornerLabel(RectTransform parent, string text, int size, Vector2 pos)
        {
            var t = NewText("Heading", parent, size, TextAnchor.UpperLeft, text);
            t.color = Gold;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = new Vector2(360f, 32f);
        }

        static Button NewButton(string name, Transform parent, string label, float width, int fontSize, float height = 0f)
        {
            var rt = NewRect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;
            var img = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            UiSkin.StyleButton(btn);

            var text = NewText("Label", rt, fontSize, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform);
            return btn;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
