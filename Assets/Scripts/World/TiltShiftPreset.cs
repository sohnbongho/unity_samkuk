using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 틸트 시프트(Step 14-5)의 수치표. 셰이더(<c>Assets/Shaders/TiltShift.shader</c>)의 머티리얼 값은 에셋이 아니라 여기서 넣는다
    /// (값을 고치면 바로 반영). 흐림 반지름은 화면 픽셀 단위라 1080p 기준값에 화면 높이 비율을 곱해 해상도가 달라도 같은 두께로 보이게 한다.
    /// </summary>
    public static class TiltShiftPreset
    {
        public const string ShaderName = "Samkuk/TiltShift";
        public static readonly int AmountId = Shader.PropertyToID("_TiltShiftAmount");
        public static readonly int FocusId = Shader.PropertyToID("_TiltShiftFocus");
        public static readonly int BandId = Shader.PropertyToID("_TiltShiftBand");
        public static readonly int RadiusId = Shader.PropertyToID("_TiltShiftRadius");

        /// <summary>흐림 세기 (0 = 없음, 1 = 기준).</summary>
        public const float Amount = 1f;
        /// <summary>또렷한 띠의 가운데 (화면 y, 0 아래 ~ 1 위). 플레이어가 가운데에 있으니 0.5.</summary>
        public const float Focus = 0.5f;
        /// <summary>또렷한 띠의 반높이 (화면 비율). 0.18 이면 가운데 36% 는 흐림 없음.</summary>
        public const float Band = 0.18f;
        /// <summary>화면 맨 위/아래의 최대 흐림 반지름 (1080p 기준 픽셀).</summary>
        public const float RadiusAt1080 = 6f;

        /// <summary>화면 높이에 맞춘 흐림 반지름 (1080 → 6, 2160 → 12).</summary>
        public static float RadiusFor(int screenHeight) => RadiusAt1080 * Mathf.Max(1, screenHeight) / 1080f;

        /// <summary>머티리얼에 값을 넣는다.</summary>
        public static void Apply(Material material, int screenHeight, float amount = Amount)
        {
            if (material == null) return;
            material.SetFloat(AmountId, amount);
            material.SetFloat(FocusId, Focus);
            material.SetFloat(BandId, Band);
            material.SetFloat(RadiusId, RadiusFor(screenHeight));
        }
    }
}
