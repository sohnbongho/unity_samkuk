using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>선택 가능한 장수 목록.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Hero Catalog", fileName = "HeroCatalog")]
    public class HeroCatalog : ScriptableObject
    {
        public List<HeroData> heroes = new List<HeroData>();
    }
}
