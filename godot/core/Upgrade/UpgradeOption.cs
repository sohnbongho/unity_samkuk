using System;
using Samkuk.Data;

namespace Samkuk.Upgrades
{
    public enum UpgradeKind
    {
        NewWeapon,
        WeaponLevelUp,
        Passive,
        Heal,
        Evolve
    }

    /// <summary>레벨업 때 제시되는 선택지 하나.</summary>
    public class UpgradeOption
    {
        public UpgradeKind Kind;
        public string Title;
        public string Description;
        /// <summary>NewWeapon / WeaponLevelUp 일 때의 대상 무기. Evolve 일 때는 진화 결과 무기.</summary>
        public WeaponData Weapon;
        /// <summary>Passive 일 때의 대상 패시브.</summary>
        public PassiveData Passive;
        /// <summary>Evolve 일 때의 진화 조합.</summary>
        public EvolutionData Evolution;
        /// <summary>선택 시 실행되는 효과.</summary>
        public Action Apply;
    }
}
