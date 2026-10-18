using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>남은 시간, 웨이브, 이벤트 배너, 보스 체력바를 표시한다.</summary>
    public class StageHudView : MonoBehaviour
    {
        const float BannerSeconds = 3f;

        [SerializeField] StageController stage;
        [SerializeField] Text timerLabel;
        [SerializeField] Text waveLabel;
        [SerializeField] Text bannerLabel;
        [SerializeField] GameObject bossBar;
        [SerializeField, Tooltip("피벗이 왼쪽인 보스 체력 채움")] RectTransform bossFill;
        [SerializeField] Text bossName;

        Enemy boss;
        float bannerTimer;

        public StageController Stage { get => stage; set => stage = value; }

        void OnEnable()
        {
            Enemy.Died += OnEnemyDied;
            if (stage == null) return;
            stage.WaveChanged += OnWaveChanged;
            stage.EventTriggered += OnEvent;
            stage.BossSpawned += OnBossSpawned;
            OnWaveChanged(stage.WaveNumber);
        }

        void OnDisable()
        {
            Enemy.Died -= OnEnemyDied;
            if (stage == null) return;
            stage.WaveChanged -= OnWaveChanged;
            stage.EventTriggered -= OnEvent;
            stage.BossSpawned -= OnBossSpawned;
        }

        void Start()
        {
            if (bannerLabel != null) bannerLabel.gameObject.SetActive(false);
            if (bossBar != null) bossBar.SetActive(false);
        }

        void Update()
        {
            if (stage != null && timerLabel != null)
            {
                int sec = Mathf.CeilToInt(stage.Remaining);
                timerLabel.text = $"{sec / 60:00}:{sec % 60:00}";
            }

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.unscaledDeltaTime;
                if (bannerTimer <= 0f && bannerLabel != null) bannerLabel.gameObject.SetActive(false);
            }

            if (boss != null && boss.Alive && bossFill != null && boss.Data != null)
            {
                float ratio = Mathf.Clamp01(boss.Hp / boss.Data.maxHp);
                bossFill.localScale = new Vector3(ratio, 1f, 1f);
            }
        }

        void OnWaveChanged(int waveNumber)
        {
            if (stage == null) return;
            SetWave(waveNumber, stage.WaveName);
        }

        // ───────────── 협동 전투(서버 값)용: StageController 없이 밖에서 넣는다 ─────────────

        public void SetTimerSeconds(int seconds)
        {
            if (timerLabel != null) timerLabel.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        public void SetWave(int waveNumber, string waveName)
        {
            if (waveLabel != null) waveLabel.text = waveNumber > 0 ? $"웨이브 {waveNumber}  {waveName}" : "";
        }

        /// <summary>배너 문구를 seconds 동안 보여 준다 (0 이하면 기본 3초).</summary>
        public void ShowBanner(string message, float seconds = 0f)
        {
            if (bannerLabel == null || string.IsNullOrEmpty(message)) return;
            bannerLabel.text = message;
            bannerLabel.gameObject.SetActive(true);
            bannerTimer = seconds > 0f ? seconds : BannerSeconds;
        }

        public void ShowBoss(string name, float hpRatio)
        {
            boss = null; // 서버 값으로만 채운다
            if (bossName != null) bossName.text = name ?? "";
            if (bossFill != null) bossFill.localScale = new Vector3(Mathf.Clamp01(hpRatio), 1f, 1f);
            if (bossBar != null && !bossBar.activeSelf) bossBar.SetActive(true);
        }

        public void HideBoss()
        {
            if (bossBar != null && bossBar.activeSelf) bossBar.SetActive(false);
        }

        void OnEvent(StageEvent ev) => ShowBanner(ev.message);

        void OnBossSpawned(Enemy e)
        {
            boss = e;
            if (bossName != null && e.Data != null) bossName.text = e.Data.displayName;
            if (bossFill != null) bossFill.localScale = Vector3.one;
            if (bossBar != null) bossBar.SetActive(true);
        }

        void OnEnemyDied(Enemy e)
        {
            if (e != boss) return;
            boss = null;
            if (bossBar != null) bossBar.SetActive(false);
        }
    }
}
