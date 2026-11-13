using System.Security.Cryptography;
using System.Text;

namespace Samkuk.Net
{
    /// <summary>비밀번호는 평문을 보내지 않고 SHA256(UTF8) 32바이트를 보낸다. 서버는 이 값을 PBKDF2 로 다시 해시해 검증한다.</summary>
    public static class PasswordHash
    {
        public static byte[] Client(string plain)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(plain ?? ""));
        }

        public static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
