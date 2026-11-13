using System;

namespace Samkuk.Compat
{
    /// <summary>
    /// 유니티 Mathf 와 같은 이름·같은 결과의 수학 함수 (Godot 전환, ADR 0004).
    /// 순수 로직을 고치지 않고 옮기려고 둔 호환 층이라 로직이 쓰는 것만 있다.
    /// </summary>
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Infinity = float.PositiveInfinity;
        public const float Epsilon = float.Epsilon;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int v) => Math.Abs(v);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b, float c) => Min(Min(a, b), c);
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);

        public static int NextPowerOfTwo(int v) { int p = 1; while (p < v) p <<= 1; return p; }
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        /// <summary>유니티와 같이 .5 는 짝수 쪽으로.</summary>
        public static float Round(float f) => (float)Math.Round(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int RoundToInt(float f) => (int)Math.Round(f);

        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;

        public static float MoveTowards(float current, float target, float maxDelta)
            => Abs(target - current) <= maxDelta ? target : current + Sign(target - current) * maxDelta;

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }

        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);

        public static float DeltaAngle(float current, float target)
        {
            float d = Repeat(target - current, 360f);
            return d > 180f ? d - 360f : d;
        }

        public static bool Approximately(float a, float b)
            => Abs(b - a) < Max(0.000001f * Max(Abs(a), Abs(b)), Epsilon * 8f);

        // 고전 펄린 노이즈(2D). 유니티와 값이 같지는 않다: 일렁임 같은 연출에만 쓰므로 0~1 범위와 연속성만 맞춘다.
        public static float PerlinNoise(float x, float y)
        {
            int xi = FloorToInt(x), yi = FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = Fade(xf), v = Fade(yf);
            float a = Grad(Hash(xi, yi), xf, yf), b = Grad(Hash(xi + 1, yi), xf - 1f, yf);
            float c = Grad(Hash(xi, yi + 1), xf, yf - 1f), d = Grad(Hash(xi + 1, yi + 1), xf - 1f, yf - 1f);
            float n = LerpUnclamped(LerpUnclamped(a, b, u), LerpUnclamped(c, d, u), v);   // 약 -0.7~0.7
            return Clamp01(n * 0.7f + 0.5f);
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static int Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) & 7u);
            }
        }

        static float Grad(int h, float x, float y) => h switch
        {
            0 => x + y, 1 => x - y, 2 => -x + y, 3 => -x - y,
            4 => x, 5 => -x, 6 => y, _ => -y,
        };
    }
}
