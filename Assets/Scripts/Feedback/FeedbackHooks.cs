using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.Stages;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Feedback
{
    /// <summary>처치한 적의 비중. 클수록 연출이 크다.</summary>
    public enum KillWeight { Normal, Heavy, Boss }

    /// <summary>
    /// 게임 이벤트를 시각 연출(입자, 화면 흔들림, 피격 번쩍임)에 연결한다.
    /// 소리는 SfxHooks가 맡고, 이 컴포넌트는 눈에 보이는 타격감만 담당한다.
    /// </summary>
    public class FeedbackHooks : MonoBehaviour
    {
        // 입자 개수에 상한이 있지만, 잔 타격 불꽃은 이 정도 넘게 쌓였으면 생략해서 처치 파편이 묻히지 않게 한다
        const int SparkBudget = 250;

        static readonly Color SparkColor = new Color(1f, 0.95f, 0.6f);
        static readonly Color BloodColor = new Color(0.9f, 0.15f, 0.15f);
        static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] PlayerExperience experience;
        [SerializeField] WeaponController weapons;
        [SerializeField] SkillController skills;
        [SerializeField] StageController stage;
        [SerializeField] ScreenShake shake;
        [SerializeField] BurstFx burst;
        [SerializeField] DamageFlashView flash;

        float lastHealth = -1f;

        public PlayerHealth PlayerHealth { get => playerHealth; set => Rewire(() => playerHealth = value); }
        public PlayerExperience Experience { get => experience; set => Rewire(() => experience = value); }
        public WeaponController Weapons { get => weapons; set => Rewire(() => weapons = value); }
        public SkillController Skills { get => skills; set => Rewire(() => skills = value); }
        public StageController Stage { get => stage; set => Rewire(() => stage = value); }
        public ScreenShake Shake { get => shake; set => shake = value; }
        public BurstFx Burst { get => burst; set => burst = value; }
        public DamageFlashView Flash { get => flash; set => flash = value; }

        /// <summary>적의 체력(maxHp)으로 비중을 가른다: 보스(300+), 정예/장수(60+), 일반.</summary>
        public static KillWeight GetKillWeight(EnemyData data)
        {
            if (data == null) return KillWeight.Normal;
            if (data.maxHp >= 300) return KillWeight.Boss;
            if (data.maxHp >= 60) return KillWeight.Heavy;
            return KillWeight.Normal;
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();

        void Rewire(System.Action assign)
        {
            bool active = isActiveAndEnabled;
            if (active) Unsubscribe();
            assign();
            if (active) Subscribe();
        }

        void Subscribe()
        {
            Enemy.Damaged += OnEnemyDamaged;
            Enemy.Died += OnEnemyDied;

            lastHealth = playerHealth != null ? playerHealth.Current : -1f;
            if (playerHealth != null) playerHealth.Changed += OnHealthChanged;
            if (experience != null) experience.LevelUp += OnLevelUp;
            if (weapons != null) weapons.Evolved += OnEvolved;
            if (skills != null) skills.Used += OnSkillUsed;
            if (stage != null) stage.BossSpawned += OnBossSpawned;
        }

        void Unsubscribe()
        {
            Enemy.Damaged -= OnEnemyDamaged;
            Enemy.Died -= OnEnemyDied;

            if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
            if (experience != null) experience.LevelUp -= OnLevelUp;
            if (weapons != null) weapons.Evolved -= OnEvolved;
            if (skills != null) skills.Used -= OnSkillUsed;
            if (stage != null) stage.BossSpawned -= OnBossSpawned;
        }

        void OnEnemyDamaged(Enemy e, float amount)
        {
            // 처치하는 타격은 OnEnemyDied가 맡는다 (Damaged는 Alive가 꺼지기 전에 오므로 남은 체력으로 구분)
            if (burst == null || e.Hp <= 0f || burst.ActiveCount >= SparkBudget) return;
            burst.Burst(e.Position, 2, SparkColor, 3.5f, 0.25f, 0.5f);
        }

        void OnEnemyDied(Enemy e)
        {
            switch (GetKillWeight(e.Data))
            {
                case KillWeight.Boss:
                    burst?.Burst(e.Position, 36, e.Data.tint, 6f, 0.8f, 1.2f);
                    shake?.AddTrauma(0.7f);
                    break;
                case KillWeight.Heavy:
                    burst?.Burst(e.Position, 16, e.Data.tint, 5f, 0.6f, 1f);
                    shake?.AddTrauma(0.3f);
                    break;
                default:
                    burst?.Burst(e.Position, 6, e.Data.tint, 4f, 0.45f, 0.8f);
                    break;
            }
        }

        void OnHealthChanged(float current, float max)
        {
            // 체력이 줄어든 경우만 (회복/최대 체력 증가는 연출 없음)
            if (lastHealth >= 0f && current < lastHealth - 0.01f)
            {
                float lost = lastHealth - current;
                flash?.Flash(Mathf.Clamp01(0.5f + lost / Mathf.Max(1f, max) * 3f));
                shake?.AddTrauma(0.5f);
                burst?.Burst(PlayerPosition(), 8, BloodColor, 4f, 0.5f, 0.9f);
            }
            lastHealth = current;
        }

        void OnLevelUp(int level) => burst?.Ring(PlayerPosition(), 16, GoldColor, 5f, 0.6f, 1f);

        void OnEvolved(WeaponData from, WeaponData to)
        {
            burst?.Ring(PlayerPosition(), 32, GoldColor, 7f, 0.9f, 1.2f);
            shake?.AddTrauma(0.5f);
        }

        void OnSkillUsed(SkillData skill) => shake?.AddTrauma(0.4f);
        void OnBossSpawned(Enemy boss) => shake?.AddTrauma(0.7f);

        Vector2 PlayerPosition()
        {
            if (playerHealth != null) return playerHealth.transform.position;
            if (experience != null) return experience.transform.position;
            return transform.position;
        }
    }
}
