using System.Collections.Generic;
using Messages;
using Samkuk.Net;

namespace Samkuk.Tests
{
    /// <summary>가짜 연결: 서버 없이 받은 메시지를 큐로 넣고(Inbox) 보낸 메시지를 기록한다(Sent). NetLoginTests / CoopRoomTests 가 함께 쓴다.</summary>
    class FakeLink : INetLink
    {
        public string Host; public int Port;
        public bool IsConnected { get; set; }
        public bool IsClosed { get; set; }
        public string Error { get; set; }
        public bool Disposed;
        public readonly Queue<MessageWrapper> Inbox = new Queue<MessageWrapper>();
        public readonly List<MessageWrapper> Sent = new List<MessageWrapper>();

        public bool TryReceive(out MessageWrapper m)
        {
            if (Inbox.Count > 0) { m = Inbox.Dequeue(); return true; }
            m = null; return false;
        }
        public bool Send(MessageWrapper m)
        {
            if (!IsConnected || IsClosed) return false;
            Sent.Add(m); return true;
        }
        public void Dispose() { Disposed = true; IsClosed = true; }

        public void Hello(int index = 1) => Inbox.Enqueue(new MessageWrapper { ConnectedResponse = new ConnectedResponse { Index = index } });
        public void LoginOk(string token) => Inbox.Enqueue(new MessageWrapper { LoginResponse = new LoginResponse { Success = true, ErrorCode = 0, AuthToken = token } });
        public void LoginFail(int code) => Inbox.Enqueue(new MessageWrapper { LoginResponse = new LoginResponse { Success = false, ErrorCode = code } });
        public void GameOk() => Inbox.Enqueue(new MessageWrapper { GameConnectResponse = new GameConnectResponse { Success = true } });
        public void GameFail(int code) => Inbox.Enqueue(new MessageWrapper { GameConnectResponse = new GameConnectResponse { Success = false, ErrorCode = code } });
    }

    /// <summary>연결 공장: 만든 가짜 연결을 순서대로 기록한다 (0 = 로그인 서버, 1 = 게임 서버).</summary>
    class FakeNet
    {
        public readonly List<FakeLink> Links = new List<FakeLink>();
        public bool AutoConnect = true;
        public INetLink Connect(string host, int port)
        {
            var link = new FakeLink { Host = host, Port = port, IsConnected = AutoConnect };
            Links.Add(link);
            return link;
        }
    }
}
