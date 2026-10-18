using UnityEngine;

namespace Samkuk.Coop
{
    /// <summary>서버 좌표 단위 변환. 위치는 sint32 x100(cm), 방향은 x1000, 속도는 x100 (coop-design.md 2절).</summary>
    public static class NetUnits
    {
        public const float PosScale = 100f;
        public const float DirScale = 1000f;

        public static float Units(int cm) => cm / PosScale;
        public static Vector2 Pos(int x, int y) => new Vector2(x / PosScale, y / PosScale);
        public static Vector2 Dir(int x, int y)
        {
            var d = new Vector2(x / DirScale, y / DirScale);
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.right;
        }
        public static float Speed(int v) => v / PosScale;
        public static float Seconds(int ms) => ms / 1000f;
    }
}
