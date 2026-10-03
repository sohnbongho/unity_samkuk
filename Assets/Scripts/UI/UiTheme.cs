using UnityEngine;

namespace Samkuk.UI
{
    /// <summary>
    /// UI 테마: 중국풍(먹색 바탕 + 금빛 테두리 + 진홍/옥색 포인트)의 색, 프레임 스프라이트, 폰트를 한 곳에 모은다.
    /// 실제 아트나 폰트로 바꾸려면 Resources/UiTheme.asset 의 슬롯만 교체하면 된다.
    /// 에셋이 없으면 스프라이트 없이 색만 있는 기본값을 쓴다 (스킨이 입혀지지 않을 뿐 동작은 정상).
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        public const string ResourceName = "UiTheme";

        [Header("스프라이트 (9-슬라이스는 에셋 임포트 설정의 Border 로 지정)")]
        [Tooltip("둥근 사각형 바탕 (색을 입혀 쓴다)")] public Sprite fill;
        [Tooltip("금빛 이중 테두리 + 모서리 장식 (패널/카드)")] public Sprite frame;
        [Tooltip("얇은 금빛 테두리 (버튼/바)")] public Sprite frameThin;
        [Tooltip("가운데 장식이 있는 가로 구분선")] public Sprite divider;

        [Header("폰트 (비워 두면 OS의 한글 폰트)")]
        public Font font;

        [Header("바탕색")]
        public Color panel = new Color(0.09f, 0.07f, 0.07f, 0.98f);
        public Color card = new Color(0.17f, 0.12f, 0.11f, 1f);
        public Color button = new Color(0.32f, 0.2f, 0.14f, 1f);
        public Color primaryButton = new Color(0.55f, 0.16f, 0.14f, 1f);
        public Color mutedButton = new Color(0.24f, 0.18f, 0.16f, 1f);

        [Header("강조색")]
        public Color gold = new Color(1f, 0.85f, 0.4f, 1f);
        public Color positive = new Color(0.2f, 0.42f, 0.28f, 1f);
        public Color disabled = new Color(0.28f, 0.24f, 0.22f, 1f);

        [Header("레벨업 카드 종류별 색")]
        public Color newWeapon = new Color(0.4f, 0.12f, 0.14f, 1f);
        public Color weaponUp = new Color(0.12f, 0.3f, 0.34f, 1f);
        public Color passive = new Color(0.13f, 0.32f, 0.2f, 1f);
        public Color heal = new Color(0.32f, 0.16f, 0.27f, 1f);
        public Color evolve = new Color(0.62f, 0.46f, 0.1f, 1f);

        static UiTheme cached;

        /// <summary>현재 테마. Resources에 에셋이 있으면 그것을, 없으면 기본값을 돌려준다 (null 이 아니다).</summary>
        public static UiTheme Get()
        {
            if (cached != null) return cached;

            cached = Resources.Load<UiTheme>(ResourceName);
            if (cached == null)
            {
                cached = CreateInstance<UiTheme>();
                cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return cached;
        }

        /// <summary>지정한 테마를 현재 테마로 쓴다 (테스트, 또는 실행 중 테마 교체용). null 이면 캐시를 비운다.</summary>
        public static void Use(UiTheme theme) => cached = theme;

        /// <summary>캐시를 버리고 다음 접근 때 다시 불러온다 (테스트/에셋 교체용).</summary>
        public static void ResetCache() => cached = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cached = null;
    }
}
