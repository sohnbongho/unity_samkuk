using Samkuk.Meta;

namespace Samkuk.Core
{
    /// <summary>
    /// HD-2D 연출(Step 14)의 켜고 끄기. 저장 설정(<see cref="SaveData.hd2dLighting"/> 등)을 읽되, 테스트와 디버그 키(F5)는
    /// <see cref="LightingOverride"/> 로 저장을 건드리지 않고 바꾼다. 단계마다 스위치를 두어 전후 비교와 되돌리기를 쉽게 한다.
    /// </summary>
    public static class Hd2dSettings
    {
        /// <summary>null 이면 저장 설정을 따른다.</summary>
        public static bool? LightingOverride { get; set; }

        /// <summary>조명(전역광 색조, 소품 점광원, 플레이어 빛)을 쓰는가.</summary>
        public static bool Lighting => LightingOverride ?? SaveSystem.Current.hd2dLighting;

        /// <summary>테스트/디버그가 바꾼 값을 모두 지운다.</summary>
        public static void ResetOverrides() => LightingOverride = null;
    }
}
