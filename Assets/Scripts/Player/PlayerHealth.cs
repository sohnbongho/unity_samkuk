using System;
using Samkuk.Core;
using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>플레이어 체력, 피격 후 무적 시간(깜빡임), 재생, 사망 처리.</summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        const float RegenTickSeconds = 0.5f;

        [SerializeField] float maxHp = 100f;
        [SerializeField, Tooltip("피격 후 무적 시간(초)")] float invulnerableTime = 0.5f;

        SpriteRenderer body;
        PlayerStats stats;
        float appliedBonus;
        float invulnTimer;
        float regenAccumulator;

        public float Current { get; private set; }
        /// <summary>패시브 보너스가 포함된 최대 체력.</summary>
        public float Max => maxHp + (stats != null ? stats.MaxHpBonus : 0f);
        public bool IsDead { get; private set; }
        /// <summary>접촉 피해를 받을 수 있는 상태인가 (무적/사망이면 false).</summary>
        public bool CanTakeContactDamage => !IsDead && invulnTimer <= 0f;

        /// <summary>(현재 체력, 최대 체력)</summary>
        public event Action<float, float> Changed;
        public event Action Died;

        void Awake()
        {
            body = GetComponentInChildren<SpriteRenderer>();
            stats = GetComponent<PlayerStats>();
            if (stats != null)
            {
                appliedBonus = stats.MaxHpBonus;
                stats.Changed += OnStatsChanged;
            }
            Current = Max;
        }

        void OnDestroy()
        {
            if (stats != null) stats.Changed -= OnStatsChanged;
        }

        void Start() => Changed?.Invoke(Current, Max);

        void OnStatsChanged()
        {
            float delta = stats.MaxHpBonus - appliedBonus;
            appliedBonus = stats.MaxHpBonus;
            if (delta > 0f && !IsDead) Current += delta; // 최대 체력이 늘어난 만큼 회복
            Current = Mathf.Min(Current, Max);
            Changed?.Invoke(Current, Max);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }

        /// <summary>무적 시간을 무시하고 피해를 준다.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, Max);

            if (Current <= 0f) Die();
        }

        /// <summary>접촉 피해: 무적 시간 중이면 무시하고, 적용되면 무적 시간이 시작된다.</summary>
        public bool TryContactDamage(float amount)
        {
            if (!CanTakeContactDamage) return false;
            invulnTimer = invulnerableTime;
            TakeDamage(amount);
            return true;
        }

        void Die()
        {
            IsDead = true;
            if (body != null) body.color = Color.white;

            var controller = GetComponent<PlayerController>();
            if (controller != null) controller.enabled = false;

            Died?.Invoke();
        }

        void Update()
        {
            UpdateInvulnerability();
            UpdateRegen();
        }

        void UpdateInvulnerability()
        {
            if (invulnTimer <= 0f) return;

            invulnTimer -= Time.deltaTime;
            if (body == null) return;

            if (invulnTimer <= 0f || IsDead)
                body.color = Color.white;
            else
                body.color = Mathf.FloorToInt(invulnTimer * 20f) % 2 == 0 ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        void UpdateRegen()
        {
            if (IsDead || stats == null || stats.RegenPerSecond <= 0f || Current >= Max) return;

            regenAccumulator += stats.RegenPerSecond * Time.deltaTime;
            if (regenAccumulator >= RegenTickSeconds * stats.RegenPerSecond)
            {
                Heal(regenAccumulator);
                regenAccumulator = 0f;
            }
        }
    }
}
