using Samkuk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 카메라의 틸트 시프트(Step 14-5): 2D 렌더러 에셋에 들어 있는 Full Screen Pass 렌더러 기능(셋업 Step 14-5 가 만듦)을
    /// 전투 동안만 켜고, 머티리얼 값(<see cref="TiltShiftPreset"/>)을 화면 높이에 맞춰 넣는다. 렌더러 기능은 에셋이라 모든 씬의 카메라에
    /// 적용되므로 이 컴포넌트가 사라질 때(다른 씬으로) 반드시 끈다 — 타이틀/내정/맵 편집기는 흐리지 않게.
    /// 설정 <see cref="Hd2dSettings.TiltShift"/>, 디버그 키 F9. 끄면 패스 자체가 돌지 않아 비용이 없다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattleTiltShift : MonoBehaviour
    {
        [SerializeField, Tooltip("Renderer2D.asset 안의 Full Screen Pass 렌더러 기능 (셋업이 연결)")] ScriptableRendererFeature feature;
        [SerializeField, Tooltip("틸트 시프트 머티리얼 (셋업이 연결)")] Material material;

        int appliedHeight = -1;

        public ScriptableRendererFeature Feature { get => feature; set => feature = value; }
        public Material Material { get => material; set => material = value; }
        public bool IsEnabled { get; private set; }

        void Awake() => Apply(Hd2dSettings.TiltShift);

        /// <summary>켜면 렌더러 기능을 살리고 값을 넣는다. 끄면 기능을 꺼 패스가 돌지 않게 한다.</summary>
        public void Apply(bool enabled)
        {
            IsEnabled = enabled;
            if (feature != null && feature.isActive != enabled) feature.SetActive(enabled);
            if (enabled) Refresh(Screen.height);
        }

        /// <summary>화면 높이에 맞는 값을 머티리얼에 넣는다 (해상도가 바뀌면 다시).</summary>
        public void Refresh(int screenHeight)
        {
            appliedHeight = screenHeight;
            TiltShiftPreset.Apply(material, screenHeight);
        }

        void LateUpdate()
        {
            if (IsEnabled && Screen.height != appliedHeight) Refresh(Screen.height);
        }

        void OnDisable()
        {
            // 다른 씬으로 가면 흐림이 남지 않게 (렌더러 기능은 에셋이라 모든 카메라에 적용된다)
            if (feature != null && feature.isActive) feature.SetActive(false);
        }

        void OnEnable()
        {
            if (IsEnabled && feature != null && !feature.isActive) feature.SetActive(true);
        }
    }
}
