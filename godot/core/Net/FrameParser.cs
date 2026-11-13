using System;
using System.IO;

namespace Samkuk.Net
{
    /// <summary>
    /// 수신 바이트를 프레임([2바이트 LE 바디 길이][바디])으로 자르는 순수 파서. 한 수신에 여러 프레임이 붙어 오거나
    /// 한 프레임이 쪼개져 와도 되도록 누적한다. 서버의 ReceiveParser 와 같은 규칙(길이 0 또는 상한 초과는 오류).
    /// </summary>
    public sealed class FrameParser
    {
        byte[] buffer;
        int head;   // 아직 읽지 않은 첫 바이트
        int tail;   // 쌓인 바이트의 끝
        readonly int maxBody;

        public FrameParser(int maxBody = NetSettings.MaxBodySize, int capacity = 16 * 1024)
        {
            this.maxBody = maxBody;
            buffer = new byte[Math.Max(capacity, NetSettings.HeaderSize + maxBody)];
        }

        /// <summary>아직 프레임으로 꺼내지 않은 바이트 수.</summary>
        public int Pending => tail - head;

        /// <summary>받은 바이트를 뒤에 붙인다. 앞서 TryPop 이 돌려준 조각은 이 호출 뒤에는 쓸 수 없다.</summary>
        public void Push(byte[] src, int offset, int length)
        {
            if (length <= 0) return;
            if (head > 0)   // 읽은 부분을 버리고 앞으로 당긴다
            {
                Buffer.BlockCopy(buffer, head, buffer, 0, tail - head);
                tail -= head;
                head = 0;
            }
            if (tail + length > buffer.Length)
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, tail + length));
            Buffer.BlockCopy(src, offset, buffer, tail, length);
            tail += length;
        }

        /// <summary>완성된 프레임이 있으면 바디 조각을 돌려준다 (다음 Push 전까지만 유효).</summary>
        public bool TryPop(out ArraySegment<byte> body)
        {
            body = default;
            if (Pending < NetSettings.HeaderSize) return false;
            int size = buffer[head] | (buffer[head + 1] << 8);
            if (size == 0 || size > maxBody)
                throw new InvalidDataException($"유효하지 않은 프레임 길이 {size} (허용 1~{maxBody})");
            if (Pending < NetSettings.HeaderSize + size) return false;
            body = new ArraySegment<byte>(buffer, head + NetSettings.HeaderSize, size);
            head += NetSettings.HeaderSize + size;
            return true;
        }

        public void Reset()
        {
            head = tail = 0;
        }
    }
}
