using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>레벨업 때 선택지로 등장할 수 있는 무기/패시브 목록.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public class UpgradeCatalog : ScriptableObject
    {
        public List<WeaponData> weapons = new List<WeaponData>();
        public List<PassiveData> passives = new List<PassiveData>();
        [Tooltip("무기 진화 조합. 진화 무기는 weapons 목록에 넣지 않는다 (일반 선택지로 나오면 안 됨)")]
        public List<EvolutionData> evolutions = new List<EvolutionData>();
    }
}
