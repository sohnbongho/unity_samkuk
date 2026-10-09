using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>
    /// 소품 종류(kind, <c>tools/terrain_art/terrain.json</c> 의 그리는 방식 이름)별 이동 효과 기본값.
    /// 나무/바위/건물처럼 몸체가 있는 것은 막고(밑동 반지름), 풀/꽃/덤불/갈대처럼 밟고 지나는 것은 통과한다.
    /// 물(강, 연못)은 막지 않고 느려진다. 셋업(Step 12-6)이 테마 에셋에 이 값을 채우고, 이후에는 에셋 값을 직접 고친다.
    /// 반지름은 크기 배율 1 기준이며 실제로는 소품의 scale 을 곱한다.
    /// </summary>
    public static class TerrainPropKinds
    {
        /// <summary>물(강, 연못) 위에서의 기본 속도 배율.</summary>
        public const float WaterSlowFactor = 0.5f;
        /// <summary>연못 그림(288x192px = 4.5x3유닛)의 가운데 물 부분 반지름. 가장자리는 얕아 그대로 걷는다.</summary>
        public const float PondSlowRadius = 1.4f;
        /// <summary>깃대 두께.</summary>
        public const float BannerBlockRadius = 0.15f;

        /// <summary>종류에 맞는 기본값을 소품에 적용한다. 모르는 종류는 통과(아무 효과 없음).</summary>
        public static void ApplyDefaults(TerrainProp prop, string kind)
        {
            if (prop == null) return;
            prop.blockRadius = BlockRadius(kind);
            prop.slowRadius = kind == "pond" ? PondSlowRadius : 0f;
            prop.slowFactor = kind == "pond" ? WaterSlowFactor : 1f;
        }

        /// <summary>
        /// 종류별 점광원 기본값(HD-2D 조명, Step 14-1). 깃대는 꼭대기의 횃불(따뜻하고 일렁임), 유르트는 창의 불빛,
        /// 연못은 물에 비친 희미한 푸른 빛. 그 밖의 소품은 빛이 없다. 셋업(Step 12-6)은 테마에 빛 값이 하나도 없을 때만 채운다.
        /// </summary>
        public static void ApplyLightDefaults(TerrainProp prop, string kind)
        {
            if (prop == null) return;
            switch (kind)
            {
                case "banner":
                    prop.lightColor = new Color(1f, 0.72f, 0.42f, 1f);
                    prop.lightRadius = 2.8f; prop.lightIntensity = 0.9f; prop.lightFlicker = 0.35f; prop.lightHeight = 1.6f;
                    break;
                case "yurt":
                    prop.lightColor = new Color(1f, 0.78f, 0.5f, 1f);
                    prop.lightRadius = 2f; prop.lightIntensity = 0.5f; prop.lightFlicker = 0.15f; prop.lightHeight = 0.5f;
                    break;
                case "pond":
                    prop.lightColor = new Color(0.55f, 0.8f, 1f, 1f);
                    prop.lightRadius = 2.4f; prop.lightIntensity = 0.35f; prop.lightFlicker = 0.08f; prop.lightHeight = 0f;
                    break;
                default:
                    prop.lightColor = new Color(1f, 1f, 1f, 0f);
                    prop.lightRadius = 0f; prop.lightIntensity = 1f; prop.lightFlicker = 0f; prop.lightHeight = 0f;
                    break;
            }
        }

        /// <summary>서 있는 소품인가(옆모습, 캐릭터를 가림, 드리운 그림자). 막는 소품과 같은 집합이다 (CONTEXT.md "서 있는 소품").</summary>
        public static bool IsStanding(string kind) => BlockRadius(kind) > 0f;

        /// <summary>종류별 막는 반지름 (0 = 통과). 그림 폭에 맞춘 어림값: 나무는 밑동만, 바위는 몸체 대부분.</summary>
        public static float BlockRadius(string kind)
        {
            switch (kind)
            {
                case "tree": return 0.32f;
                case "pine": return 0.28f;
                case "palm": return 0.28f;
                case "willow": return 0.32f;
                case "deadtree": return 0.26f;
                case "stump": return 0.35f;
                case "rock": return 0.42f;
                case "mossrock": return 0.6f;
                case "boulder": return 0.7f;
                case "bigrock": return 1.1f;
                case "mound": return 0.9f;
                case "terrace": return 1.1f;
                case "yurt": return 0.95f;
                case "boat": return 0.8f;
                case "banner": return BannerBlockRadius;
                default: return 0f;   // bush, tuft, flowers, fern, reed, pond, riverwater, riverbank: 통과
            }
        }
    }
}
