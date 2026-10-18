using System;
using Messages;
using Samkuk.Net;

namespace Samkuk.Coop
{
    public enum CoopPhase
    {
        None,     // 방 없음 (만들거나 참가할 수 있음)
        Creating, // RoomCreateRequest 보내고 응답 대기 (초대 코드 방식, UI 는 안 씀)
        Joining,  // RoomJoinRequest(초대 코드) 또는 RoomEnterRequest(월드) 보내고 응답 대기
        InRoom,   // 대기방: 준비/장수 변경/시작
        Starting, // 방장이 RoomStartRequest 보내고 응답·BattleStartNoti 대기
        Battle,   // BattleStartNoti 를 받음 (전투 씬은 15-4 가 이어받는다)
    }

    /// <summary>
    /// 협동 방 흐름의 순수 상태 기계: 월드(성) 입장(없으면 서버가 만들고 첫 입장자가 방장) → 대기방(준비, 장수, 스테이지) → 시작 → 전투 시작 알림 → 전투 끝나면 대기방으로.
    /// 초대 코드 방식(Create/Join)도 남아 있지만 UI 는 월드 입장(<see cref="Enter"/>)만 쓴다.
    /// 메시지는 <c>send</c> 로 보내고 받은 것은 <see cref="Handle"/> 로 넣으며 시간은 <see cref="Tick"/> 으로 받으므로 서버 없이 테스트한다.
    /// 방 상태의 원본은 서버(RoomInfo)이고 여기서는 요청을 보낸 뒤 서버가 보내 주는 상태를 그대로 담는다.
    /// </summary>
    public sealed class CoopFlow
    {
        readonly Func<MessageWrapper, bool> send;
        float waited;

        public RoomModel Room { get; }
        public CoopPhase Phase { get; private set; } = CoopPhase.None;
        /// <summary>화면에 보여 줄 현재 상태/실패 이유.</summary>
        public string Message { get; private set; } = "";
        /// <summary>마지막으로 받은 전투 시작 알림 (전투 씬이 읽는다). 방을 나가면 지운다.</summary>
        public BattleStartNoti BattleStart { get; private set; }
        public BattleEndNoti BattleEnd { get; private set; }
        public string UserId => Room.MyUserId;
        public float Timeout = NetSettings.ResponseTimeout;
        /// <summary>방을 만든 뒤 서버의 스테이지가 비어 있으면 방장이 자동으로 정하는 스테이지.</summary>
        public string DefaultStageId = CoopSettings.DefaultStageId;

        /// <summary>단계/방 상태/문구가 바뀔 때.</summary>
        public event Action Changed;

        public bool InRoom => Room.InRoom;
        public bool IsBusy => Phase == CoopPhase.Creating || Phase == CoopPhase.Joining || Phase == CoopPhase.Starting;

        public CoopFlow(Func<MessageWrapper, bool> send, string myUserId)
        {
            this.send = send ?? throw new ArgumentNullException(nameof(send));
            Room = new RoomModel(myUserId);
        }

        // ───────────────────────── 요청 ─────────────────────────

        public bool Create(string heroId)
        {
            if (!CanRequest(CoopPhase.None, "이미 방에 있습니다")) return false;
            if (string.IsNullOrEmpty(heroId)) { Set(Phase, "장수를 고르세요"); return false; }
            if (!Post(new MessageWrapper { RoomCreateRequest = new RoomCreateRequest { HeroId = heroId } })) return false;
            Set(CoopPhase.Creating, "방을 만드는 중...");
            return true;
        }

        public bool Join(string code, string heroId)
        {
            if (!CanRequest(CoopPhase.None, "이미 방에 있습니다")) return false;
            if (string.IsNullOrEmpty(heroId)) { Set(Phase, "장수를 고르세요"); return false; }
            code = RoomModel.NormalizeCode(code);
            if (!RoomModel.IsValidCode(code)) { Set(Phase, $"초대 코드는 영문/숫자 {CoopSettings.CodeLength}자리입니다"); return false; }
            if (!Post(new MessageWrapper { RoomJoinRequest = new RoomJoinRequest { RoomCode = code, HeroId = heroId } })) return false;
            Set(CoopPhase.Joining, $"방 {code} 에 참가하는 중...");
            return true;
        }

        /// <summary>월드(성)로 들어간다: 그 성의 방이 없으면 서버가 만들고(내가 방장), 있으면 참가. 전투 중이면 대기 멤버로.</summary>
        public bool Enter(string worldId, string heroId)
        {
            if (!CanRequest(CoopPhase.None, "이미 방에 있습니다")) return false;
            if (string.IsNullOrEmpty(heroId)) { Set(Phase, "장수를 고르세요"); return false; }
            if (string.IsNullOrEmpty(worldId)) { Set(Phase, "들어갈 성을 고르세요"); return false; }
            if (!Post(new MessageWrapper { RoomEnterRequest = new RoomEnterRequest { WorldId = worldId, HeroId = heroId } })) return false;
            Set(CoopPhase.Joining, "들어가는 중...");
            return true;
        }

        public bool Leave()
        {
            if (!Room.InRoom) return false;
            Post(new MessageWrapper { RoomLeaveRequest = new RoomLeaveRequest() });
            // 응답을 기다리지 않고 바로 나간 것으로 본다 (서버가 끊겨도 화면은 닫혀야 한다)
            Room.Clear();
            BattleStart = null;
            Set(CoopPhase.None, "방을 나왔습니다");
            return true;
        }

        public bool SetReady(bool ready)
        {
            if (!CanRequest(CoopPhase.InRoom, "방에 있지 않습니다")) return false;
            return Post(new MessageWrapper { RoomReadyRequest = new RoomReadyRequest { Ready = ready } });
        }

        public bool SetHero(string heroId)
        {
            if (!CanRequest(CoopPhase.InRoom, "방에 있지 않습니다")) return false;
            if (string.IsNullOrEmpty(heroId)) return false;
            return Post(new MessageWrapper { RoomSetHeroRequest = new RoomSetHeroRequest { HeroId = heroId } });
        }

        public bool SetStage(string stageId, string castleId = "")
        {
            if (!CanRequest(CoopPhase.InRoom, "방에 있지 않습니다")) return false;
            if (!Room.IsHost) { Set(Phase, "방장만 스테이지를 바꿀 수 있습니다"); return false; }
            return Post(new MessageWrapper { RoomSetStageRequest = new RoomSetStageRequest { StageId = stageId ?? "", CastleId = castleId ?? "" } });
        }

        public bool Start()
        {
            if (!CanRequest(CoopPhase.InRoom, "방에 있지 않습니다")) return false;
            if (!Room.CanStart) { Set(Phase, Room.StartBlockReason); return false; }
            if (!Post(new MessageWrapper { RoomStartRequest = new RoomStartRequest() })) return false;
            Set(CoopPhase.Starting, "전투를 시작하는 중...");
            return true;
        }

        /// <summary>연결이 끊기는 등 바깥 사정으로 방이 사라졌을 때.</summary>
        public void Reset(string message)
        {
            Room.Clear();
            BattleStart = null;
            Set(CoopPhase.None, message ?? "");
        }

        // ───────────────────────── 시간/수신 ─────────────────────────

        public void Tick(float dt)
        {
            if (!IsBusy) return;
            waited += dt;
            if (waited <= Timeout) return;
            // 응답이 없으면 요청 전 상태로 되돌린다 (방 안에서 시작을 눌렀으면 방에 남는다)
            Set(Phase == CoopPhase.Starting ? CoopPhase.InRoom : CoopPhase.None, "서버가 응답하지 않습니다");
        }

        /// <summary>서버 메시지를 처리한다. 방/전투 시작 메시지면 true.</summary>
        public bool Handle(MessageWrapper message)
        {
            if (message == null) return false;
            switch (message.PayloadCase)
            {
                case MessageWrapper.PayloadOneofCase.RoomCreateResponse:
                {
                    var r = message.RoomCreateResponse;
                    if (Phase != CoopPhase.Creating) return true;
                    if (!r.Success) { Set(CoopPhase.None, RoomModel.CreateErrorText(r.ErrorCode)); return true; }
                    Room.Apply(r.Room);
                    Set(CoopPhase.InRoom, $"방을 만들었습니다. 초대 코드 {Room.Code}");
                    EnsureStageAndCastle();
                    return true;
                }
                case MessageWrapper.PayloadOneofCase.RoomEnterResponse:
                {
                    var r = message.RoomEnterResponse;
                    if (Phase != CoopPhase.Joining) return true;
                    if (!r.Success) { Set(CoopPhase.None, RoomModel.EnterErrorText(r.ErrorCode)); return true; }
                    Room.Apply(r.Room);
                    Set(CoopPhase.InRoom, Room.InBattle && !Room.MyInBattle ? "전투가 진행 중입니다. 다음 판부터 함께합니다"
                        : Room.IsHost ? "첫 입장: 방장입니다. 사람이 모이면 [시작]" : "들어왔습니다");
                    EnsureStageAndCastle();
                    return true;
                }
                case MessageWrapper.PayloadOneofCase.RoomJoinResponse:
                {
                    var r = message.RoomJoinResponse;
                    if (Phase != CoopPhase.Joining) return true;
                    if (!r.Success) { Set(CoopPhase.None, RoomModel.JoinErrorText(r.ErrorCode)); return true; }
                    Room.Apply(r.Room);
                    Set(CoopPhase.InRoom, $"방 {Room.Code} 에 들어왔습니다");
                    return true;
                }
                case MessageWrapper.PayloadOneofCase.RoomStateNoti:
                    Room.Apply(message.RoomStateNoti.Room);
                    if (!Room.InRoom) Set(CoopPhase.None, "방이 닫혔습니다");
                    else if (Phase == CoopPhase.None) Set(CoopPhase.InRoom, "");
                    else Changed?.Invoke();
                    return true;
                case MessageWrapper.PayloadOneofCase.RoomLeaveResponse:
                    if (Room.InRoom) Reset("방을 나왔습니다");
                    return true;
                case MessageWrapper.PayloadOneofCase.RoomStartResponse:
                {
                    var r = message.RoomStartResponse;
                    if (!r.Success) Set(CoopPhase.InRoom, RoomModel.StartErrorText(r.ErrorCode));
                    // 성공이면 BattleStartNoti 가 곧 온다
                    return true;
                }
                case MessageWrapper.PayloadOneofCase.BattleStartNoti:
                    BattleStart = message.BattleStartNoti;
                    BattleEnd = null;
                    Set(CoopPhase.Battle, "전투 시작!");
                    return true;
                case MessageWrapper.PayloadOneofCase.BattleEndNoti:
                    BattleEnd = message.BattleEndNoti;
                    if (Phase == CoopPhase.Battle) Set(Room.InRoom ? CoopPhase.InRoom : CoopPhase.None, message.BattleEndNoti.Cleared ? "전투 승리" : "전투 종료");
                    return true;
                default:
                    return false;
            }
        }

        // ───────────────────────── 내부 ─────────────────────────

        /// <summary>
        /// 방장이면 스테이지(비어 있을 때만 기본값)와 전투 배경 성(= 들어온 월드)을 서버에 정해 둔다. 서버는 저장만 하고 클라가 전투 배경에 쓴다.
        /// </summary>
        void EnsureStageAndCastle()
        {
            if (!Room.IsHost) return;
            string stage = Room.StageId.Length > 0 ? Room.StageId : DefaultStageId;
            string castle = Room.WorldId.Length > 0 ? Room.WorldId : Room.CastleId;
            if (string.IsNullOrEmpty(stage)) return;
            if (Room.StageId == stage && Room.CastleId == castle) return;
            SetStage(stage, castle);
        }

        bool CanRequest(CoopPhase required, string otherwise)
        {
            if (Phase == required) return true;
            Set(Phase, IsBusy ? "응답을 기다리는 중입니다" : otherwise);
            return false;
        }

        bool Post(MessageWrapper message)
        {
            if (send(message)) return true;
            Set(Phase, "서버에 접속돼 있지 않습니다");
            return false;
        }

        void Set(CoopPhase phase, string message)
        {
            Phase = phase;
            Message = message ?? "";
            waited = 0f;
            Changed?.Invoke();
        }
    }
}
