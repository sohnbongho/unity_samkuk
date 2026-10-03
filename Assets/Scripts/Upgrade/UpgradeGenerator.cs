using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Player;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Upgrades
{
    /// <summary>현재 보유 상태를 보고 레벨업 선택지를 무작위로 만든다.</summary>
    public static class UpgradeGenerator
    {
        public const float HealRatio = 0.3f;

        /// <summary>
        /// 후보: 보유 무기 강화(최대 레벨 제외) + 새 무기(보유 한도 미만일 때) + 패시브(최대 레벨 제외).
        /// 후보가 count보다 적으면 체력 회복 선택지를 채워 넣는다.
        /// </summary>
        public static List<UpgradeOption> Generate(UpgradeCatalog catalog, WeaponController weapons,
            PlayerStats stats, PlayerHealth health, int count = 3, int maxWeapons = 4)
        {
            var candidates = new List<UpgradeOption>();

            // 보유 무기 강화
            if (weapons != null)
            {
                foreach (var w in weapons.Weapons)
                {
                    if (w.IsMaxLevel) continue;
                    var weapon = w;
                    candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.WeaponLevelUp,
                        Weapon = weapon.Data,
                        Title = $"{weapon.Data.displayName} Lv.{weapon.Level + 1}",
                        Description = weapon.NextLevelDescription(),
                        Apply = () => weapon.LevelUp()
                    });
                }
            }

            // 새 무기
            if (catalog != null && weapons != null && weapons.Weapons.Count < maxWeapons)
            {
                foreach (var data in catalog.weapons)
                {
                    if (data == null || Owns(weapons, data)) continue;
                    var wd = data;
                    candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.NewWeapon,
                        Weapon = wd,
                        Title = $"새 무기: {wd.displayName}",
                        Description = wd.description,
                        Apply = () => weapons.AddWeapon(wd)
                    });
                }
            }

            // 패시브
            if (catalog != null && stats != null)
            {
                foreach (var passive in catalog.passives)
                {
                    if (!stats.CanUpgrade(passive)) continue;
                    var p = passive;
                    int next = stats.GetLevel(p) + 1;
                    candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.Passive,
                        Passive = p,
                        Title = $"{p.displayName} Lv.{next}",
                        Description = p.description,
                        Apply = () => stats.AddPassive(p)
                    });
                }
            }

            Shuffle(candidates);
            if (candidates.Count > count) candidates.RemoveRange(count, candidates.Count - count);

            // 후보 부족 시 체력 회복으로 채움
            if (candidates.Count < count && health != null)
            {
                candidates.Add(new UpgradeOption
                {
                    Kind = UpgradeKind.Heal,
                    Title = "체력 회복",
                    Description = $"최대 체력의 {HealRatio * 100f:0}% 회복",
                    Apply = () => health.Heal(health.Max * HealRatio)
                });
            }

            return candidates;
        }

        static bool Owns(WeaponController weapons, WeaponData data)
        {
            foreach (var w in weapons.Weapons)
                if (w.Data == data) return true;
            return false;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
