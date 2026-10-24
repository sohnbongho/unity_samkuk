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
        [SerializeField, Tooltip("3D 렌더러(Renderer3D.asset)의 같은 기능 (셋업 Step 16-1 이 연결). 2.5D 틸트일 때 이쪽을 켠다")] ScriptableRendererFeature tiltFeature;

        int appliedHeight = -1;

        public ScriptableRendererFeature Feature { get => feature; set => feature = value; }
        public ScriptableRendererFeature TiltFeature { get => tiltFeature; set => tiltFeature = value; }
        public Material Material { get => material; set => material = value; }
        public bool IsEnabled { get; private set; }

        /// <summary>지금 카메라가 쓰는 렌더러의 기능: 틸트면 3D 렌더러 쪽, 아니면 2D 렌더러 쪽.</summary>
        public ScriptableRendererFeature Current => TiltView.Active && tiltFeature != null ? tiltFeature : feature;

        void Awake() => Apply(Hd2dSettings.TiltShift);

        /// <summary>켜면 렌더러 기능을 살리고 값을 넣는다. 끄면 기능을 꺼 패스가 돌지 않게 한다. 쓰지 않는 쪽 렌더러의 기능은 항상 끈다.</summary>
        public void Apply(bool enabled)
        {
            IsEnabled = enabled;
            var current = Current;
            var other = ReferenceEquals(current, feature) ? tiltFeature : feature;
            if (other != null && other.isActive) other.SetActive(false);
            if (current != null && current.isActive != enabled) current.SetActive(enabled);
            if (enabled) Refresh(Screen.height);
        }

        void OnTiltChanged() => Apply(IsEnabled);

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
            TiltView.Changed -= OnTiltChanged;
            // 다른 씬으로 가면 흐림이 남지 않게 (렌더러 기능은 에셋이라 모든 카메라에 적용된다)
            if (feature != null && feature.isActive) feature.SetActive(false);
            if (tiltFeature != null && tiltFeature.isActive) tiltFeature.SetActive(false);
        }

        void OnEnable()
        {
            TiltView.Changed += OnTiltChanged;
            var current = Current;
            if (IsEnabled && current != null && !current.isActive) current.SetActive(true);
        }
    }
}
