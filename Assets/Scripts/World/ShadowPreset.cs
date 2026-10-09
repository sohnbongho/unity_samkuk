using UnityEngine;

namespace Samkuk.World
{
    /// <summary>드리운 그림자 한 벌: 높이 1 인 점이 옆으로 얼마나 밀리는지(shearX), 아래로 얼마나 뻗는지(length), 농도(alpha).</summary>
    public struct ShadowSettings
    {
        public float shearX;
        public float length;
        public float alpha;
    }

    /// <summary>
    /// 드리운 그림자(Step 14-4)의 수치표와 수학. 캐릭터/서 있는 소품의 그림을 어둡게 한 번 더 그리되 발밑에서 비스듬히 눕힌다:
    /// 발을 원점으로 높이 y 인 점은 (x + shearX·y, -length·y) 로 간다 — 땅에 드리운 햇빛 그림자. 시간대가 해질녘이면 길고 짙다.
    /// 유니티 트랜스폼에는 전단(shear)이 없으므로 그 2x2 행렬을 "회전 · 배율 · 회전"으로 분해해(<see cref="Decompose"/>) 3단 트랜스폼으로 만든다.
    /// </summary>
    public static class ShadowPreset
    {
        /// <summary>그림자가 그려지는 정렬 순서 (배경 레이어 안, 바닥 얼룩/강/연못/바닥 장식보다 위).</summary>
        public const int SortingOrder = 200;

        /// <summary>적은 발밑 타원(발밑 그림자)으로 할지. 수백 마리의 드리운 그림자가 무거우면 true 로 (값 하나로 전환).</summary>
        public static bool EnemyBlob = false;

        public static ShadowSettings For(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Dusk: return new ShadowSettings { shearX = 0.9f, length = 0.7f, alpha = 0.42f };    // 낮은 해: 길고 짙게
                case TimeOfDay.Dawn: return new ShadowSettings { shearX = -0.7f, length = 0.6f, alpha = 0.3f };   // 반대쪽에서, 옅게
                default: return new ShadowSettings { shearX = 0.35f, length = 0.45f, alpha = 0.32f };
            }
        }

        /// <summary>그림자 행렬 [x'; y'] = [[1, shearX], [0, -length]] · [x; y].</summary>
        public static void Matrix(ShadowSettings s, out float a, out float b, out float c, out float d)
        {
            a = 1f; b = s.shearX; c = 0f; d = -s.length;
        }

        /// <summary>
        /// 2x2 행렬 [[a, b], [c, d]] 를 R(outer) · diag(sx, sy) · R(inner) 로 분해한다 (각도는 도, 반시계 양수). sy 는 음수일 수 있다(뒤집힘).
        /// 트랜스폼 계층: 바깥 노드 회전 outer → 가운데 노드 배율 (sx, sy) → 안쪽 노드 회전 inner.
        /// </summary>
        public static void Decompose(float a, float b, float c, float d, out float outerDeg, out float sx, out float sy, out float innerDeg)
        {
            float e = (a + d) * 0.5f, f = (a - d) * 0.5f, g = (c + b) * 0.5f, h = (c - b) * 0.5f;
            float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + g * g);
            sx = q + r;
            sy = q - r;
            float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(h, e);
            innerDeg = (a2 - a1) * 0.5f * Mathf.Rad2Deg;
            outerDeg = (a2 + a1) * 0.5f * Mathf.Rad2Deg;
        }
    }
}
