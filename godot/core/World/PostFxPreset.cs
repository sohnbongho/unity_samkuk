using Samkuk.Compat;

namespace Samkuk.World
{
    /// <summary>
    /// HD-2D 후처리(Step 14-2)의 수치표. 화면 층이 이 값으로 WorldEnvironment 를 채운다(값을 고치면 바로 반영).
    /// 블룸은 문턱을 1 로 두어 스프라이트(밝기 1 이하)는 번지지 않고 HDR 로 1 을 넘는 빛(횃불, 플레이어 빛이 겹친 곳)만 번진다.
    /// 비네트는 가장자리를 살짝 어둡게, 색 보정은 대비·채도를 조금 올리고, 색온도는 시간대를 따른다(해질녘 따뜻하게, 새벽 차갑게).
    /// 틸트 시프트(흐림)는 여기 없다 — 커스텀 패스가 필요해 Step 14-5 로 따로 둔다.
    /// </summary>
    public static class PostFxPreset
    {
        public const float BloomThreshold = 1f;
        public const float BloomIntensity = 0.7f;
        public const float BloomScatter = 0.65f;
        public const float VignetteIntensity = 0.28f;
        public const float VignetteSmoothness = 0.45f;
        public const float Contrast = 10f;
        public const float Saturation = 12f;

        /// <summary>시간대별 색온도 (-100~100, 양수가 따뜻함). 전역광 색조와 겹치므로 작게.</summary>
        public static float Temperature(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Dusk: return 12f;
                case TimeOfDay.Dawn: return -6f;
                default: return 0f;
            }
        }

        /// <summary>시간대별 녹색-자홍 기울기. 새벽만 살짝 분홍.</summary>
        public static float Tint(TimeOfDay time) => time == TimeOfDay.Dawn ? 4f : 0f;

    }
}
