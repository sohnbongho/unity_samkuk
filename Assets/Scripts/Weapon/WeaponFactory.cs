using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>무기 종류(WeaponType)에 맞는 Weapon 컴포넌트를 붙여 준다. 플레이어와 아군이 함께 쓴다.</summary>
    public static class WeaponFactory
    {
        /// <summary>go 에 type 에 맞는 무기 컴포넌트를 추가한다. 지원하지 않는 종류면 null.</summary>
        public static Weapon Attach(GameObject go, WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Arrow: return go.AddComponent<ArrowWeapon>();
                case WeaponType.Slash: return go.AddComponent<SlashWeapon>();
                case WeaponType.Orbit: return go.AddComponent<OrbitWeapon>();
                case WeaponType.Thrust: return go.AddComponent<ThrustWeapon>();
                case WeaponType.FireZone: return go.AddComponent<FireZoneWeapon>();
                case WeaponType.Lightning: return go.AddComponent<LightningWeapon>();
                case WeaponType.Rain: return go.AddComponent<RainWeapon>();
                case WeaponType.Nova: return go.AddComponent<NovaWeapon>();
                default: return null;
            }
        }
    }
}
