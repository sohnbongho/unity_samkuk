using System.Collections.Generic;
using Samkuk.Audio;
using Samkuk.Coop;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 협동 방 창(타이틀 [협동] 버튼): 장수 선택 → [방 만들기] 또는 초대 코드 입력 → [참가] → 대기방(구성원 4칸, [준비]/[시작], [나가기]).
    /// 로그인 창처럼 코드로 만들어 캔버스 맨 위에 덮는다. 상태의 원본은 <see cref="CoopFlow"/>(서버가 보내는 방 상태)이고 이 창은 그것을 그리기만 한다.
    /// 전투 시작 알림이 오면 문구만 바꾼다(전투 씬 연결은 Step 15-4).
    /// </summary>
    public class RoomPanel : MonoBehaviour
    {
        public const string RootName = "RoomPanel";
        static readonly Color HeroIdle = new Color(0.25f, 0.22f, 0.2f);
        static readonly Color HeroPicked = new Color(0.6f, 0.45f, 0.15f);

        CoopFlow flow;
        Text codeText, status;
        readonly Text[] memberTexts = new Text[CoopSettings.MaxMembers];
        InputField codeField;
        Button createButton, joinButton, readyButton, startButton, leaveButton, closeButton;
        readonly List<Button> heroButtons = new List<Button>();
        readonly List<HeroData> heroes = new List<HeroData>();
        GameObject lobbyGroup, roomGroup;
        string selectedHeroId = "";

        public CoopFlow Flow => flow;
        public string Status => status != null ? status.text : "";
        public string SelectedHeroId => selectedHeroId;
        public string Code { get => codeField != null ? codeField.text : ""; set { if (codeField != null) codeField.text = value; } }
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

            codeText = UiBuild.Text("Code", box.transform, font, 30, TextAnchor.MiddleCenter, "");
            UiBuild.Place(codeText.rectTransform, 0f, 250f, 900f, 44f);
            codeText.color = new Color(1f, 0.9f, 0.6f);

            // 장수 선택 줄 (방 밖에서는 만들기/참가에 쓰고, 방 안에서는 바로 서버에 바꿔 달라고 한다)
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

            // 방 밖: 만들기 / 코드 참가
            lobbyGroup = new GameObject("Lobby", typeof(RectTransform));
            lobbyGroup.transform.SetParent(box.transform, false);
            UiBuild.Place((RectTransform)lobbyGroup.transform, 0f, 0f, 1000f, 720f);
            createButton = UiBuild.Button("CreateButton", lobbyGroup.transform, font, "방 만들기", 0f, 50f, new Vector2(400f, 76f), new Color(0.55f, 0.2f, 0.18f));
            codeField = UiBuild.Input("CodeField", lobbyGroup.transform, font, "초대 코드 6자리", -100f, -50f, 380f, 66f, false);
            codeField.characterLimit = CoopSettings.CodeLength;
            joinButton = UiBuild.Button("JoinButton", lobbyGroup.transform, font, "참가", 160f, -50f, new Vector2(140f, 66f), new Color(0.2f, 0.35f, 0.5f));
            createButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); CreateRoom(); });
            joinButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); JoinRoom(); });

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

        public void CreateRoom() => flow?.Create(selectedHeroId);

        public void JoinRoom() => flow?.Join(Code, selectedHeroId);

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

        /// <summary>창을 닫는다. 방에 있으면 나간다 (전투 시작 뒤에는 닫지 않는다: 15-4 가 전투 씬으로 넘긴다).</summary>
        public void Close()
        {
            if (flow != null && flow.Phase == CoopPhase.Battle) return;
            if (flow != null && flow.InRoom) flow.Leave();
            Destroy(gameObject);
        }

        // ───────────────────────── 그리기 ─────────────────────────

        void Refresh()
        {
            if (flow == null) return;
            var room = flow.Room;
            bool inRoom = room.InRoom;
            bool busy = flow.IsBusy;
            bool battle = flow.Phase == CoopPhase.Battle;

            lobbyGroup.SetActive(!inRoom);
            roomGroup.SetActive(inRoom);
            codeText.text = inRoom ? $"초대 코드  {room.Code}   (친구에게 알려 주세요)" : "방을 만들거나 친구의 초대 코드로 참가하세요";

            string myHero = inRoom ? room.MyHeroId : selectedHeroId;
            if (inRoom && myHero.Length > 0) selectedHeroId = myHero;
            for (int i = 0; i < heroButtons.Count; i++)
            {
                heroButtons[i].image.color = heroes[i].name == selectedHeroId ? HeroPicked : HeroIdle;
                heroButtons[i].interactable = !busy && !battle;
            }

            createButton.interactable = !busy;
            joinButton.interactable = !busy;
            codeField.interactable = !busy;

            if (inRoom)
            {
                for (int slot = 0; slot < memberTexts.Length; slot++)
                    memberTexts[slot].text = room.TryGetMember(slot, out var m) ? MemberLine(room, m) : $"{slot + 1}.  (빈 자리)";
                bool host = room.IsHost;
                readyButton.gameObject.SetActive(!host);
                startButton.gameObject.SetActive(host);
                UiBuild.SetLabel(readyButton, room.MyReady ? "준비 취소" : "준비");
                readyButton.interactable = !busy && !battle;
                startButton.interactable = !busy && !battle && room.CanStart;
                UiBuild.SetLabel(startButton, room.CanStart || busy ? "시작" : "시작 (" + room.StartBlockReason + ")");
                leaveButton.interactable = !battle;
            }
            closeButton.interactable = !battle;
            SetStatus(flow.Message);
        }

        string MemberLine(RoomModel room, RoomMemberInfo m)
        {
            string name = m.DisplayName.Length > 0 ? m.DisplayName : m.UserId;
            if (m.UserId == room.MyUserId) name += " (나)";
            var hero = HeroName(m.HeroId);
            string state = m.Slot == room.HostSlot ? "방장" : (m.Ready ? "준비 완료" : "대기 중");
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
