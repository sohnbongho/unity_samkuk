using System;
using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Coop;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 협동 방 창(타이틀 [협동] 버튼): 장수와 성(월드)을 고르고 [입장] → 같은 성에 들어온 사람들이 한 방에 모인다(RoomEnterRequest, 첫 입장자가 방장)
    /// → 대기방(구성원 4칸, [준비]/[시작], [나가기]). 초대 코드 방식(RoomCreate/RoomJoin)은 서버에 남아 있지만 UI 는 쓰지 않는다(사용자 결정: 코드 입력이 불편).
    /// 로그인 창처럼 코드로 만들어 캔버스 맨 위에 덮는다. 상태의 원본은 <see cref="CoopFlow"/>(서버가 보내는 방 상태)이고 이 창은 그것을 그리기만 한다.
    /// 전투 시작 알림(BattleStartNoti)이 오면 전투 씬을 연다: 거기서 CoopBattleDriver 가 서버 스냅샷으로 전투를 그린다(Step 15-4).
    /// 전투 중인 월드에 들어오면 대기 멤버로 머물다 다음 판부터 참여한다.
    /// </summary>
    public class RoomPanel : MonoBehaviour
    {
        public const string RootName = "RoomPanel";
        /// <summary>전투 씬을 여는 방법 (테스트가 null 로 막는다).</summary>
        public static Action<string> SceneLoader = name => SceneManager.LoadScene(name);
        static readonly Color HeroIdle = new Color(0.25f, 0.22f, 0.2f);
        static readonly Color HeroPicked = new Color(0.6f, 0.45f, 0.15f);

        CoopFlow flow;
        Text codeText, status, castleText;
        readonly Text[] memberTexts = new Text[CoopSettings.MaxMembers];
        Button enterButton, prevCastleButton, nextCastleButton, readyButton, startButton, leaveButton, closeButton;
        readonly List<Button> heroButtons = new List<Button>();
        readonly List<HeroData> heroes = new List<HeroData>();
        readonly List<CastleData> castles = new List<CastleData>();
        GameObject lobbyGroup, roomGroup;
        string selectedHeroId = "";
        int castleIndex = -1;
        string worldId = "";
        bool loadingBattle;

        public CoopFlow Flow => flow;
        public string Status => status != null ? status.text : "";
        public string SelectedHeroId => selectedHeroId;
        /// <summary>들어갈 월드(성 에셋 이름, 예: Castle_Luoyang). 성 목록이 없는 테스트에서는 직접 넣는다.</summary>
        public string WorldId { get => worldId; set { worldId = value ?? ""; castleIndex = IndexOfCastle(worldId); Refresh(); } }
        public bool IsRoomViewVisible => roomGroup != null && roomGroup.activeSelf;

        /// <summary>타이틀에서 연다. 이미 열려 있으면 그것. 접속돼 있지 않으면 열지 않고 null.</summary>
        public static RoomPanel Open(Transform parent)
        {
            var existing = parent.Find(RootName);
            if (existing != null) return existing.GetComponent<RoomPanel>();
            var net = NetClient.Instance;
            if (net == null || !net.IsOnline) return null;
            return Create(parent, CoopSession.GetOrCreate(net));
        }

        /// <summary>창을 만든다. flow 는 테스트가 가짜 전송으로 만든 것을 넣을 수 있다.</summary>
        public static RoomPanel Create(Transform parent, CoopFlow flow)
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var blocker = go.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.6f);

            var panel = go.AddComponent<RoomPanel>();
            panel.flow = flow;
            panel.Build();
            return panel;
        }

        void Build()
        {
            var font = UiFont.Get();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(transform, false);
            UiBuild.Place((RectTransform)box.transform, 0f, 0f, 1000f, 720f);
            var boxImg = box.GetComponent<Image>();
            boxImg.color = new Color(0.12f, 0.09f, 0.07f, 0.96f);
            UiSkin.StylePanel(boxImg);

            var title = UiBuild.Text("Title", box.transform, font, 44, TextAnchor.MiddleCenter, "협동 — 친구와 함께");
            UiBuild.Place(title.rectTransform, 0f, 310f, 900f, 60f);
            UiSkin.StyleTitle(title);

            codeText = UiBuild.Text("World", box.transform, font, 30, TextAnchor.MiddleCenter, "");
            UiBuild.Place(codeText.rectTransform, 0f, 250f, 900f, 44f);
            codeText.color = new Color(1f, 0.9f, 0.6f);

            // 장수 선택 줄 (방 밖에서는 입장에 쓰고, 방 안에서는 바로 서버에 바꿔 달라고 한다)
            var heroLabel = UiBuild.Text("HeroLabel", box.transform, font, 26, TextAnchor.MiddleCenter, "장수");
            UiBuild.Place(heroLabel.rectTransform, 0f, 195f, 900f, 36f);
            var catalog = HeroCatalog.Load();
            if (catalog != null)
                foreach (var h in catalog.heroes) if (h != null) heroes.Add(h);
            float heroW = 170f, gap = 10f;
            float x0 = -(heroes.Count - 1) * (heroW + gap) * 0.5f;
            for (int i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                var b = UiBuild.Button("Hero" + i, box.transform, font, hero.displayName, x0 + i * (heroW + gap), 140f, new Vector2(heroW, 60f), HeroIdle);
                b.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); SelectHero(hero.name); });
                heroButtons.Add(b);
            }

            // 방 밖: 성(월드) 고르기 + 입장
            lobbyGroup = new GameObject("Lobby", typeof(RectTransform));
            lobbyGroup.transform.SetParent(box.transform, false);
            UiBuild.Place((RectTransform)lobbyGroup.transform, 0f, 0f, 1000f, 720f);
            var castleLabel = UiBuild.Text("CastleLabel", lobbyGroup.transform, font, 26, TextAnchor.MiddleCenter, "들어갈 성 (같은 성에 들어온 사람끼리 한 방)");
            UiBuild.Place(castleLabel.rectTransform, 0f, 70f, 900f, 36f);
            prevCastleButton = UiBuild.Button("PrevCastle", lobbyGroup.transform, font, "<", -260f, 15f, new Vector2(70f, 60f), new Color(0.25f, 0.25f, 0.3f));
            nextCastleButton = UiBuild.Button("NextCastle", lobbyGroup.transform, font, ">", 260f, 15f, new Vector2(70f, 60f), new Color(0.25f, 0.25f, 0.3f));
            castleText = UiBuild.Text("CastleName", lobbyGroup.transform, font, 34, TextAnchor.MiddleCenter, "");
            UiBuild.Place(castleText.rectTransform, 0f, 15f, 420f, 60f);
            enterButton = UiBuild.Button("EnterButton", lobbyGroup.transform, font, "입장", 0f, -80f, new Vector2(400f, 76f), new Color(0.55f, 0.2f, 0.18f));
            prevCastleButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); StepCastle(-1); });
            nextCastleButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); StepCastle(1); });
            enterButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); EnterRoom(); });

            // 방 안: 구성원 4칸 + 준비/시작/나가기
            roomGroup = new GameObject("Room", typeof(RectTransform));
            roomGroup.transform.SetParent(box.transform, false);
            UiBuild.Place((RectTransform)roomGroup.transform, 0f, 0f, 1000f, 720f);
            for (int i = 0; i < memberTexts.Length; i++)
            {
                var slotBg = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image));
                slotBg.transform.SetParent(roomGroup.transform, false);
                UiBuild.Place((RectTransform)slotBg.transform, 0f, 80f - i * 50f, 760f, 46f);
                var img = slotBg.GetComponent<Image>();
                img.color = new Color(0.05f, 0.04f, 0.03f, 0.8f);
                UiSkin.StyleSlot(img);
                memberTexts[i] = UiBuild.Text("Text", slotBg.transform, font, 26, TextAnchor.MiddleLeft, "");
                UiBuild.Stretch(memberTexts[i].rectTransform, 18f);
            }
            readyButton = UiBuild.Button("ReadyButton", roomGroup.transform, font, "준비", -210f, -170f, new Vector2(300f, 70f), new Color(0.2f, 0.45f, 0.25f));
            startButton = UiBuild.Button("StartButton", roomGroup.transform, font, "시작", 210f, -170f, new Vector2(300f, 70f), new Color(0.55f, 0.2f, 0.18f));
            leaveButton = UiBuild.Button("LeaveButton", roomGroup.transform, font, "나가기", 0f, -250f, new Vector2(240f, 56f), new Color(0.25f, 0.25f, 0.3f));
            readyButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); ToggleReady(); });
            startButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); StartBattle(); });
            leaveButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); LeaveRoom(); });

            status = UiBuild.Text("Status", box.transform, font, 24, TextAnchor.MiddleCenter, "");
            UiBuild.Place(status.rectTransform, 0f, -230f, 900f, 60f);
            status.color = new Color(1f, 0.85f, 0.6f);

            closeButton = UiBuild.Button("CloseButton", box.transform, font, "닫기", 0f, -310f, new Vector2(240f, 56f), new Color(0.25f, 0.25f, 0.3f));
            closeButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); Close(); });

            // 처음 고른 장수: 마지막으로 전투한 장수, 없으면 첫 장수
            var initial = GameSession.SelectedHero != null ? GameSession.SelectedHero.name : (heroes.Count > 0 ? heroes[0].name : "");
            if (flow != null && flow.Room.InRoom && flow.Room.MyHeroId.Length > 0) initial = flow.Room.MyHeroId;
            selectedHeroId = initial;

            // 처음 고른 성: 내 성(내정의 시작 성), 없으면 첫 성
            var castleCatalog = CastleCatalog.Load();
            if (castleCatalog != null)
                foreach (var c in castleCatalog.castles) if (c != null) castles.Add(c);
            var home = castleCatalog != null ? castleCatalog.Find(SaveSystem.Current.homeCastleId) : null;
            castleIndex = home != null ? castles.IndexOf(home) : (castles.Count > 0 ? 0 : -1);
            worldId = castleIndex >= 0 ? castles[castleIndex].name : "";
            if (flow != null && flow.Room.InRoom && flow.Room.WorldId.Length > 0) WorldId = flow.Room.WorldId;

            if (flow != null) flow.Changed += Refresh;
            Refresh();
            if (heroes.Count == 0) SetStatus("장수 카탈로그가 없습니다 (Samkuk > Run All Setup)");
        }

        // ───────────────────────── 조작 ─────────────────────────

        public void SelectHero(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return;
            selectedHeroId = heroId;
            if (flow != null && flow.Phase == CoopPhase.InRoom && flow.Room.MyHeroId != heroId) flow.SetHero(heroId);
            Refresh();
        }

        /// <summary>성 목록에서 앞/뒤로 (끝에서는 반대쪽으로 돈다).</summary>
        public void StepCastle(int delta)
        {
            if (castles.Count == 0) return;
            castleIndex = ((castleIndex < 0 ? 0 : castleIndex) + delta % castles.Count + castles.Count) % castles.Count;
            worldId = castles[castleIndex].name;
            Refresh();
        }

        public void EnterRoom() => flow?.Enter(worldId, selectedHeroId);

        public void ToggleReady()
        {
            if (flow == null) return;
            flow.SetReady(!flow.Room.MyReady);
        }

        public void StartBattle() => flow?.Start();

        public void LeaveRoom()
        {
            flow?.Leave();
            Refresh();
        }

        /// <summary>창을 닫는다. 방에 있으면 나간다 (전투 시작 뒤에는 닫지 않는다: 전투 씬으로 넘어간다).</summary>
        public void Close()
        {
            if (flow != null && flow.Phase == CoopPhase.Battle) return;
            if (flow != null && flow.InRoom) flow.Leave();
            Destroy(gameObject);
        }

        // ───────────────────────── 그리기 ─────────────────────────

        int IndexOfCastle(string assetName)
        {
            for (int i = 0; i < castles.Count; i++) if (castles[i].name == assetName) return i;
            return -1;
        }

        string CastleLabel(string assetName)
        {
            int i = IndexOfCastle(assetName);
            if (i >= 0) return $"{castles[i].displayName} ({castles[i].hanja})";
            return assetName.Length > 0 ? assetName : "성 없음";
        }

        void Refresh()
        {
            if (flow == null || lobbyGroup == null) return;
            var room = flow.Room;
            bool inRoom = room.InRoom;
            bool busy = flow.IsBusy;
            bool battle = flow.Phase == CoopPhase.Battle;
            bool waitingNextRound = inRoom && room.InBattle && !room.MyInBattle;

            lobbyGroup.SetActive(!inRoom);
            roomGroup.SetActive(inRoom);
            codeText.text = inRoom
                ? (waitingNextRound ? $"{CastleLabel(room.WorldId)} — 전투 진행 중, 다음 판을 기다리는 중" : $"{CastleLabel(room.WorldId)} 에 모인 사람들")
                : "장수와 성을 고르고 [입장]: 같은 성에 들어온 사람끼리 함께 싸웁니다";
            if (castleText != null) castleText.text = CastleLabel(worldId);

            string myHero = inRoom ? room.MyHeroId : selectedHeroId;
            if (inRoom && myHero.Length > 0) selectedHeroId = myHero;
            for (int i = 0; i < heroButtons.Count; i++)
            {
                heroButtons[i].image.color = heroes[i].name == selectedHeroId ? HeroPicked : HeroIdle;
                heroButtons[i].interactable = !busy && !battle;
            }

            enterButton.interactable = !busy;
            prevCastleButton.interactable = !busy && castles.Count > 1;
            nextCastleButton.interactable = !busy && castles.Count > 1;

            if (inRoom)
            {
                for (int slot = 0; slot < memberTexts.Length; slot++)
                    memberTexts[slot].text = room.TryGetMember(slot, out var m) ? MemberLine(room, m) : $"{slot + 1}.  (빈 자리)";
                bool host = room.IsHost;
                readyButton.gameObject.SetActive(!host);
                startButton.gameObject.SetActive(host);
                UiBuild.SetLabel(readyButton, room.MyReady ? "준비 취소" : "준비");
                readyButton.interactable = !busy && !battle && !room.InBattle;
                startButton.interactable = !busy && !battle && room.CanStart;
                UiBuild.SetLabel(startButton, room.CanStart || busy ? "시작" : "시작 (" + room.StartBlockReason + ")");
                leaveButton.interactable = !battle;
            }
            closeButton.interactable = !battle;
            SetStatus(flow.Message);

            if (battle && !loadingBattle && flow.BattleStart != null)
            {
                loadingBattle = true;
                SceneLoader?.Invoke(GameManager.BattleSceneName);
            }
        }

        string MemberLine(RoomModel room, RoomMemberInfo m)
        {
            string name = m.DisplayName.Length > 0 ? m.DisplayName : m.UserId;
            if (m.UserId == room.MyUserId) name += " (나)";
            var hero = HeroName(m.HeroId);
            string state = room.InBattle ? (m.InBattle ? "전투 중" : "다음 판 대기")
                : m.Slot == room.HostSlot ? "방장" : (m.Ready ? "준비 완료" : "대기 중");
            return $"{m.Slot + 1}.  {name}   {hero}   [{state}]";
        }

        string HeroName(string heroId)
        {
            foreach (var h in heroes)
                if (h.name == heroId) return h.displayName;
            return heroId.Length > 0 ? heroId : "장수 미정";
        }

        void SetStatus(string text)
        {
            if (status != null) status.text = text ?? "";
        }

        void OnDestroy()
        {
            if (flow != null) flow.Changed -= Refresh;
            flow = null;
        }
    }
}
