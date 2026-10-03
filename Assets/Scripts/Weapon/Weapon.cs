using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>무기 공통 기반. 레벨과 패시브(PlayerStats)에 따른 능력치 계산을 제공한다.</summary>
    public abstract class Weapon : MonoBehaviour
    {
        public WeaponData Data { get; private set; }
        public int Level { get; private set; } = 1;

        protected PlayerController Owner { get; private set; }
        protected EnemyManager Enemies { get; private set; }
        protected WeaponController Controller { get; private set; }
        protected PlayerStats Stats { get; private set; }

        /// <summary>일회성 이펙트 재생기 (없을 수 있음).</summary>
        protected WeaponFx Fx => Controller != null ? Controller.Fx : null;

        public float Damage =>
            Data.damage * (1f + Data.damagePerLevel * (Level - 1)) * (Stats != null ? Stats.DamageMultiplier : 1f);

        public float Cooldown =>
            Data.cooldown * Mathf.Max(0.3f, 1f - Data.cooldownReductionPerLevel * (Level - 1))
                          * (Stats != null ? Stats.CooldownMultiplier : 1f);

        public int Count =>
            Data.count + (Data.levelsPerExtraCount > 0 ? (Level - 1) / Data.levelsPerExtraCount : 0);

        public bool IsMaxLevel => Level >= Data.maxLevel;

        public void Initialize(WeaponData data, WeaponController controller, PlayerController owner, EnemyManager enemies)
        {
            Data = data;
            Controller = controller;
            Owner = owner;
            Enemies = enemies;
            Stats = owner != null ? owner.GetComponent<PlayerStats>() : null;
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

        /// <summary>다음 레벨에서 좋아지는 점을 설명하는 문자열 (레벨업 선택지용).</summary>
        public string NextLevelDescription()
        {
            var parts = new List<string>();
            if (Data.damagePerLevel > 0f)
                parts.Add($"공격력 +{Data.damagePerLevel * 100f:0}%");
            if (Data.cooldownReductionPerLevel > 0f && Data.type != WeaponType.Orbit)
                parts.Add($"쿨다운 -{Data.cooldownReductionPerLevel * 100f:0}%");
            if (Data.levelsPerExtraCount > 0 && Level % Data.levelsPerExtraCount == 0)
                parts.Add("수량 +1");
            return string.Join("\n", parts);
        }

        /// <summary>적 매니저를 지연 연결할 수 있도록 갱신.</summary>
        internal void SetEnemies(EnemyManager enemies) => Enemies = enemies;

        protected virtual void OnInitialized() { }
        protected virtual void OnLevelChanged() { }

        protected Vector2 OwnerPosition => Owner.transform.position;

        /// <summary>무기 데이터의 넉백 값이 있으면 origin 반대 방향으로 적을 민다 (살아있는 적만).</summary>
        protected void Knock(Enemy enemy, Vector2 origin)
        {
            if (Data.knockback <= 0f || !enemy.Alive) return;

            Vector2 away = enemy.Position - origin;
            if (away.sqrMagnitude < 1e-6f) away = Vector2.right;
            enemy.Knockback(away.normalized * Data.knockback);
        }
    }
}
