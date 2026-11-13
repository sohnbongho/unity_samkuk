using System.Collections.Generic;
using Messages;

namespace Samkuk.Coop
{
    /// <summary>방 구성원 한 명 (서버의 RoomMember 를 그대로 옮긴 값).</summary>
    public struct RoomMemberInfo
    {
        public int Slot;
        public string UserId;
        public string DisplayName;
        public string HeroId;
        public bool Ready;
        /// <summary>현재 판에 참여 중 (전투 중에 들어온 사람은 false: 다음 판부터).</summary>
        public bool InBattle;
    }

    /// <summary>
    /// 협동 방의 순수 상태: 서버가 보내는 RoomInfo(방 생성/참가 응답, RoomStateNoti)를 그대로 담고
    /// "내가 방장인가, 시작할 수 있는가" 같은 판단만 한다. 방 상태의 원본은 서버이며 클라이언트는 바꾸지 않는다.
    /// </summary>
    public sealed class RoomModel
    {
        public string MyUserId { get; }
        public string Code { get; private set; } = "";
        public int HostSlot { get; private set; } = -1;
        public string StageId { get; private set; } = "";
        public string CastleId { get; private set; } = "";
        /// <summary>맵 위치(성) id. 같은 월드로 들어온 사람끼리 한 방.</summary>
        public string WorldId { get; private set; } = "";
        /// <summary>이 방이 전투 진행 중인가 (뒤늦게 들어온 사람은 대기).</summary>
        public bool InBattle { get; private set; }
        public readonly List<RoomMemberInfo> Members = new List<RoomMemberInfo>();

        public RoomModel(string myUserId)
        {
            MyUserId = myUserId ?? "";
        }

        public bool InRoom => Code.Length > 0;

        /// <summary>내 자리(0..3). 방에 없으면 -1.</summary>
        public int MySlot
        {
            get
            {
                foreach (var m in Members)
                    if (m.UserId == MyUserId) return m.Slot;
                return -1;
            }
        }

        public bool IsHost => InRoom && MySlot >= 0 && MySlot == HostSlot;
        public bool MyReady => TryGetMe(out var me) && me.Ready;
        public bool MyInBattle => TryGetMe(out var me) && me.InBattle;
        public string MyHeroId => TryGetMe(out var me) ? me.HeroId : "";

        public bool TryGetMe(out RoomMemberInfo me)
        {
            foreach (var m in Members)
                if (m.UserId == MyUserId) { me = m; return true; }
            me = default;
            return false;
        }

        public bool TryGetMember(int slot, out RoomMemberInfo member)
        {
            foreach (var m in Members)
                if (m.Slot == slot) { member = m; return true; }
            member = default;
            return false;
        }

        /// <summary>방장을 뺀 전원이 준비했는가 (방장은 준비 없이 시작한다는 서버 규칙).</summary>
        public bool OthersReady
        {
            get
            {
                foreach (var m in Members)
                    if (m.Slot != HostSlot && !m.Ready) return false;
                return true;
            }
        }

        /// <summary>시작 버튼을 누를 수 있는가. 아니면 <see cref="StartBlockReason"/>.</summary>
        public bool CanStart => StartBlockReason.Length == 0;

        public string StartBlockReason
        {
            get
            {
                if (!InRoom) return "방에 있지 않습니다";
                if (InBattle) return "전투가 진행 중입니다";
                if (!IsHost) return "방장만 시작할 수 있습니다";
                if (!OthersReady) return "아직 준비하지 않은 사람이 있습니다";
                if (StageId.Length == 0) return "스테이지가 정해지지 않았습니다";
                return "";
            }
        }

        /// <summary>서버가 보낸 방 상태로 통째로 바꾼다. null 이면 방을 나간 것으로 본다.</summary>
        public void Apply(RoomInfo info)
        {
            if (info == null) { Clear(); return; }
            Code = info.RoomCode ?? "";
            HostSlot = info.HostSlot;
            StageId = info.StageId ?? "";
            CastleId = info.CastleId ?? "";
            WorldId = info.WorldId ?? "";
            InBattle = info.InBattle;
            Members.Clear();
            foreach (var m in info.Members)
            {
                Members.Add(new RoomMemberInfo
                {
                    Slot = m.Slot, UserId = m.UserId ?? "", DisplayName = m.DisplayName ?? "", HeroId = m.HeroId ?? "", Ready = m.Ready, InBattle = m.InBattle,
                });
            }
            Members.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }

        public void Clear()
        {
            Code = "";
            HostSlot = -1;
            StageId = "";
            CastleId = "";
            WorldId = "";
            InBattle = false;
            Members.Clear();
        }

        /// <summary>입력한 초대 코드를 서버 형식으로: 앞뒤 공백 제거, 대문자.</summary>
        public static string NormalizeCode(string code) => (code ?? "").Trim().ToUpperInvariant();

        /// <summary>6자리 영문 대문자/숫자인가 (NormalizeCode 뒤에 검사).</summary>
        public static bool IsValidCode(string code)
        {
            if (code == null || code.Length != CoopSettings.CodeLength) return false;
            foreach (char c in code)
                if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }

        /// <summary>RoomCreateResponse.error_code: 1 이미 방에 있음, 2 서버 오류, 3 장수 없음.</summary>
        public static string CreateErrorText(int code)
        {
            switch (code)
            {
                case 1: return "이미 방에 들어가 있습니다";
                case 2: return "서버 오류로 방을 만들지 못했습니다";
                case 3: return "서버가 모르는 장수입니다 (서버 데이터를 다시 내보내세요)";
                default: return $"방을 만들지 못했습니다 (코드 {code})";
            }
        }

        /// <summary>RoomEnterResponse.error_code: 2 정원, 4 이미 방에 있음, 5 장수 없음, 6 서버 오류.</summary>
        public static string EnterErrorText(int code)
        {
            switch (code)
            {
                case 2: return "그 성은 가득 찼습니다 (최대 4명). 다른 성으로 들어가세요";
                case 4: return "이미 방에 들어가 있습니다";
                case 5: return "서버가 모르는 장수입니다 (서버 데이터를 다시 내보내세요)";
                case 6: return "서버 오류로 들어가지 못했습니다";
                default: return $"들어가지 못했습니다 (코드 {code})";
            }
        }

        /// <summary>RoomJoinResponse.error_code: 1 방 없음, 2 정원, 3 진행 중, 4 이미 방에 있음, 5 장수 없음.</summary>
        public static string JoinErrorText(int code)
        {
            switch (code)
            {
                case 1: return "그 초대 코드의 방이 없습니다";
                case 2: return "방이 가득 찼습니다 (최대 4명)";
                case 3: return "이미 전투가 진행 중인 방입니다";
                case 4: return "이미 방에 들어가 있습니다";
                case 5: return "서버가 모르는 장수입니다 (서버 데이터를 다시 내보내세요)";
                default: return $"참가하지 못했습니다 (코드 {code})";
            }
        }

        /// <summary>RoomStartResponse.error_code: 1 방장 아님, 2 준비 안 된 인원, 3 스테이지 없음, 4 진행 중.</summary>
        public static string StartErrorText(int code)
        {
            switch (code)
            {
                case 1: return "방장만 시작할 수 있습니다";
                case 2: return "아직 준비하지 않은 사람이 있습니다";
                case 3: return "스테이지가 정해지지 않았습니다";
                case 4: return "이미 전투가 진행 중입니다";
                default: return $"시작하지 못했습니다 (코드 {code})";
            }
        }
    }
}
