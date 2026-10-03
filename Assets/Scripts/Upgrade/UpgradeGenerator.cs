using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Player;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Upgrades
{
    /// <summary>현재 보유 상태를 보고 레벨업 선택지를 가중치 무작위로 만든다.</summary>
    public static class UpgradeGenerator
    {
        public const float HealRatio = 0.3f;

        /// <summary>선택 후보와 등장 가중치.</summary>
        struct Weighted
        {
            public UpgradeOption option;
            public float weight;
        }

        /// <summary>
        /// 구성: 진화 가능한 무기(있으면 항상 먼저) + 가중치 무작위 후보
        /// (보유 무기 강화(최대 레벨 제외) / 새 무기(보유 한도 미만, 이미 진화한 무기 제외) / 패시브(최대 레벨 제외)).
        /// 가중치는 UpgradeCatalog 의 값을 쓴다: 보유 무기 강화를 자주 보여 줘서 한 무기를 집중해 키우면 진화에 닿을 수 있다.
        /// 후보가 count보다 적으면 체력 회복 선택지를 채워 넣는다.
        /// </summary>
        public static List<UpgradeOption> Generate(UpgradeCatalog catalog, WeaponController weapons,
            PlayerStats stats, PlayerHealth health, int count = 3, int maxWeapons = 4)
        {
            float levelUpWeight = catalog != null ? catalog.weaponLevelUpWeight : 1f;
            float passiveWeight = catalog != null ? catalog.passiveWeight : 1f;
            float newWeaponWeight = catalog != null ? catalog.newWeaponWeight : 1f;

            var evolutions = BuildEvolutionOptions(catalog, weapons, stats);
            var candidates = new List<Weighted>();

            // 보유 무기 강화
            if (weapons != null)
            {
                foreach (var w in weapons.Weapons)
                {
                    if (w.IsMaxLevel) continue;
                    var weapon = w;
                    candidates.Add(new Weighted
                    {
                        weight = levelUpWeight,
                        option = new UpgradeOption
                        {
                            Kind = UpgradeKind.WeaponLevelUp,
                            Weapon = weapon.Data,
                            Title = $"{weapon.Data.displayName} Lv.{weapon.Level + 1}",
                            Description = weapon.NextLevelDescription(),
                            Apply = () => weapon.LevelUp()
                        }
                    });
                }
            }

            // 새 무기
            if (catalog != null && weapons != null && weapons.Weapons.Count < maxWeapons)
            {
                foreach (var data in catalog.weapons)
                {
                    if (data == null || weapons.Owns(data) || weapons.HasEvolved(data)) continue;
                    var wd = data;
                    candidates.Add(new Weighted
                    {
                        weight = newWeaponWeight,
                        option = new UpgradeOption
                        {
                            Kind = UpgradeKind.NewWeapon,
                            Weapon = wd,
                            Title = $"새 무기: {wd.displayName}",
                            Description = wd.description,
                            Apply = () => weapons.AddWeapon(wd)
                        }
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
                    candidates.Add(new Weighted
                    {
                        weight = passiveWeight,
                        option = new UpgradeOption
                        {
                            Kind = UpgradeKind.Passive,
                            Passive = p,
                            Title = $"{p.displayName} Lv.{next}",
                            Description = p.description,
                            Apply = () => stats.AddPassive(p)
                        }
                    });
                }
            }

            // 진화 선택지가 항상 먼저, 남는 자리를 가중치 무작위 후보로 채운다
            var result = new List<UpgradeOption>(count);
            for (int i = 0; i < evolutions.Count && result.Count < count; i++) result.Add(evolutions[i]);
            DrawWeighted(candidates, count - result.Count, result);

            // 후보 부족 시 체력 회복으로 채움
            if (result.Count < count && health != null)
            {
                result.Add(new UpgradeOption
                {
                    Kind = UpgradeKind.Heal,
                    Title = "체력 회복",
                    Description = $"최대 체력의 {HealRatio * 100f:0}% 회복",
                    Apply = () => health.Heal(health.Max * HealRatio)
                });
            }

            return result;
        }

        /// <summary>현재 상태에서 진화할 수 있는 조합 (무기 레벨 + 필요 패시브를 모두 만족).</summary>
        public static bool CanEvolve(EvolutionData evo, WeaponController weapons, PlayerStats stats)
        {
            if (evo == null || !evo.IsValid || weapons == null || stats == null) return false;
            if (weapons.Owns(evo.evolvedWeapon)) return false;

            foreach (var w in weapons.Weapons)
            {
                if (w.Data != evo.baseWeapon) continue;

                int needed = Mathf.Min(evo.requiredLevel, w.Data.maxLevel);
                return w.Level >= needed && stats.GetLevel(evo.requiredPassive) >= evo.requiredPassiveLevel;
            }
            return false;
        }

        static List<UpgradeOption> BuildEvolutionOptions(UpgradeCatalog catalog, WeaponController weapons, PlayerStats stats)
        {
            var options = new List<UpgradeOption>();
            if (catalog == null) return options;

            foreach (var evo in catalog.evolutions)
            {
                if (!CanEvolve(evo, weapons, stats)) continue;

                var e = evo;
                options.Add(new UpgradeOption
                {
                    Kind = UpgradeKind.Evolve,
                    Weapon = e.evolvedWeapon,
                    Evolution = e,
                    Title = $"진화: {e.baseWeapon.displayName} → {e.evolvedWeapon.displayName}",
                    Description = $"{e.requiredPassive.displayName}의 힘으로 무기가 진화한다.\n{e.evolvedWeapon.description}",
                    Apply = () => weapons.Evolve(e.baseWeapon, e.evolvedWeapon)
                });
            }
            return options;
        }

        /// <summary>
        /// 가중치에 비례하는 확률로 중복 없이 최대 n개를 뽑아 result 에 추가한다.
        /// 가중치가 0 이하인 후보는 뽑히지 않는다.
        /// </summary>
        static void DrawWeighted(List<Weighted> pool, int n, List<UpgradeOption> result)
        {
            for (int drawn = 0; drawn < n && pool.Count > 0; drawn++)
            {
                float total = 0f;
                for (int i = 0; i < pool.Count; i++) total += Mathf.Max(0f, pool[i].weight);
                if (total <= 0f) return;

                float roll = Random.value * total;
                int chosen = pool.Count - 1;
                float acc = 0f;
                for (int i = 0; i < pool.Count; i++)
                {
                    acc += Mathf.Max(0f, pool[i].weight);
                    if (roll <= acc && pool[i].weight > 0f) { chosen = i; break; }
                }

                result.Add(pool[chosen].option);
                pool.RemoveAt(chosen);
            }
        }
    }
}
