using System;
using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samkuk.Strategy
{
    /// <summary>
    /// 내정 화면(전략 지도 + 성 화면). 화면은 전부 코드로 만든다: 성이 늘거나 줄어도 씬을 다시 구성할 필요가 없고,
    /// 씬에는 이 컴포넌트가 붙은 캔버스만 있으면 된다. 상태 규칙은 <see cref="StrategyModel"/>.
    ///   지도: 성 마커를 누르면 선택(오른쪽에 정보), 선택한 성을 다시 누르거나 [성에 들어가기] → 성 화면.
    ///   성 화면: 배경 그림 + 인접한 성 버튼. ESC = 성 화면 → 지도 → 타이틀.
    /// 새 UI 이름은 UiSkin 규칙(Box/Title/Button)을 따르되, 런타임에 만든 것이라 스킨은 직접 입힌다.
    /// </summary>
    public class StrategyUI : MonoBehaviour
    {
        // 이 화면의 기준 해상도. 다른 화면(타이틀/HUD)은 1920x1080 기준이지만 내정은 지도/명령이 들어갈 공간이 더 필요해 2560x1440 으로 둔다.
        public static readonly Vector2 ReferenceResolution = new Vector2(2560f, 1440f);

        // 지도 영역 (기준 해상도 안, 16:9). 그림은 이 영역에 꽉 채워 늘리므로 성 위치(0~1)가 그대로 좌표가 된다.
        const float MapW = 1960f, MapH = 1102.5f;
        static readonly Vector2 MapPos = new Vector2(40f, -160f);

        const string HintNormal = "성을 누르면 선택, 선택한 성을 다시 누르면 들어갑니다";
        const string HintPickHome = "시작할 성을 고르세요. 선택한 성을 다시 누르거나 [이 성에서 시작]을 누르면 그 성이 내 성이 됩니다";

        [SerializeField] CastleCatalog catalog;
        [SerializeField, Tooltip("전략 지도 그림 (2560x1440). tools/castle_art/generate.ps1 -Only Map")] Sprite mapSprite;

        /// <summary>씬 로드 방법 (테스트에서 대체 가능).</summary>
        public Action<string> SceneLoader { get; set; } = name => SceneManager.LoadScene(name);

        public StrategyModel Model { get; private set; }
        public int MarkerCount => markers.Count;
        public bool IsMapVisible => mapPanel != null && mapPanel.activeSelf;
        public bool IsCastleVisible => castlePanel != null && castlePanel.activeSelf;
        public bool IsTargetPanelVisible => targetPanel != null && targetPanel.activeSelf;
        public string TargetPanelTitle => targetTitle != null ? targetTitle.text : "";

        class Marker
        {
            public CastleData castle;
            public Button button;
            public GameObject ring;
            public GameObject homeMark;
            public Image rim;
        }

        class Link
        {
            public CastleData a, b;
            public Image image;
        }

        readonly List<Marker> markers = new List<Marker>();
        readonly List<Link> links = new List<Link>();
        readonly List<Button> neighborButtons = new List<Button>();

        GameObject mapPanel, castlePanel;
        Text infoTitle, infoSub, infoNeighbors, castleTitle, castleSub;
        Text hintLabel, enterLabel, castleNote;
        Text progressLabel, targetTitle;
        GameObject targetPanel;
        RectTransform targetRows;
        readonly List<Button> targetButtons = new List<Button>();
        Button changeHomeButton, sortieButton;
        Image infoPreview, castleBackground;
        Button enterButton;
        RectTransform neighborRow;
        bool built;

        static Sprite circleSprite, ringSprite;

        /// <summary>테스트/코드에서 쓸 때: Start 전에 데이터를 넣는다 (씬에서는 직렬화된 값을 쓴다).</summary>
        public void Configure(CastleCatalog castles, Sprite map)
        {
            catalog = castles;
            mapSprite = map;
        }

        void Start() => EnsureBuilt();

        /// <summary>화면을 한 번만 만든다. 이후 상태 변화는 모델의 Changed 로 갱신한다.</summary>
        public void EnsureBuilt()
        {
            if (built) return;
            built = true;

            ApplyCanvasScaler();
            Time.timeScale = 1f;
            Model = new StrategyModel(catalog);
            BuildUi();
            UiFont.Apply(gameObject);
            Model.SortieStarted += OnSortieStarted;
            Model.Changed += Refresh;
            Refresh();
        }

        /// <summary>캔버스 기준 해상도를 코드와 맞춘다 (씬에 저장된 값이 달라도 배치가 어긋나지 않게).</summary>
        void ApplyCanvasScaler()
        {
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // 고정 배치 화면이라 화면 비율이 16:9 가 아니어도(세로 화면, 울트라와이드) 2560x1440 영역이 통째로 보이게 확장 방식을 쓴다
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        void OnSortieStarted(CastleData castle) => SceneLoader?.Invoke(GameManager.BattleSceneName);

        void OnDestroy()
        {
            if (Model != null) Model.Changed -= Refresh;
            if (Model != null) Model.SortieStarted -= OnSortieStarted;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || Model == null) return;
            if (IsTargetPanelVisible) CloseTargetPanel();   // 공격 대상 선택창부터 닫는다
            else if (!Model.Leave()) BackToTitle();
        }

        public void BackToTitle() => SceneLoader?.Invoke(GameManager.TitleSceneName);

        /// <summary>연결되지 않은 필수 참조의 이름 목록 (셋업 검증/테스트용).</summary>
        public List<string> MissingReferences()
        {
            var missing = new List<string>();
            if (catalog == null) missing.Add(nameof(catalog));
            if (mapSprite == null) missing.Add(nameof(mapSprite));
            return missing;
        }

        // ───────────────────────── 화면 갱신 ─────────────────────────

        void Refresh()
        {
            if (Model == null) return;
            var sel = Model.Selected;

            mapPanel.SetActive(!Model.InCastle);
            castlePanel.SetActive(Model.InCastle);

            // 지도: 선택 표시와 선택한 성에 이어진 길 강조
            var theme = UiTheme.Get();
            foreach (var m in markers)
            {
                m.ring.SetActive(m.castle == sel);
                m.homeMark.SetActive(Model.IsHome(m.castle));
                // 테두리 색: 내 성은 금색, 공격할 수 있는 이웃 적 성은 붉은색, 나머지는 기본
                m.rim.color = Model.IsOwned(m.castle) ? theme.gold : (Model.IsAttackable(m.castle) ? AttackableRim : DefaultRim);
            }
            progressLabel.text = Model.IsUnified ? "천하 통일!" : (Model.HasHome ? $"보유 성 {Model.OwnedCount} / {Model.TotalCount}" : "");
            targetPanel.SetActive(false); // 상태가 바뀌면 공격 대상 선택창은 닫는다
            hintLabel.text = Model.HasHome ? HintNormal : HintPickHome;
            changeHomeButton.gameObject.SetActive(Model.HasHome && Model.OwnedCount <= 1); // 다른 성을 차지했다면 시작 성을 바꿀 수 없다
            enterLabel.text = Model.HasHome ? "성에 들어가기" : "이 성에서 시작";
            foreach (var l in links)
            {
                bool hot = sel != null && (l.a == sel || l.b == sel);
                l.image.color = hot ? new Color(theme.gold.r, theme.gold.g, theme.gold.b, 0.95f) : new Color(0.26f, 0.17f, 0.12f, 0.5f);
                var rt = l.image.rectTransform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, hot ? 8f : 4f);
            }

            // 오른쪽 정보 상자
            if (sel == null)
            {
                infoTitle.text = "성을 선택하세요";
                infoSub.text = "";
                infoNeighbors.text = "";
                infoPreview.enabled = false;
                enterButton.interactable = false;
            }
            else
            {
                infoTitle.text = $"{sel.displayName}  {sel.hanja}";
                infoSub.text = sel.Summary + (Model.IsHome(sel) ? "  ★ 내 성" : "");
                infoNeighbors.text = "인접한 성\n" + NeighborNames(sel);
                infoPreview.enabled = sel.background != null;
                infoPreview.sprite = sel.background;
                enterButton.interactable = true;
            }

            // 성 화면
            if (Model.InCastle && sel != null)
            {
                castleBackground.sprite = sel.background;
                castleTitle.text = $"{sel.displayName}  {sel.hanja}";
                castleSub.text = sel.Summary + (Model.IsHome(sel) ? "  ★ 내 성" : "");
                sortieButton.gameObject.SetActive(Model.CanSortie);
                castleNote.text = Model.IsOwned(sel)
                    ? "우리 성입니다. [출진]으로 이웃한 적 성을 공격합니다 (내정 명령은 다음 단계에서 추가됩니다)"
                    : (Model.IsAttackable(sel) ? "적의 성입니다. 이웃한 우리 성에서 출진해 공격할 수 있습니다" : "적의 성입니다. 우리 영토와 이어져 있지 않아 아직 공격할 수 없습니다");
                RebuildNeighborButtons(sel);
            }
        }

        static readonly Color DefaultRim = new Color(0.96f, 0.92f, 0.82f);
        static readonly Color AttackableRim = new Color(0.9f, 0.3f, 0.25f);

        string OwnerText(CastleData castle)
        {
            if (Model.IsHome(castle)) return "★ 내 성 (시작 성)";
            if (Model.IsOwned(castle)) return "● 우리 성";
            return Model.IsAttackable(castle) ? "▶ 적의 성 (공격 가능)" : "적의 성";
        }

        static string NeighborNames(CastleData castle)
        {
            var names = new List<string>();
            foreach (var n in castle.neighbors)
                if (n != null) names.Add(n.displayName);
            return string.Join(", ", names);
        }

        void RebuildNeighborButtons(CastleData castle)
        {
            foreach (var b in neighborButtons)
                if (b != null) Destroy(b.gameObject);
            neighborButtons.Clear();

            foreach (var n in castle.neighbors)
            {
                if (n == null) continue;
                var target = n;
                var btn = NewButton($"Neighbor_{n.id}", neighborRow, $"{n.displayName}", new Vector2(240f, 84f), 40);
                btn.GetComponent<LayoutElement>().preferredWidth = 200f;
                Bind(btn, () => Model.MoveTo(target));
                UiFont.Apply(btn.gameObject);
                neighborButtons.Add(btn);
            }
        }

        // ───────────────────────── 화면 만들기 ─────────────────────────

        void BuildUi()
        {
            var theme = UiTheme.Get();

            var bg = NewRect("Background", transform);
            Stretch(bg);
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.07f, 0.12f, 1f);
            bgImage.raycastTarget = false;

            BuildMapPanel(theme);
            BuildCastlePanel(theme);
        }

        void BuildMapPanel(UiTheme theme)
        {
            var panel = NewRect("MapPanel", transform);
            Stretch(panel);
            mapPanel = panel.gameObject;

            var title = NewText("Title", panel, 72, TextAnchor.MiddleLeft, "전략 지도");
            UiSkin.StyleTitle(title);
            TopLeft(title.rectTransform, new Vector2(60f, -30f), new Vector2(900f, 100f));
            progressLabel = NewText("Progress", panel, 50, TextAnchor.MiddleLeft, "");
            progressLabel.color = theme.gold;
            TopLeft(progressLabel.rectTransform, new Vector2(960f, -34f), new Vector2(900f, 100f));
            hintLabel = NewText("Hint", panel, 32, TextAnchor.MiddleLeft, HintNormal);
            hintLabel.color = new Color(0.85f, 0.78f, 0.7f);
            TopLeft(hintLabel.rectTransform, new Vector2(60f, -1292f), new Vector2(2440f, 56f));

            var back = NewButton("BackButton", panel, "타이틀로  [ESC]", new Vector2(420f, 84f), 36);
            TopRight(back.GetComponent<RectTransform>(), new Vector2(-40f, -38f));
            Bind(back, BackToTitle);

            // 시작 성을 다시 고르고 싶을 때 (내 성이 있을 때만 보인다)
            changeHomeButton = NewButton("ChangeHomeButton", panel, "시작 성 변경", new Vector2(400f, 84f), 34);
            TopRight(changeHomeButton.GetComponent<RectTransform>(), new Vector2(-500f, -38f));
            Bind(changeHomeButton, () => Model.ClearHome());

            // 지도 바탕 + 프레임
            var frame = NewRect("MapFrame", panel);
            TopLeft(frame, MapPos + new Vector2(-10f, 10f), new Vector2(MapW + 20f, MapH + 20f));
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.05f, 0.04f, 0.05f, 1f);
            frameImage.raycastTarget = false;
            UiSkin.AddFrame(frameImage, theme.frameThin);

            var map = NewRect("Map", panel);
            TopLeft(map, MapPos, new Vector2(MapW, MapH));
            var mapImage = map.gameObject.AddComponent<Image>();
            mapImage.sprite = mapSprite;
            mapImage.color = mapSprite != null ? Color.white : new Color(0.3f, 0.35f, 0.3f);
            mapImage.raycastTarget = false;

            var linkRoot = NewRect("Links", map);
            Stretch(linkRoot);
            var markerRoot = NewRect("Markers", map);
            Stretch(markerRoot);

            if (catalog != null)
            {
                BuildLinks(linkRoot);
                BuildMarkers(markerRoot);
            }

            BuildInfoBox(panel, theme);
        }

        void BuildLinks(RectTransform root)
        {
            var done = new HashSet<(CastleData, CastleData)>();
            foreach (var a in catalog.castles)
            {
                if (a == null) continue;
                foreach (var b in a.neighbors)
                {
                    if (b == null || done.Contains((b, a)) || !done.Add((a, b))) continue;

                    var rt = NewRect($"Link_{a.id}_{b.id}", root);
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    Vector2 pa = ToMap(a.mapPosition), pb = ToMap(b.mapPosition);
                    Vector2 d = pb - pa;
                    rt.anchoredPosition = (pa + pb) * 0.5f;
                    rt.sizeDelta = new Vector2(d.magnitude, 4f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    var img = rt.gameObject.AddComponent<Image>();
                    img.raycastTarget = false;
                    links.Add(new Link { a = a, b = b, image = img });
                }
            }
        }

        /// <summary>지도 위 좌표(0~1, 왼쪽 위가 원점)를 지도 영역 안의 UI 좌표(왼쪽 위 기준, y 아래가 음수)로.</summary>
        static Vector2 ToMap(Vector2 p) => new Vector2(p.x * MapW, -p.y * MapH);

        void BuildMarkers(RectTransform root)
        {
            foreach (var castle in catalog.castles)
            {
                if (castle == null) continue;

                float size = 28f + 9f * (int)castle.size;
                var rt = NewRect($"Marker_{castle.id}", root);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = ToMap(castle.mapPosition);
                rt.sizeDelta = new Vector2(size, size);

                // 바깥 테두리(누르는 대상) + 주(州) 색 점
                var rim = rt.gameObject.AddComponent<Image>();
                rim.sprite = Circle();
                rim.color = new Color(0.96f, 0.92f, 0.82f);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = rim;
                var colors = btn.colors;
                colors.highlightedColor = UiTheme.Get().gold;
                colors.pressedColor = new Color(0.8f, 0.7f, 0.4f);
                btn.colors = colors;

                var dot = NewRect("Dot", rt);
                Anchor(dot, Vector2.zero, Vector2.one);
                dot.offsetMin = new Vector2(4f, 4f);
                dot.offsetMax = new Vector2(-4f, -4f);
                var dotImage = dot.gameObject.AddComponent<Image>();
                dotImage.sprite = Circle();
                dotImage.color = Shade(RegionColor(castle.region), 0.72f);
                dotImage.raycastTarget = false;

                var ring = NewRect("Ring", rt);
                ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
                ring.sizeDelta = new Vector2(size + 22f, size + 22f);
                var ringImage = ring.gameObject.AddComponent<Image>();
                ringImage.sprite = Ring();
                ringImage.color = UiTheme.Get().gold;
                ringImage.raycastTarget = false;
                ring.gameObject.SetActive(false);

                // 내 성 표시 (★)
                var homeMark = NewText("HomeMark", rt, 40, TextAnchor.MiddleCenter, "★");
                homeMark.color = UiTheme.Get().gold;
                homeMark.rectTransform.anchorMin = homeMark.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                homeMark.rectTransform.pivot = new Vector2(0.5f, 0f);
                homeMark.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                homeMark.rectTransform.sizeDelta = new Vector2(48f, 48f);
                var homeOutline = homeMark.gameObject.AddComponent<Outline>();
                homeOutline.effectColor = new Color(0.1f, 0.06f, 0.04f, 0.95f);
                homeOutline.effectDistance = new Vector2(2f, -2f);
                homeMark.gameObject.SetActive(false);

                var label = NewText("Label", rt, 26, TextAnchor.MiddleCenter, castle.displayName);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -2f);
                label.rectTransform.sizeDelta = new Vector2(150f, 34f);
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.1f, 0.06f, 0.04f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);

                var target = castle;
                Bind(btn, () => OnMarkerClicked(target));
                markers.Add(new Marker { castle = castle, button = btn, ring = ring.gameObject, homeMark = homeMark.gameObject, rim = rim });
            }
        }

        /// <summary>마커 클릭: 처음엔 선택, 이미 선택한 성을 다시 누르면 주된 동작(내 성이 없으면 [이 성에서 시작], 있으면 [들어가기]).</summary>
        public void OnMarkerClicked(CastleData castle)
        {
            if (Model.Selected == castle) OnPrimaryAction();
            else Model.Select(castle);
        }

        /// <summary>정보 상자의 큰 버튼: 시작 성을 아직 안 골랐으면 그 성에서 시작, 골랐으면 선택한 성으로 들어간다.</summary>
        public void OnPrimaryAction()
        {
            if (!Model.HasHome) Model.StartHere();
            else Model.Enter();
        }

        void BuildInfoBox(RectTransform panel, UiTheme theme)
        {
            var box = NewRect("Box", panel);
            TopLeft(box, new Vector2(2040f, -160f), new Vector2(480f, MapH));
            UiSkin.StylePanel(box.gameObject.AddComponent<Image>());

            infoTitle = NewText("Title", box, 56, TextAnchor.MiddleCenter, "");
            UiSkin.StyleTitle(infoTitle);
            TopLeft(infoTitle.rectTransform, new Vector2(20f, -28f), new Vector2(440f, 90f));

            infoSub = NewText("Sub", box, 34, TextAnchor.MiddleCenter, "");
            infoSub.color = new Color(0.85f, 0.78f, 0.7f);
            TopLeft(infoSub.rectTransform, new Vector2(20f, -126f), new Vector2(440f, 52f));

            var preview = NewRect("Preview", box);
            TopLeft(preview, new Vector2(20f, -194f), new Vector2(440f, 247.5f));
            infoPreview = preview.gameObject.AddComponent<Image>();
            infoPreview.preserveAspect = true;
            infoPreview.raycastTarget = false;
            infoPreview.enabled = false;

            infoNeighbors = NewText("Neighbors", box, 34, TextAnchor.UpperLeft, "");
            infoNeighbors.lineSpacing = 1.2f;
            TopLeft(infoNeighbors.rectTransform, new Vector2(30f, -468f), new Vector2(420f, 420f));

            enterButton = NewButton("EnterButton", box, "성에 들어가기", new Vector2(440f, 100f), 44);
            var ert = enterButton.GetComponent<RectTransform>();
            ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0f);
            ert.pivot = new Vector2(0.5f, 0f);
            ert.anchoredPosition = new Vector2(0f, 30f);
            enterButton.targetGraphic.color = theme.primaryButton;
            UiSkin.StyleButton(enterButton, false);
            enterLabel = enterButton.GetComponentInChildren<Text>();
            Bind(enterButton, OnPrimaryAction);
        }

        void BuildCastlePanel(UiTheme theme)
        {
            var panel = NewRect("CastlePanel", transform);
            Stretch(panel);
            castlePanel = panel.gameObject;

            // 배경 그림: 화면 비율이 달라도 화면을 가득 덮도록 확대
            var bg = NewRect("CastleBackground", panel);
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
            bg.pivot = new Vector2(0.5f, 0.5f);
            castleBackground = bg.gameObject.AddComponent<Image>();
            castleBackground.raycastTarget = false;
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            // 위 띠: 성 이름
            var top = NewRect("TopBar", panel);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 170f);
            var topImage = top.gameObject.AddComponent<Image>();
            topImage.color = new Color(0.05f, 0.03f, 0.03f, 0.62f);
            topImage.raycastTarget = false;

            castleTitle = NewText("Title", top, 88, TextAnchor.MiddleLeft, "");
            UiSkin.StyleTitle(castleTitle);
            TopLeft(castleTitle.rectTransform, new Vector2(70f, -10f), new Vector2(1400f, 104f));
            castleSub = NewText("Sub", top, 38, TextAnchor.MiddleLeft, "");
            castleSub.color = new Color(0.9f, 0.84f, 0.75f);
            TopLeft(castleSub.rectTransform, new Vector2(74f, -112f), new Vector2(1400f, 50f));

            var toMap = NewButton("MapButton", top, "지도로  [ESC]", new Vector2(380f, 84f), 36);
            TopRight(toMap.GetComponent<RectTransform>(), new Vector2(-50f, -44f));
            Bind(toMap, () => Model.Leave());

            // 아래 띠: 인접한 성 / 내정 명령 자리
            var bottom = NewRect("BottomBar", panel);
            bottom.anchorMin = new Vector2(0f, 0f);
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(0f, 240f);
            var bottomImage = bottom.gameObject.AddComponent<Image>();
            bottomImage.color = new Color(0.05f, 0.03f, 0.03f, 0.62f);
            bottomImage.raycastTarget = false;

            var label = NewText("NeighborLabel", bottom, 36, TextAnchor.MiddleLeft, "인접한 성 (눌러서 이동)");
            label.color = new Color(0.9f, 0.84f, 0.75f);
            TopLeft(label.rectTransform, new Vector2(70f, -14f), new Vector2(1000f, 52f));

            neighborRow = NewRect("NeighborRow", bottom);
            TopLeft(neighborRow, new Vector2(70f, -76f), new Vector2(2420f, 96f));
            var layout = neighborRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            castleNote = NewText("Note", bottom, 30, TextAnchor.MiddleLeft, "");
            castleNote.color = new Color(0.7f, 0.64f, 0.58f);
            TopLeft(castleNote.rectTransform, new Vector2(70f, -184f), new Vector2(1600f, 44f));

            // 출진: 내 성 안에서만 보인다 (전투 씬으로 이동)
            sortieButton = NewButton("SortieButton", bottom, "출진", new Vector2(560f, 110f), 56);
            var sortieRt = sortieButton.GetComponent<RectTransform>();
            sortieRt.anchorMin = sortieRt.anchorMax = new Vector2(1f, 0f);
            sortieRt.pivot = new Vector2(1f, 0f);
            sortieRt.anchoredPosition = new Vector2(-60f, 60f);
            sortieButton.targetGraphic.color = theme.primaryButton;
            UiSkin.StyleButton(sortieButton, false);
            Bind(sortieButton, OpenTargetPanel);
            sortieButton.gameObject.SetActive(false);

            BuildTargetPanel(panel, theme);

            panel.gameObject.SetActive(false);
        }

        /// <summary>[출진] 을 누르면 뜨는 공격 대상 선택창 (이 성과 이웃한 적 성 목록)을 만든다.</summary>
        void BuildTargetPanel(RectTransform castlePanelRoot, UiTheme theme)
        {
            var root = NewRect("TargetPanel", castlePanelRoot);
            Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f); // 뒤의 버튼이 눌리지 않게 클릭도 막는다

            var box = NewRect("Box", root);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(1100f, 860f);
            UiSkin.StylePanel(box.gameObject.AddComponent<Image>());

            targetTitle = NewText("Title", box, 52, TextAnchor.MiddleCenter, "");
            UiSkin.StyleTitle(targetTitle);
            TopLeft(targetTitle.rectTransform, new Vector2(30f, -34f), new Vector2(1040f, 90f));

            targetRows = NewRect("Rows", box);
            TopLeft(targetRows, new Vector2(60f, -160f), new Vector2(980f, 540f));
            var layout = targetRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var cancel = NewButton("TargetCancelButton", box, "취소  [ESC]", new Vector2(360f, 84f), 38);
            var cancelRt = cancel.GetComponent<RectTransform>();
            cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.anchoredPosition = new Vector2(0f, 36f);
            Bind(cancel, CloseTargetPanel);

            targetPanel = root.gameObject;
            targetPanel.SetActive(false);
        }

        /// <summary>공격 대상 선택창을 연다: 선택한 성과 이웃한 적 성마다 버튼 하나. 없으면 안내만 보인다.</summary>
        public void OpenTargetPanel()
        {
            if (Model == null || !Model.CanSortie) return;

            foreach (var b in targetButtons)
                if (b != null) Destroy(b.gameObject);
            targetButtons.Clear();

            var targets = Model.AttackTargets();
            targetTitle.text = targets.Count > 0
                ? $"{Model.Selected.displayName}에서 어느 성을 공격하시겠습니까?"
                : "공격할 수 있는 성이 없습니다 (이웃한 성이 모두 우리 성입니다)";

            foreach (var t in targets)
            {
                var target = t;
                string label = $"{t.displayName}   {t.hanja}    {CastleData.SizeLabel(t.size)} · {CastleData.TerrainLabel(t.terrain)}";
                var btn = NewButton($"Target_{t.id}", targetRows, label, new Vector2(980f, 96f), 42);
                var le = btn.GetComponent<LayoutElement>();
                le.preferredHeight = 96f;
                le.minHeight = 96f;
                Bind(btn, () => Model.Sortie(target));
                UiFont.Apply(btn.gameObject);
                targetButtons.Add(btn);
            }
            targetPanel.SetActive(true);
        }

        public void CloseTargetPanel()
        {
            if (targetPanel != null) targetPanel.SetActive(false);
        }

        // ───────────────────────── 색 / 스프라이트 ─────────────────────────

        /// <summary>주(州)별 색. tools/castle_art/MapArt.cs 의 지도 그림 색과 같은 계열을 유지한다.</summary>
        public static Color RegionColor(CastleRegion region)
        {
            switch (region)
            {
                case CastleRegion.Youzhou: return Rgb(176, 196, 140);
                case CastleRegion.Jizhou: return Rgb(214, 196, 120);
                case CastleRegion.Bingzhou: return Rgb(190, 170, 140);
                case CastleRegion.Qingzhou: return Rgb(150, 196, 160);
                case CastleRegion.Yanzhou: return Rgb(206, 170, 130);
                case CastleRegion.Yuzhou: return Rgb(200, 150, 150);
                case CastleRegion.Xuzhou: return Rgb(160, 180, 200);
                case CastleRegion.Sili: return Rgb(222, 184, 96);
                case CastleRegion.Liangzhou: return Rgb(176, 150, 120);
                case CastleRegion.Yizhou: return Rgb(120, 176, 130);
                case CastleRegion.Jingzhou: return Rgb(150, 170, 210);
                case CastleRegion.Yangzhou: return Rgb(150, 200, 200);
                case CastleRegion.Jiaozhou: return Rgb(190, 150, 190);
                default: return Color.gray;
            }
        }

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        static Sprite Circle() => circleSprite != null ? circleSprite : (circleSprite = MakeRadialSprite(0f, 1f));
        static Sprite Ring() => ringSprite != null ? ringSprite : (ringSprite = MakeRadialSprite(0.78f, 1f));

        /// <summary>반지름 비율 [inner, outer] 사이만 채운 부드러운 원/고리 스프라이트 (에셋 없이 코드로).</summary>
        static Sprite MakeRadialSprite(float inner, float outer)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[N * N];
            float c = (N - 1) / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (N / 2f);
                    float a = Mathf.Clamp01((outer - r) * (N / 2f)) * (inner > 0f ? Mathf.Clamp01((r - inner) * (N / 2f)) : 1f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                action();
            });
        }

        static Button NewButton(string name, Transform parent, string label, Vector2 size, int fontSize)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            rt.gameObject.AddComponent<LayoutElement>();
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

        /// <summary>왼쪽 위 기준(앵커/피벗 모두 왼쪽 위)으로 위치와 크기를 정한다. y 는 아래로 갈수록 음수.</summary>
        static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void TopRight(RectTransform rt, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = pos;
        }
    }
}
