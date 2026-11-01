using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 장수 개성(15-7): 서바이버 전투의 장수 데이터(<see cref="HeroData"/>)를 야전의 장수 부대 값으로 옮긴다.
    /// 공격력 배율 → 장수 부대 피해 배율, 이동 속도 배율 → 이동 속도 배율, 최대 체력 보정(기본 100 기준 ±) → 병사 수 배율.
    /// 순수 모델(<see cref="FieldSetup"/>)은 유니티를 모르므로 이 다리만 유니티 쪽에 둔다. 무기 연출은 <see cref="FieldWeaponShow"/>.
    /// </summary>
    public static class FieldHeroLink
    {
        /// <summary>체력 보정이 아무리 낮아도 병사 수는 이 배율 밑으로 내려가지 않는다.</summary>
        public const float MinSoldierMul = 0.2f;
        /// <summary>서바이버 전투의 기본 최대 체력 (체력 보정의 기준).</summary>
        public const float BaseHp = 100f;

        public static float SoldierMul(HeroData hero) => hero == null ? 1f : Mathf.Max(MinSoldierMul, (BaseHp + hero.maxHpBonus) / BaseHp);

        /// <summary>장수 값을 시작 조건에 넣는다. hero 가 없으면 모두 1(개성 없음).</summary>
        public static void Apply(FieldSetup setup, HeroData hero)
        {
            if (setup == null) return;
            setup.playerGeneralDamageMul = hero != null ? Mathf.Max(0.1f, hero.damageMultiplier) : 1f;
            setup.playerGeneralSpeedMul = hero != null ? Mathf.Max(0.1f, hero.moveSpeedMultiplier) : 1f;
            setup.playerGeneralSoldierMul = SoldierMul(hero);
            if (hero != null && !string.IsNullOrEmpty(hero.displayName)) setup.playerGeneralName = hero.displayName;
        }

        /// <summary>준비 화면·띠에 보여 줄 한 줄: 야전에서 쓰는 값만 (경험치·획득 범위는 야전에 없다).</summary>
        public static string Summary(HeroData hero)
        {
            if (hero == null) return "개성 없음 (공격 x1, 이동 x1, 병사 x1)";
            string weapon = hero.startingWeapon != null ? hero.startingWeapon.displayName : "없음";
            return $"공격 x{hero.damageMultiplier:0.##}  이동 x{hero.moveSpeedMultiplier:0.##}  병사 x{SoldierMul(hero):0.##}  무기 {weapon}";
        }
    }
}
