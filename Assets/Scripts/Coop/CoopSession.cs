using Messages;
using Samkuk.Net;

namespace Samkuk.Coop
{
    /// <summary>
    /// 씬을 넘어 살아 있는 협동 흐름 하나. NetClient(DontDestroyOnLoad)에 붙어 방/전투 메시지와 틱을 받아 <see cref="CoopFlow"/> 에 넘기고,
    /// 접속이 끊기면 방 상태를 지운다. 타이틀의 방 창(RoomPanel)과 전투 씬(15-4)이 같은 Flow 를 본다.
    /// </summary>
    public static class CoopSession
    {
        static NetClient bound;

        public static CoopFlow Flow { get; private set; }

        /// <summary>전투 씬이 읽을 전투 시작 알림. 없으면 협동 전투가 아니다.</summary>
        public static BattleStartNoti BattleStart => Flow?.BattleStart;
        public static bool InBattle => Flow != null && Flow.Phase == CoopPhase.Battle;

        /// <summary>이 연결에 붙은 Flow 를 돌려준다. 다른 연결/다른 아이디면 새로 만든다.</summary>
        public static CoopFlow GetOrCreate(NetClient net)
        {
            if (net == null) return null;
            if (Flow != null && bound == net && Flow.UserId == net.UserId) return Flow;
            Detach();
            Flow = new CoopFlow(net.Send, net.UserId);
            bound = net;
            net.MessageReceived += OnMessage;
            net.Ticked += OnTick;
            net.PhaseChanged += OnPhase;
            return Flow;
        }

        public static void Detach()
        {
            if (bound != null)
            {
                bound.MessageReceived -= OnMessage;
                bound.Ticked -= OnTick;
                bound.PhaseChanged -= OnPhase;
            }
            bound = null;
            Flow = null;
        }

        static void OnMessage(MessageWrapper message) => Flow?.Handle(message);
        static void OnTick(float dt) => Flow?.Tick(dt);

        static void OnPhase(NetPhase phase)
        {
            // 접속이 끊기면 서버 쪽 방도 사라진다 (서버는 끊긴 사람을 방에서 뺀다)
            if (phase != NetPhase.Online && Flow != null && (Flow.InRoom || Flow.IsBusy))
                Flow.Reset(phase == NetPhase.Failed ? "서버와의 연결이 끊겨 방에서 나왔습니다" : "접속을 끊어 방에서 나왔습니다");
        }
    }
}
