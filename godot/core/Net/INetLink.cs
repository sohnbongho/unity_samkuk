using System;
using Messages;

namespace Samkuk.Net
{
    /// <summary>
    /// 서버와의 연결 하나. 만들면 바로 접속을 시작하고, 받은 메시지는 큐에 쌓여 메인 스레드가 TryReceive 로 꺼낸다.
    /// 로그인 흐름(LoginFlow)이 이 인터페이스만 보므로 테스트는 가짜 연결로 서버 없이 돌린다.
    /// </summary>
    public interface INetLink : IDisposable
    {
        /// <summary>접속이 끝나 보낼 수 있는 상태.</summary>
        bool IsConnected { get; }
        /// <summary>접속 실패/끊김/닫음. 한 번 true 가 되면 되돌아오지 않는다.</summary>
        bool IsClosed { get; }
        /// <summary>닫힌 이유(없으면 null).</summary>
        string Error { get; }
        bool TryReceive(out MessageWrapper message);
        bool Send(MessageWrapper message);
    }
}
