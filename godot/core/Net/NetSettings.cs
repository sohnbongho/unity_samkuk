namespace Samkuk.Net
{
    /// <summary>
    /// 서버 주소와 프로토콜 상수 한 곳. 프로토콜 쪽 원본은 csharpServer(SessionConstInfo, message.proto)이고
    /// 거기가 바뀌면 여기도 맞춘다 (docs/NETWORK.md).
    /// </summary>
    public static class NetSettings
    {
        public static string Host = "127.0.0.1";
        public static int LoginPort = 9100;
        public static int GamePort = 9101;

        /// <summary>프레임 머리: 바디 길이(헤더 제외)를 little-endian ushort 로.</summary>
        public const int HeaderSize = 2;
        /// <summary>서버가 연결을 끊는 바디 길이 상한.</summary>
        public const int MaxBodySize = 8190;
        /// <summary>게임 서버 연결 유지 간격(초). 서버는 10초 동안 못 받으면 끊는다.</summary>
        public const float KeepAliveInterval = 3f;
        /// <summary>연결/응답을 이만큼 기다리면 실패로 본다(초). 첫 로그인은 서버의 DB 첫 연결 + PBKDF2(10만 회)로 수 초 걸릴 수 있어 넉넉히.</summary>
        public const float ResponseTimeout = 15f;
    }
}
