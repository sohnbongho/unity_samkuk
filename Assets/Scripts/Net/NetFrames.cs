using System;
using System.IO;
using Google.Protobuf;
using Messages;

namespace Samkuk.Net
{
    /// <summary>MessageWrapper 와 프레임 바이트 사이의 변환. 바디는 protobuf 직렬화, 머리는 바디 길이(헤더 제외) LE ushort.</summary>
    public static class NetFrames
    {
        public static byte[] Encode(MessageWrapper message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var body = message.ToByteArray();
            if (body.Length == 0 || body.Length > NetSettings.MaxBodySize)
                throw new InvalidDataException($"보낼 수 없는 프레임 길이 {body.Length} (허용 1~{NetSettings.MaxBodySize})");
            var frame = new byte[NetSettings.HeaderSize + body.Length];
            frame[0] = (byte)(body.Length & 0xFF);
            frame[1] = (byte)(body.Length >> 8);
            Buffer.BlockCopy(body, 0, frame, NetSettings.HeaderSize, body.Length);
            return frame;
        }

        public static MessageWrapper Decode(ArraySegment<byte> body)
            => MessageWrapper.Parser.ParseFrom(body.Array, body.Offset, body.Count);
    }
}
