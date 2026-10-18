using Samkuk.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>플레이어 체력바와 경험치바(채움 비율 + 텍스트). HUD 캔버스 전체에 한글 폰트를 적용한다.</summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField, Tooltip("피벗이 왼쪽인 채움 이미지의 RectTransform")] RectTransform hpFill;
        [SerializeField] Text hpLabel;
        [SerializeField] PlayerExperience playerExperience;
        [SerializeField, Tooltip("피벗이 왼쪽인 경험치 채움 이미지")] RectTransform expFill;
        [SerializeField] Text levelLabel;

        public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }
        public PlayerExperience PlayerExperience { get => playerExperience; set => playerExperience = value; }

        void Awake() => UiFont.Apply(gameObject);

        void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed += OnHealthChanged;
                OnHealthChanged(playerHealth.Current, playerHealth.Max);
            }
            if (playerExperience != null)
            {
                playerExperience.Changed += OnExpChanged;
                OnExpChanged(playerExperience.Level, playerExperience.Current, playerExperience.ToNext);
            }
        }

        void OnDisable()
        {
            if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
            if (playerExperience != null) playerExperience.Changed -= OnExpChanged;
        }

        /// <summary>협동 전투: 서버 PlayerState 값을 그대로 (PlayerHealth 이벤트 대신).</summary>
        public void SetHealth(float current, float max) => OnHealthChanged(current, max);
        public void SetExp(int level, int current, int toNext) => OnExpChanged(level, current, toNext);

        void OnHealthChanged(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (hpFill != null) hpFill.localScale = new Vector3(ratio, 1f, 1f);
            if (hpLabel != null) hpLabel.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        void OnExpChanged(int level, int current, int toNext)
        {
            float ratio = toNext > 0 ? Mathf.Clamp01((float)current / toNext) : 0f;
            if (expFill != null) expFill.localScale = new Vector3(ratio, 1f, 1f);
            if (levelLabel != null) levelLabel.text = $"Lv.{level}   {current} / {toNext}";
        }
    }
}
