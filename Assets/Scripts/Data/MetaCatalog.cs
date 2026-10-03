using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>상점에서 살 수 있는 영구 강화 목록.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Meta Catalog", fileName = "MetaCatalog")]
    public class MetaCatalog : ScriptableObject
    {
        public List<MetaUpgradeData> upgrades = new List<MetaUpgradeData>();
    }
}
