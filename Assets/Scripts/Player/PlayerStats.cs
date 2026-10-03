using System;
using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>획득한 패시브의 누적 효과(배율/보너스)를 계산해 제공한다.</summary>
    public class PlayerStats : MonoBehaviour
    {
        readonly Dictionary<PassiveData, int> levels = new Dictionary<PassiveData, int>();

        public float DamageMultiplier { get; private set; } = 1f;
        public float CooldownMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float PickupRadiusMultiplier { get; private set; } = 1f;
        public float ExpMultiplier { get; private set; } = 1f;
        public float MaxHpBonus { get; private set; }
        public float RegenPerSecond { get; private set; }

        /// <summary>패시브가 바뀔 때마다 호출된다.</summary>
        public event Action Changed;

        public IReadOnlyDictionary<PassiveData, int> Levels => levels;
        public int GetLevel(PassiveData passive) => levels.TryGetValue(passive, out int lv) ? lv : 0;
        public bool CanUpgrade(PassiveData passive) => passive != null && GetLevel(passive) < passive.maxLevel;

        public bool AddPassive(PassiveData passive)
        {
            if (!CanUpgrade(passive)) return false;
            levels[passive] = GetLevel(passive) + 1;
            Recalculate();
            return true;
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

            DamageMultiplier = 1f + damage;
            CooldownMultiplier = Mathf.Max(0.3f, 1f - cooldown);
            MoveSpeedMultiplier = 1f + speed;
            PickupRadiusMultiplier = 1f + pickup;
            ExpMultiplier = 1f + exp;
            MaxHpBonus = maxHp;
            RegenPerSecond = regen;
            Changed?.Invoke();
        }
    }
}
