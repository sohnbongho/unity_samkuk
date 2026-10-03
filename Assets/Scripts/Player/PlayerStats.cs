using System;
using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>
    /// 최종 능력치(배율/보너스)를 계산해 제공한다.
    /// 구성: 패시브(레벨 누적) × 장수 보정 × 시간제 버프.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        class Buff
        {
            public float damage, cooldown, speed, remaining;
        }

        readonly Dictionary<PassiveData, int> levels = new Dictionary<PassiveData, int>();
        readonly List<Buff> buffs = new List<Buff>();

        float heroDamage = 1f, heroSpeed = 1f, heroExp = 1f, heroPickup = 1f, heroMaxHp;
        Meta.MetaBonuses meta;

        public float DamageMultiplier { get; private set; } = 1f;
        public float CooldownMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float PickupRadiusMultiplier { get; private set; } = 1f;
        public float ExpMultiplier { get; private set; } = 1f;
        public float MaxHpBonus { get; private set; }
        public float RegenPerSecond { get; private set; }

        /// <summary>능력치가 바뀔 때마다 호출된다.</summary>
        public event Action Changed;

        public IReadOnlyDictionary<PassiveData, int> Levels => levels;
        public int ActiveBuffCount => buffs.Count;
        public int GetLevel(PassiveData passive) => levels.TryGetValue(passive, out int lv) ? lv : 0;
        public bool CanUpgrade(PassiveData passive) => passive != null && GetLevel(passive) < passive.maxLevel;

        public bool AddPassive(PassiveData passive)
        {
            if (!CanUpgrade(passive)) return false;
            levels[passive] = GetLevel(passive) + 1;
            Recalculate();
            return true;
        }

        /// <summary>영구 강화(메타) 보너스를 적용한다. 장수 보정/패시브와 곱해진다.</summary>
        public void ApplyMeta(Meta.MetaBonuses bonuses)
        {
            meta = bonuses;
            Recalculate();
        }

        /// <summary>장수의 기본 보정치를 적용한다 (null이면 보정 없음).</summary>
        public void ApplyHero(HeroData hero)
        {
            heroDamage = hero != null ? hero.damageMultiplier : 1f;
            heroSpeed = hero != null ? hero.moveSpeedMultiplier : 1f;
            heroExp = hero != null ? hero.expMultiplier : 1f;
            heroPickup = hero != null ? hero.pickupRadiusMultiplier : 1f;
            heroMaxHp = hero != null ? hero.maxHpBonus : 0f;
            Recalculate();
        }

        /// <summary>
        /// 시간제 버프를 추가한다. damage/speed는 증가 비율, cooldown은 감소 비율(0.3 = 30% 단축).
        /// </summary>
        public void AddBuff(float damageBonus, float cooldownReduction, float speedBonus, float duration)
        {
            if (duration <= 0f) return;
            buffs.Add(new Buff
            {
                damage = damageBonus, cooldown = cooldownReduction, speed = speedBonus, remaining = duration
            });
            Recalculate();
        }

        void Update()
        {
            if (buffs.Count == 0) return;

            bool expired = false;
            float dt = Time.deltaTime;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                buffs[i].remaining -= dt;
                if (buffs[i].remaining <= 0f)
                {
                    buffs.RemoveAt(i);
                    expired = true;
                }
            }
            if (expired) Recalculate();
        }

        void Recalculate()
        {
            float damage = 0f, cooldown = 0f, speed = 0f, pickup = 0f, exp = 0f, maxHp = 0f, regen = 0f;

            foreach (var kv in levels)
            {
                float v = kv.Key.valuePerLevel * kv.Value;
                switch (kv.Key.type)
                {
                    case PassiveType.Damage: damage += v; break;
                    case PassiveType.Cooldown: cooldown += v; break;
                    case PassiveType.MoveSpeed: speed += v; break;
                    case PassiveType.PickupRadius: pickup += v; break;
                    case PassiveType.ExpGain: exp += v; break;
                    case PassiveType.MaxHp: maxHp += v; break;
                    case PassiveType.Regen: regen += v; break;
                }
            }

            float buffDamage = 1f, buffCooldown = 1f, buffSpeed = 1f;
            foreach (var b in buffs)
            {
                buffDamage *= 1f + b.damage;
                buffCooldown *= 1f - b.cooldown;
                buffSpeed *= 1f + b.speed;
            }

            DamageMultiplier = (1f + damage) * heroDamage * (1f + meta.damage) * buffDamage;
            CooldownMultiplier = Mathf.Max(0.3f, (1f - cooldown) * buffCooldown);
            MoveSpeedMultiplier = (1f + speed) * heroSpeed * (1f + meta.moveSpeed) * buffSpeed;
            PickupRadiusMultiplier = (1f + pickup) * heroPickup * (1f + meta.pickupRadius);
            ExpMultiplier = (1f + exp) * heroExp * (1f + meta.expGain);
            MaxHpBonus = maxHp + heroMaxHp + meta.maxHp;
            RegenPerSecond = regen + meta.regen;
            Changed?.Invoke();
        }
    }
}
