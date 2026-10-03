using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>무기 공통 기반. 레벨에 따른 능력치 계산을 제공한다.</summary>
    public abstract class Weapon : MonoBehaviour
    {
        public WeaponData Data { get; private set; }
        public int Level { get; private set; } = 1;

        protected PlayerController Owner { get; private set; }
        protected EnemyManager Enemies { get; private set; }
        protected WeaponController Controller { get; private set; }

        public float Damage => Data.damage * (1f + Data.damagePerLevel * (Level - 1));

        public float Cooldown =>
            Data.cooldown * Mathf.Max(0.3f, 1f - Data.cooldownReductionPerLevel * (Level - 1));

        public int Count =>
            Data.count + (Data.levelsPerExtraCount > 0 ? (Level - 1) / Data.levelsPerExtraCount : 0);

        public bool IsMaxLevel => Level >= Data.maxLevel;

        public void Initialize(WeaponData data, WeaponController controller, PlayerController owner, EnemyManager enemies)
        {
            Data = data;
            Controller = controller;
            Owner = owner;
            Enemies = enemies;
            Level = 1;
            OnInitialized();
        }

        public bool LevelUp()
        {
            if (IsMaxLevel) return false;
            Level++;
            OnLevelChanged();
            return true;
        }

        /// <summary>적 매니저를 지연 연결할 수 있도록 갱신.</summary>
        internal void SetEnemies(EnemyManager enemies) => Enemies = enemies;

        protected virtual void OnInitialized() { }
        protected virtual void OnLevelChanged() { }

        protected Vector2 OwnerPosition => Owner.transform.position;
    }
}
