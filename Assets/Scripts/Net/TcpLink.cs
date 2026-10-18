using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Messages;

namespace Samkuk.Net
{
    /// <summary>
    /// TCP 연결 하나. 접속은 비동기로 시작하고, 백그라운드 스레드가 받은 바이트를 프레임으로 잘라 큐에 넣는다.
    /// 유니티 API 를 쓰지 않으므로(메인 스레드로 넘기는 것은 NetClient 의 Update) 어느 스레드에서 끝나도 안전하다.
    /// 보내기는 메인 스레드에서 바로 쓴다(작은 프레임, 가끔이라 블로킹이 문제되지 않음).
    /// </summary>
    public sealed class TcpLink : INetLink
    {
        readonly TcpClient client = new TcpClient();
        readonly ConcurrentQueue<MessageWrapper> inbox = new ConcurrentQueue<MessageWrapper>();
        readonly object sendLock = new object();
        NetworkStream stream;
        volatile bool connected, closed, disposed;
        volatile string error;

        public string Endpoint { get; }

        public TcpLink(string host, int port)
        {
            Endpoint = $"{host}:{port}";
            try
            {
                client.NoDelay = true;
                client.BeginConnect(host, port, OnConnected, null);
            }
            catch (Exception e) { Fail("연결 실패: " + e.Message); }
        }

        void OnConnected(IAsyncResult ar)
        {
            try
            {
                client.EndConnect(ar);
                if (disposed) { client.Close(); return; }
                stream = client.GetStream();
                connected = true;
                var thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Samkuk.Net.Receive" };
                thread.Start();
            }
            catch (Exception e) { if (!disposed) Fail("연결 실패: " + e.Message); }
        }

        void ReceiveLoop()
        {
            var parser = new FrameParser();
            var chunk = new byte[16 * 1024];
            try
            {
                while (!disposed)
                {
                    int n = stream.Read(chunk, 0, chunk.Length);
                    if (n <= 0) { Fail("서버가 연결을 끊었습니다"); return; }
                    parser.Push(chunk, 0, n);
                    while (parser.TryPop(out var body)) inbox.Enqueue(NetFrames.Decode(body));
                }
            }
            catch (Exception e) { if (!disposed) Fail("수신 오류: " + e.Message); }
        }

        /// <summary>첫 원인만 남긴다 (끊긴 뒤 이어지는 오류는 덮어쓰지 않음).</summary>
        void Fail(string message)
        {
            if (closed) return;
            error = message;
            closed = true;
        }

        public bool IsConnected => connected && !closed;
        public bool IsClosed => closed;
        public string Error => error;

        public bool TryReceive(out MessageWrapper message) => inbox.TryDequeue(out message);

        public bool Send(MessageWrapper message)
        {
            if (!IsConnected) return false;
            try
            {
                var frame = NetFrames.Encode(message);
                lock (sendLock) stream.Write(frame, 0, frame.Length);
                return true;
            }
            catch (Exception e)
            {
                if (!disposed) Fail("송신 오류: " + e.Message);
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            closed = true;
            try { stream?.Close(); } catch { }
            try { client.Close(); } catch { }
        }
    }
}
