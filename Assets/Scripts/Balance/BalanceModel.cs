using System;
using System.Collections.Generic;
using System.Text;
using Samkuk.Data;
using Samkuk.Meta;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Balance
{
    /// <summary>
    /// 밸런스 점검용 단순 모델. 게임을 직접 돌리지 않고 데이터(스테이지, 적, 무기, 경험치 곡선, 영구 강화)만으로
    /// "한 판이 어떤 흐름이 되는가"를 어림한다 (레벨 속도, 화력 대 적 체력 유입, 골드 수급).
    ///
    /// 주의: 이 모델은 몇 가지 가정(처치율, 무기별 동시 타격 수, 평균적인 빌드)에 기대는 어림값이다.
    /// 절대적인 정답이 아니라 "수치를 바꿨을 때 흐름이 어느 쪽으로 움직이는가"와 "의도한 범위를 벗어나지 않았는가"를
    /// 확인하는 용도이며, 최종 체감은 직접 플레이로 확인해야 한다.
    /// </summary>
    public static class BalanceModel
    {
        /// <summary>진화에 필요한 기본 무기 레벨 (모든 진화 조합의 기준값. 1분 스테이지에서 도달 가능하도록 낮게 잡음).</summary>
        public const int EvolutionRequiredLevel = 3;

        /// <summary>피격 후 무적 시간(초). PlayerHealth 의 기본값과 같다.</summary>
        public const float InvulnerableSeconds = 0.5f;

        // 무기가 평균적으로 한 번에 맞히는 적 수 (밀집한 적 무리를 가정한 값)
        const float ArrowTargetsCap = 1.5f;
        const float SlashTargets = 2f;
        const float ThrustTargets = 2.5f;
        const float FireTargets = 3f;
        const float LightningTargets = 2f;
        const float RainTargets = 1.2f;
        const float NovaTargets = 6f;
        const float OrbitContactRatio = 0.5f;

        // 빌드 가정: 레벨업 선택의 60%는 무기(앞의 둘은 새 무기), 40%는 패시브. 패시브 한 번은 평균 공격력 +4%, 쿨다운 -2%.
        const float WeaponPickRatio = 0.6f;
        const float PassiveDamagePerPick = 0.04f;
        const float PassiveCooldownPerPick = 0.02f;

        // ───────────────────────── 스테이지 ─────────────────────────

        /// <summary>시각 t에 적용 중인 웨이브 (없으면 null).</summary>
        public static Wave WaveAt(StageData stage, float t)
        {
            Wave current = null;
            float best = float.NegativeInfinity;
            foreach (var w in stage.waves)
            {
                if (w.startTime <= t && w.startTime >= best) { current = w; best = w.startTime; }
            }
            return current;
        }

        /// <summary>웨이브의 적 가중 평균값 (예: 체력, 접촉 피해, 경험치).</summary>
        public static float WaveAverage(Wave wave, Func<EnemyData, float> selector)
        {
            float total = 0f, sum = 0f;
            foreach (var e in wave.enemies)
            {
                if (e.data == null || e.weight <= 0f) continue;
                total += e.weight;
                sum += e.weight * selector(e.data);
            }
            return total > 0f ? sum / total : 0f;
        }

        /// <summary>시각 t에 초당 새로 나타나는 적의 체력 합 (스폰 속도 × 평균 체력).</summary>
        public static float HpInflowPerSecond(StageData stage, float t)
        {
            var w = WaveAt(stage, t);
            return w == null ? 0f : w.spawnPerSecond * WaveAverage(w, e => e.maxHp);
        }

        /// <summary>플레이어가 처치한다고 가정하는 비율 (판이 후반으로 갈수록 몰려서 낮아진다).</summary>
        public static float KillRatio(float t, float duration)
        {
            float f = duration > 0f ? t / duration : 0f;
            if (f < 0.25f) return 0.95f;
            if (f < 0.5f) return 0.9f;
            if (f < 0.75f) return 0.85f;
            return 0.75f;
        }

        // ───────────────────────── 경험치 / 레벨 ─────────────────────────

        /// <summary>초마다 얻는 경험치의 기대값 (처치율 반영, 웨이브 스폰 + 이벤트 적).</summary>
        public static float[] ExpPerSecond(StageData stage, float expMultiplier = 1f)
        {
            int seconds = Mathf.CeilToInt(stage.duration);
            var exp = new float[seconds];
            for (int t = 0; t < seconds; t++)
            {
                float kill = KillRatio(t, stage.duration);
                var w = WaveAt(stage, t);
                if (w != null) exp[t] += w.spawnPerSecond * WaveAverage(w, e => e.expReward) * kill;

                foreach (var ev in stage.events)
                {
                    if (ev.enemy != null && (int)ev.time == t)
                        exp[t] += Mathf.Max(1, ev.count) * ev.enemy.expReward * kill;
                }
                exp[t] *= expMultiplier;
            }
            return exp;
        }

        /// <summary>경험치 흐름에서 레벨업이 일어나는 시각(초) 목록.</summary>
        public static List<int> LevelUpTimes(float[] expPerSecond)
        {
            var times = new List<int>();
            float current = 0f;
            int level = 1;
            for (int t = 0; t < expPerSecond.Length; t++)
            {
                current += expPerSecond[t];
                while (current >= PlayerExperience.RequiredFor(level))
                {
                    current -= PlayerExperience.RequiredFor(level);
                    level++;
                    times.Add(t);
                }
            }
            return times;
        }

        // ───────────────────────── 무기 화력 ─────────────────────────

        /// <summary>
        /// 무기 하나의 초당 피해(DPS) 어림값. 레벨 성장(피해 +%, 쿨다운 -%, 수량)은 실제 Weapon 과 같은 식이고,
        /// 한 번에 맞히는 적 수는 무기 종류별 가정치를 쓴다.
        /// </summary>
        public static float WeaponDps(WeaponData w, int level, float damageMult = 1f, float cooldownMult = 1f)
        {
            level = Mathf.Max(1, level);
            float damage = w.damage * (1f + w.damagePerLevel * (level - 1)) * damageMult;
            float cooldown = Mathf.Max(0.05f, w.cooldown * Mathf.Max(0.3f, 1f - w.cooldownReductionPerLevel * (level - 1)) * cooldownMult);
            int count = w.count + (w.levelsPerExtraCount > 0 ? (level - 1) / w.levelsPerExtraCount : 0);
            float tick = Mathf.Max(0.05f, w.tickInterval);

            switch (w.type)
            {
                case WeaponType.Arrow: return damage * count * Mathf.Min(w.pierce, ArrowTargetsCap) / cooldown;
                case WeaponType.Slash: return damage * count * SlashTargets / cooldown;
                case WeaponType.Orbit: return damage * count / tick * OrbitContactRatio;
                case WeaponType.Thrust: return damage * count * ThrustTargets / cooldown;
                case WeaponType.FireZone: return damage / tick * count * FireTargets * Mathf.Min(1f, w.duration / cooldown);
                case WeaponType.Lightning: return damage * count * LightningTargets / cooldown;
                case WeaponType.Rain: return damage * count * RainTargets / cooldown;
                case WeaponType.Nova: return damage * NovaTargets / cooldown;
                default: return 0f;
            }
        }

        /// <summary>카탈로그 무기 중 1레벨 화력이 중간쯤인 둘 (평균적인 빌드가 고르는 무기로 가정).</summary>
        public static List<WeaponData> MedianCatalogWeapons(IEnumerable<WeaponData> catalogWeapons)
        {
            var list = new List<WeaponData>(catalogWeapons);
            list.RemoveAll(w => w == null);
            list.Sort((a, b) => WeaponDps(a, 1).CompareTo(WeaponDps(b, 1)));
            var picked = new List<WeaponData>();
            if (list.Count == 0) return picked;
            int mid = list.Count / 2;
            picked.Add(list[Mathf.Clamp(mid - 1, 0, list.Count - 1)]);
            if (list.Count > 1) picked.Add(list[Mathf.Clamp(mid, 0, list.Count - 1)]);
            return picked;
        }

        /// <summary>
        /// 레벨업을 levelUps 번 한 "평균적으로 집중해서 키운" 빌드의 화력. 장수 시작 무기 + 중간급 새 무기 둘을 쓰고,
        /// 레벨업의 일부는 패시브 몫으로 돌린다. 장수 시작 무기에 업그레이드의 60%가 모인다.
        /// </summary>
        public static float NominalBuildDps(int levelUps, WeaponData heroWeapon, IReadOnlyList<WeaponData> extraWeapons)
        {
            int weaponPicks = Mathf.RoundToInt(WeaponPickRatio * levelUps);
            int passivePicks = levelUps - weaponPicks;

            var owned = new List<WeaponData> { heroWeapon };
            for (int i = 0; i < extraWeapons.Count && i < weaponPicks; i++) owned.Add(extraWeapons[i]);

            var levels = new int[owned.Count];
            for (int i = 0; i < levels.Length; i++) levels[i] = 1;

            int upgrades = Mathf.Max(0, weaponPicks - (owned.Count - 1));
            for (int k = 0; k < upgrades; k++)
            {
                int target = (k % 5) < 3 ? 0 : (1 + (k % 2)) % owned.Count;
                levels[target] = Mathf.Min(owned[target].maxLevel, levels[target] + 1);
            }

            float damageMult = 1f + PassiveDamagePerPick * passivePicks;
            float cooldownMult = 1f - PassiveCooldownPerPick * passivePicks;
            float total = 0f;
            for (int i = 0; i < owned.Count; i++)
                total += WeaponDps(owned[i], levels[i], damageMult, cooldownMult);
            return total;
        }

        /// <summary>시각 t의 압박비 = 평균 빌드 화력 / 초당 적 체력 유입. 1보다 크면 화력이 앞서고, 작으면 적이 쌓인다.</summary>
        public static float PressureRatio(StageData stage, float t, IReadOnlyList<int> levelUpTimes,
            WeaponData heroWeapon, IReadOnlyList<WeaponData> extraWeapons)
        {
            int levelUps = 0;
            foreach (int lt in levelUpTimes) if (lt <= t) levelUps++;

            float inflow = HpInflowPerSecond(stage, t);
            return inflow > 0f ? NominalBuildDps(levelUps, heroWeapon, extraWeapons) / inflow : float.PositiveInfinity;
        }

        // ───────────────────────── 생존 ─────────────────────────

        /// <summary>한 종류의 적에게 계속 둘러싸였을 때(피격 무적 시간마다 한 번씩 맞음) 체력 hp가 바닥나기까지의 초.</summary>
        public static float SecondsToDieUnderContact(float hp, float contactDamage)
        {
            if (contactDamage <= 0f) return float.PositiveInfinity;
            return hp / (contactDamage / InvulnerableSeconds);
        }

        // ───────────────────────── 골드 ─────────────────────────

        /// <summary>영구 강화를 모두 최대로 올리는 데 드는 골드 합계.</summary>
        public static int TotalMetaCost(MetaCatalog catalog)
        {
            int total = 0;
            foreach (var u in catalog.upgrades)
            {
                if (u == null) continue;
                for (int level = 0; level < u.maxLevel; level++) total += u.CostForLevel(level);
            }
            return total;
        }

        /// <summary>1레벨을 사는 데 드는 가장 싼 영구 강화 비용.</summary>
        public static int CheapestMetaCost(MetaCatalog catalog)
        {
            int cheapest = int.MaxValue;
            foreach (var u in catalog.upgrades)
                if (u != null) cheapest = Mathf.Min(cheapest, u.CostForLevel(0));
            return cheapest;
        }

        // ───────────────────────── 보고서 ─────────────────────────

        /// <summary>현재 데이터로 계산한 밸런스 요약 (콘솔 출력용).</summary>
        public static string Report(StageData stage, IReadOnlyList<WeaponData> heroWeapons,
            UpgradeCatalog upgrades, MetaCatalog meta)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Samkuk] 밸런스 보고서 (어림 모델 — 방향 확인용, 최종 체감은 직접 플레이로)");

            var exp = ExpPerSecond(stage);
            var levelUps = LevelUpTimes(exp);
            sb.AppendLine($"- 경험치: 처치율 반영 총 {SumOf(exp):0}, {stage.duration:0}초 동안 레벨업 {levelUps.Count}회 (최종 Lv.{levelUps.Count + 1})");
            sb.AppendLine($"  레벨업 시각: {string.Join(", ", levelUps)}");

            var extras = MedianCatalogWeapons(upgrades.weapons);
            sb.AppendLine("- 압박비(평균 빌드 화력 / 초당 적 체력 유입, 1 이상이면 화력 우세):");
            foreach (var w in stage.waves)
            {
                float t = w.startTime + 5f;
                if (t >= stage.duration) t = w.startTime;
                float sum = 0f;
                foreach (var hw in heroWeapons) sum += PressureRatio(stage, t, levelUps, hw, extras);
                float avg = heroWeapons.Count > 0 ? sum / heroWeapons.Count : 0f;
                sb.AppendLine($"  {w.name} ({w.startTime:0}초~): 유입 {HpInflowPerSecond(stage, t):0}/초, 압박비 {avg:0.00}");
            }

            sb.AppendLine($"- 영구 강화: 전부 최대로 {TotalMetaCost(meta)}G, 가장 싼 1레벨 {CheapestMetaCost(meta)}G");
            sb.AppendLine($"  골드 예시: 30초 사망(처치 80) {RunRewards.Calculate(80, 30f, false)}G, 클리어(처치 250) {RunRewards.Calculate(250, stage.duration, true)}G");
            return sb.ToString();
        }

        static float SumOf(float[] values)
        {
            float s = 0f;
            foreach (float v in values) s += v;
            return s;
        }
    }
}
