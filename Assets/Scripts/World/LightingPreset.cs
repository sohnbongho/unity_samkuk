using Samkuk.Data;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>전역광 한 벌: 색과 세기.</summary>
    public struct GlobalLightSettings
    {
        public Color color;
        public float intensity;
    }

    /// <summary>
    /// HD-2D 조명(Step 14-1)의 수치표. 지형이 주는 색조에 시간대가 주는 색조를 곱해 전투 맵 전체의 전역광을 정한다.
    /// 낮/밤 주기는 없다(한 판이 1분이라 변화가 급해 보인다). 값을 바꾸면 테스트(<c>Hd2dLightingTests</c>)가 범위를 지킨다:
    /// 어떤 조합도 너무 어두워 적이 안 보이면 안 되고(채널 최소 0.45), 해질녘은 낮보다 따뜻해야 한다.
    /// </summary>
    public static class LightingPreset
    {
        /// <summary>어떤 조합에서도 이보다 어두운 채널은 없다 (적/보석이 보이게).</summary>
        public const float MinChannel = 0.45f;

        /// <summary>플레이어 주변의 약한 빛: 어두운 시간대에도 주인공 주변은 또렷하게.</summary>
        public static readonly Color PlayerGlowColor = new Color(1f, 0.96f, 0.88f, 1f);
        public const float PlayerGlowRadius = 3.2f;
        public const float PlayerGlowIntensity = 0.28f;

        public static GlobalLightSettings Global(CastleTerrain terrain, TimeOfDay time)
        {
            var t = TerrainTint(terrain);
            var d = TimeTint(time, out float intensity);
            var c = new Color(
                Mathf.Max(MinChannel, t.r * d.r),
                Mathf.Max(MinChannel, t.g * d.g),
                Mathf.Max(MinChannel, t.b * d.b), 1f);
            return new GlobalLightSettings { color = c, intensity = intensity };
        }

        /// <summary>지형이 주는 색조 (낮 기준). 평야는 거의 흰색, 초원·황토는 따뜻하고 산악·강변은 서늘하다.</summary>
        public static Color TerrainTint(CastleTerrain terrain)
        {
            switch (terrain)
            {
                case CastleTerrain.Steppe: return new Color(1f, 0.95f, 0.84f);
                case CastleTerrain.Mountain: return new Color(0.88f, 0.93f, 1f);
                case CastleTerrain.River: return new Color(0.9f, 0.98f, 1f);
                case CastleTerrain.Jungle: return new Color(0.9f, 1f, 0.9f);
                case CastleTerrain.Loess: return new Color(1f, 0.92f, 0.8f);
                default: return new Color(1f, 0.99f, 0.96f);   // Plain
            }
        }

        /// <summary>시간대가 주는 색조와 세기. 해질녘은 주황, 새벽은 분홍빛 푸른색.</summary>
        public static Color TimeTint(TimeOfDay time, out float intensity)
        {
            switch (time)
            {
                case TimeOfDay.Dusk: intensity = 0.92f; return new Color(1f, 0.78f, 0.58f);
                case TimeOfDay.Dawn: intensity = 0.96f; return new Color(0.96f, 0.9f, 1f);
                default: intensity = 1f; return Color.white;
            }
        }
    }
}
