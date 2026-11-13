using System;
using Google.Protobuf;
using Messages;

namespace Samkuk.Net
{
    public enum NetPhase
    {
        Offline,          // 접속 안 함 (오프라인 플레이)
        ConnectingLogin,  // 로그인 서버 접속 + 첫 인사(ConnectedResponse) 대기
        LoggingIn,        // LoginRequest 보내고 응답 대기
        ConnectingGame,   // 게임 서버 접속 + 첫 인사 대기
        Authenticating,   // GameConnectRequest 보내고 응답 대기
        Online,           // 인증 완료, 연결 유지 중
        Failed,           // 실패/끊김 (Message 에 이유)
    }

    /// <summary>
    /// 로그인 흐름의 순수 상태 기계: 로그인 서버(접속 → 인사 → LoginRequest → 토큰) → 게임 서버(접속 → 인사 → GameConnectRequest)
    /// → 접속 완료 뒤 3초마다 KeepAlive. 연결은 INetLink 로만 다루고 시간은 Tick(dt) 로 받으므로 서버 없이 테스트한다.
    /// 서버는 항상 ConnectedResponse 를 첫 프레임으로 보내므로 그것을 받은 뒤에 요청을 보낸다(DummyClient 와 같은 순서).
    /// </summary>
    public sealed class LoginFlow
    {
        readonly Func<string, int, INetLink> connect;
        INetLink link;
        string host;
        int gamePort;
        byte[] passwordHash;
        float phaseTime, sinceKeepAlive;

        public NetPhase Phase { get; private set; } = NetPhase.Offline;
        /// <summary>화면에 보여 줄 현재 상태/실패 이유.</summary>
        public string Message { get; private set; } = "";
        public string UserId { get; private set; } = "";
        public string Token { get; private set; } = "";
        /// <summary>실패했을 때 서버 오류 코드(연결 문제면 -1).</summary>
        public int ErrorCode { get; private set; }
        public int KeepAlivesSent { get; private set; }
        public float KeepAliveInterval = NetSettings.KeepAliveInterval;
        public float Timeout = NetSettings.ResponseTimeout;

        /// <summary>접속 완료 뒤 로그인 흐름이 쓰지 않는 메시지(방/전투)가 오면 알린다. KeepAliveNoti 도 여기로 온다.</summary>
        public event Action<MessageWrapper> Unhandled;

        public bool IsOnline => Phase == NetPhase.Online;
        public bool IsBusy => Phase == NetPhase.ConnectingLogin || Phase == NetPhase.LoggingIn
                           || Phase == NetPhase.ConnectingGame || Phase == NetPhase.Authenticating;

        public LoginFlow(Func<string, int, INetLink> connect)
        {
            this.connect = connect ?? throw new ArgumentNullException(nameof(connect));
        }

        public void Begin(string userId, byte[] passwordHash, string host, int loginPort, int gamePort)
        {
            Disconnect();
            UserId = userId ?? "";
            this.passwordHash = passwordHash ?? Array.Empty<byte>();
            this.host = host;
            this.gamePort = gamePort;
            Token = "";
            ErrorCode = 0;
            KeepAlivesSent = 0;
            Enter(NetPhase.ConnectingLogin, "로그인 서버에 연결하는 중...");
            link = Open(host, loginPort);
        }

        /// <summary>접속 완료 상태에서만 보낸다. 아니면 false.</summary>
        public bool Send(MessageWrapper message)
        {
            return Phase == NetPhase.Online && link != null && message != null && link.Send(message);
        }

        /// <summary>연결을 끊고 오프라인으로. 실패 상태도 지운다.</summary>
        public void Disconnect()
        {
            link?.Dispose();
            link = null;
            Enter(NetPhase.Offline, "");
        }

        public void Tick(float dt)
        {
            if (link == null) return;
            phaseTime += dt;

            // 먼저 받은 것을 처리한다: 서버가 응답 직후 연결을 끊어도 그 응답을 놓치지 않도록
            while (link != null && link.TryReceive(out var message)) Handle(message);
            if (link == null) return;

            if (link.IsClosed)
            {
                Fail(Phase == NetPhase.Online ? "서버와의 연결이 끊겼습니다" : (link.Error ?? "연결이 끊겼습니다"), -1);
                return;
            }

            switch (Phase)
            {
                case NetPhase.ConnectingLogin:
                case NetPhase.ConnectingGame:
                case NetPhase.LoggingIn:
                case NetPhase.Authenticating:
                    if (phaseTime > Timeout)
                        Fail(Phase == NetPhase.ConnectingLogin ? "로그인 서버에 연결할 수 없습니다"
                           : Phase == NetPhase.ConnectingGame ? "게임 서버에 연결할 수 없습니다"
                           : "서버가 응답하지 않습니다", -1);
                    break;

                case NetPhase.Online:
                    sinceKeepAlive += dt;
                    if (sinceKeepAlive >= KeepAliveInterval)
                    {
                        sinceKeepAlive = 0f;
                        if (link.Send(new MessageWrapper { KeepAliveRequest = new KeepAliveRequest() })) KeepAlivesSent++;
                    }
                    break;
            }
        }

        void Handle(MessageWrapper message)
        {
            switch (message.PayloadCase)
            {
                case MessageWrapper.PayloadOneofCase.ConnectedResponse:
                    if (Phase == NetPhase.ConnectingLogin)
                    {
                        link.Send(new MessageWrapper
                        {
                            LoginRequest = new LoginRequest { UserId = UserId, PasswordHash = ByteString.CopyFrom(passwordHash) }
                        });
                        Enter(NetPhase.LoggingIn, "로그인 중...");
                    }
                    else if (Phase == NetPhase.ConnectingGame)
                    {
                        link.Send(new MessageWrapper { GameConnectRequest = new GameConnectRequest { AuthToken = Token } });
                        Enter(NetPhase.Authenticating, "게임 서버 인증 중...");
                    }
                    break;

                case MessageWrapper.PayloadOneofCase.LoginResponse:
                    if (Phase != NetPhase.LoggingIn) break;
                    var login = message.LoginResponse;
                    if (!login.Success) { Fail(LoginErrorText(login.ErrorCode), login.ErrorCode); break; }
                    Token = login.AuthToken ?? "";
                    link.Dispose();                                   // 로그인 소켓은 닫고 게임 서버로 (토큰 유효 60초)
                    Enter(NetPhase.ConnectingGame, "게임 서버에 연결하는 중...");
                    link = Open(host, gamePort);
                    break;

                case MessageWrapper.PayloadOneofCase.GameConnectResponse:
                    if (Phase != NetPhase.Authenticating) break;
                    var game = message.GameConnectResponse;
                    if (!game.Success) { Fail(GameErrorText(game.ErrorCode), game.ErrorCode); break; }
                    sinceKeepAlive = 0f;
                    Enter(NetPhase.Online, "접속 완료");
                    break;

                case MessageWrapper.PayloadOneofCase.ErrorResponse:
                    Fail($"서버 오류 (코드 {message.ErrorResponse.ErrorCode})", message.ErrorResponse.ErrorCode);
                    break;

                default:
                    // 방/전투 메시지는 로그인 흐름의 일이 아니다: 접속 완료 뒤라면 밖(CoopSession 등)으로 넘긴다
                    if (Phase == NetPhase.Online) Unhandled?.Invoke(message);
                    break;
            }
        }

        INetLink Open(string h, int port)
        {
            try { return connect(h, port); }
            catch (Exception e) { Fail("연결 실패: " + e.Message, -1); return null; }
        }

        void Enter(NetPhase phase, string message)
        {
            Phase = phase;
            Message = message;
            phaseTime = 0f;
        }

        void Fail(string message, int code)
        {
            link?.Dispose();
            link = null;
            ErrorCode = code;
            Enter(NetPhase.Failed, message);
        }

        /// <summary>LoginResponse.error_code: 0 성공, 1 인증 실패, 2 밴, 3 서버 오류, 4 횟수 제한.</summary>
        public static string LoginErrorText(int code)
        {
            switch (code)
            {
                case 1: return "아이디 또는 비밀번호가 틀렸습니다";
                case 2: return "접속이 차단된 계정입니다";
                case 3: return "로그인 서버 오류입니다. 잠시 후 다시 시도하세요";
                case 4: return "시도가 너무 잦습니다. 잠시 후 다시 시도하세요";
                default: return $"로그인 실패 (코드 {code})";
            }
        }

        /// <summary>GameConnectResponse.error_code: 0 성공, 1 토큰 무효, 2 서버 오류.</summary>
        public static string GameErrorText(int code)
        {
            switch (code)
            {
                case 1: return "인증이 만료되었습니다. 다시 로그인하세요";
                case 2: return "게임 서버 오류입니다. 잠시 후 다시 시도하세요";
                default: return $"게임 서버 접속 실패 (코드 {code})";
            }
        }
    }
}
