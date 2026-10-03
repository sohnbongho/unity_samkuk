using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.Feedback
{
    /// <summary>플레이어가 맞았을 때 화면 전체가 붉게 번쩍였다가 사라지는 효과 (HUD의 전체 화면 이미지).</summary>
    public class DamageFlashView : MonoBehaviour
    {
        [SerializeField] Image image;
        [SerializeField, Tooltip("강도 1일 때의 최대 불투명도")] float maxAlpha = 0.4f;
        [SerializeField, Tooltip("초당 줄어드는 강도")] float decayPerSecond = 2.2f;

        float strength;

        public Image Image { get => image; set => image = value; }
        /// <summary>현재 강도(0~1).</summary>
        public float Strength => strength;

        /// <summary>강도(0~1)만큼 번쩍인다. 이미 더 강하게 번쩍이는 중이면 그대로 둔다.</summary>
        public void Flash(float amount)
        {
            strength = Mathf.Clamp01(Mathf.Max(strength, amount));
            Apply();
        }

        void Update()
        {
            if (strength <= 0f) return;
            strength = Mathf.Max(0f, strength - decayPerSecond * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            if (image == null) return;
            var c = image.color;
            c.a = strength * maxAlpha;
            image.color = c;
        }
    }
}
