using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Meta
{
    /// <summary>영구 강화로 얻는 보너스 합계.</summary>
    public struct MetaBonuses
    {
        public float maxHp;
        public float damage;
        public float moveSpeed;
        public float regen;
        public float pickupRadius;
        public float expGain;
        public float goldGain;
    }

    /// <summary>영구 강화의 구매/비용/보너스 계산 (저장 데이터를 직접 다루는 순수 로직).</summary>
    public static class MetaProgression
    {
        /// <summary>저장 파일에서 강화를 식별하는 키 (에셋 이름).</summary>
        public static string Key(MetaUpgradeData upgrade) => upgrade.name;

        public static int GetLevel(SaveData save, MetaUpgradeData upgrade) =>
            Mathf.Clamp(save.GetUpgradeLevel(Key(upgrade)), 0, upgrade.maxLevel);

        public static bool IsMaxLevel(SaveData save, MetaUpgradeData upgrade) =>
            GetLevel(save, upgrade) >= upgrade.maxLevel;

        /// <summary>다음 레벨의 비용. 이미 최대 레벨이면 -1.</summary>
        public static int NextCost(SaveData save, MetaUpgradeData upgrade) =>
            IsMaxLevel(save, upgrade) ? -1 : upgrade.CostForLevel(GetLevel(save, upgrade));

        public static bool CanPurchase(SaveData save, MetaUpgradeData upgrade)
        {
            if (save == null || upgrade == null || IsMaxLevel(save, upgrade)) return false;
            return save.gold >= NextCost(save, upgrade);
        }

        /// <summary>골드가 충분하고 최대 레벨이 아니면 구매한다. 성공하면 true.</summary>
        public static bool TryPurchase(SaveData save, MetaUpgradeData upgrade)
        {
            if (!CanPurchase(save, upgrade)) return false;

            int cost = NextCost(save, upgrade);
            save.gold -= cost;
            save.SetUpgradeLevel(Key(upgrade), GetLevel(save, upgrade) + 1);
            return true;
        }

        /// <summary>보유한 영구 강화들의 효과를 합산한다.</summary>
        public static MetaBonuses ComputeBonuses(MetaCatalog catalog, SaveData save)
        {
            var b = new MetaBonuses();
            if (catalog == null || save == null) return b;

            foreach (var u in catalog.upgrades)
            {
                if (u == null) continue;
                float v = u.valuePerLevel * GetLevel(save, u);
                switch (u.stat)
                {
                    case MetaStat.MaxHp: b.maxHp += v; break;
                    case MetaStat.Damage: b.damage += v; break;
                    case MetaStat.MoveSpeed: b.moveSpeed += v; break;
                    case MetaStat.Regen: b.regen += v; break;
                    case MetaStat.PickupRadius: b.pickupRadius += v; break;
                    case MetaStat.ExpGain: b.expGain += v; break;
                    case MetaStat.GoldGain: b.goldGain += v; break;
                }
            }
            return b;
        }
    }

    /// <summary>한 판이 끝났을 때 받는 골드 계산.</summary>
    public static class RunRewards
    {
        public const float GoldPerKill = 0.5f;
        public const float GoldPerSecond = 1f;
        public const int ClearBonus = 50;

        /// <summary>(처치 수 × 0.5 + 생존 초 × 1 + 클리어 보너스) × 골드 배율, 소수점 버림.</summary>
        public static int Calculate(int kills, float seconds, bool cleared, float goldMultiplier = 1f)
        {
            float raw = Mathf.Max(0, kills) * GoldPerKill + Mathf.Max(0f, seconds) * GoldPerSecond + (cleared ? ClearBonus : 0);
            return Mathf.Max(0, Mathf.FloorToInt(raw * Mathf.Max(0f, goldMultiplier)));
        }
    }
}
