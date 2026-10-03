using Samkuk.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>플레이어 체력바(채움 비율 + 텍스트).</summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField, Tooltip("피벗이 왼쪽인 채움 이미지의 RectTransform")] RectTransform hpFill;
        [SerializeField] Text hpLabel;

        public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }

        void OnEnable()
        {
            if (playerHealth == null) return;
            playerHealth.Changed += OnHealthChanged;
            OnHealthChanged(playerHealth.Current, playerHealth.Max);
        }

        void OnDisable()
        {
            if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
        }

        void OnHealthChanged(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (hpFill != null) hpFill.localScale = new Vector3(ratio, 1f, 1f);
            if (hpLabel != null) hpLabel.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
