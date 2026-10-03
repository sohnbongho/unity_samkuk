using System;
using Samkuk.Player;
using Samkuk.Stages;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Samkuk.Core
{
    /// <summary>
    /// 게임 상태 관리: 플레이어 사망(게임오버) / 스테이지 클리어 시 게임을 멈추고 Finished 이벤트를 보낸다.
    /// R 키로 다시 시작하고, GoToTitle로 타이틀 씬으로 돌아간다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string TitleSceneName = "TitleScene";
        public const string GameSceneName = "SampleScene";

        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] StageController stage;
        [SerializeField] GameObject clearPanel;

        public bool IsGameOver { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsFinished => IsGameOver || IsCleared;

        public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }
        public StageController Stage { get => stage; set => stage = value; }

        /// <summary>한 판이 끝났을 때 (true: 클리어, false: 게임오버). 게임은 이미 멈춘 상태다.</summary>
        public event Action<bool> Finished;

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
            Finished?.Invoke(false);
        }

        void OnStageCleared()
        {
            if (IsFinished) return;
            IsCleared = true;
            if (clearPanel != null) clearPanel.SetActive(true);
            Time.timeScale = 0f;
            Finished?.Invoke(true);
        }

        void Update()
        {
            if (!IsFinished) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame) Restart();
            else if (kb.tKey.wasPressedThisFrame) GoToTitle();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>타이틀 씬으로 이동한다. 타이틀 씬이 빌드에 없으면 현재 씬을 다시 시작한다.</summary>
        public void GoToTitle()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(TitleSceneName))
                SceneManager.LoadScene(TitleSceneName);
            else
                Restart();
        }

        void OnDestroy()
        {
            // 에디터에서 플레이를 멈춰도 timeScale이 0으로 남지 않도록
            Time.timeScale = 1f;
        }
    }
}
