using Samkuk.Player;
using Samkuk.Stages;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Samkuk.Core
{
    /// <summary>게임 상태 관리: 플레이어 사망 시 게임오버, 스테이지 클리어 시 클리어 화면, R 키로 재시작.</summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] StageController stage;
        [SerializeField] GameObject clearPanel;

        public bool IsGameOver { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsFinished => IsGameOver || IsCleared;

        void Awake()
        {
            Time.timeScale = 1f;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (clearPanel != null) clearPanel.SetActive(false);
        }

        void OnEnable()
        {
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
            if (stage != null) stage.Cleared += OnStageCleared;
        }

        void OnDisable()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (stage != null) stage.Cleared -= OnStageCleared;
        }

        void OnPlayerDied()
        {
            if (IsFinished) return;
            IsGameOver = true;
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            Time.timeScale = 0f;
        }

        void OnStageCleared()
        {
            if (IsFinished) return;
            IsCleared = true;
            if (clearPanel != null) clearPanel.SetActive(true);
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (!IsFinished) return;

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnDestroy()
        {
            // 에디터에서 플레이를 멈춰도 timeScale이 0으로 남지 않도록
            Time.timeScale = 1f;
        }
    }
}
