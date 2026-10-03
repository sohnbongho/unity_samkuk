using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Heroes;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.Stages;
using Samkuk.Upgrades;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Audio
{
    /// <summary>게임 이벤트(피격, 처치, 레벨업, 진화, 스킬, 보스 등장, 종료...)를 효과음에 연결한다.</summary>
    public class SfxHooks : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] PlayerExperience experience;
        [SerializeField] WeaponController weapons;
        [SerializeField] SkillController skills;
        [SerializeField] StageController stage;
        [SerializeField] GameManager game;
        [SerializeField] LevelUpController levelUp;
        [SerializeField] HeroSelectController hero;

        float lastHealth = -1f;

        public PlayerHealth PlayerHealth { get => playerHealth; set => Rewire(() => playerHealth = value); }
        public PlayerExperience Experience { get => experience; set => Rewire(() => experience = value); }
        public WeaponController Weapons { get => weapons; set => Rewire(() => weapons = value); }
        public SkillController Skills { get => skills; set => Rewire(() => skills = value); }
        public StageController Stage { get => stage; set => Rewire(() => stage = value); }
        public GameManager Game { get => game; set => Rewire(() => game = value); }
        public LevelUpController LevelUp { get => levelUp; set => Rewire(() => levelUp = value); }
        public HeroSelectController Hero { get => hero; set => Rewire(() => hero = value); }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();

        /// <summary>참조를 바꿀 때 이전 구독을 해제하고 새 대상에 다시 구독한다.</summary>
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

            // 구독 시점의 체력을 기준으로 삼는다 (PlayerHealth.Start 의 첫 알림보다 늦게 켜져도 첫 피격음이 빠지지 않도록)
            lastHealth = playerHealth != null ? playerHealth.Current : -1f;
            if (playerHealth != null) playerHealth.Changed += OnHealthChanged;
            if (experience != null) experience.LevelUp += OnLevelUp;
            if (weapons != null) weapons.Evolved += OnEvolved;
            if (skills != null) skills.Used += OnSkillUsed;
            if (stage != null) stage.BossSpawned += OnBossSpawned;
            if (game != null) game.Finished += OnFinished;
            if (levelUp != null) levelUp.Chosen += OnChosen;
            if (hero != null) hero.HeroSelected += OnHeroSelected;
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
            if (game != null) game.Finished -= OnFinished;
            if (levelUp != null) levelUp.Chosen -= OnChosen;
            if (hero != null) hero.HeroSelected -= OnHeroSelected;
        }

        void OnEnemyDamaged(Enemy e, float amount)
        {
            // 처치되는 타격은 Kill 소리만 낸다. Damaged는 Alive가 false가 되기 전에 오므로 남은 체력으로 구분한다.
            if (e.Hp > 0f) AudioManager.Play(SfxId.Hit);
        }

        void OnEnemyDied(Enemy e) => AudioManager.Play(SfxId.Kill);

        void OnHealthChanged(float current, float max)
        {
            // 체력이 줄어든 경우만 피격음 (회복/최대 체력 증가는 소리 없음)
            if (lastHealth >= 0f && current < lastHealth - 0.01f && current > 0f) AudioManager.Play(SfxId.PlayerHurt);
            lastHealth = current;
        }

        void OnLevelUp(int level) => AudioManager.Play(SfxId.LevelUp);
        void OnEvolved(WeaponData from, WeaponData to) => AudioManager.Play(SfxId.Evolve);
        void OnSkillUsed(SkillData skill) => AudioManager.Play(SfxId.Skill);
        void OnBossSpawned(Enemy boss) => AudioManager.Play(SfxId.Boss);
        void OnFinished(bool cleared) => AudioManager.Play(cleared ? SfxId.Victory : SfxId.Defeat);
        void OnChosen(UpgradeOption option) { if (option.Kind != UpgradeKind.Evolve) AudioManager.Play(SfxId.Click); }
        void OnHeroSelected(HeroData hero) => AudioManager.Play(SfxId.Click);
    }
}
