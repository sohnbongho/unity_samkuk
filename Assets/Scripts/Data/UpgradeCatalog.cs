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
        [Tooltip("무기 진화 조합. 진화 무기는 weapons 목록에 넣지 않는다 (일반 선택지로 나오면 안 됨). " +
                 "기본 무기는 weapons 목록의 무기이거나 장수의 시작 무기여야 한다.")]
        public List<EvolutionData> evolutions = new List<EvolutionData>();

        [Header("선택지 등장 가중치 (클수록 자주 나옴)")]
        [Tooltip("이미 가진 무기의 레벨업. 높을수록 한 무기를 집중해서 키워 진화에 닿기 쉽다")]
        public float weaponLevelUpWeight = 4f;
        [Tooltip("패시브 (새로 얻기/레벨업)")]
        public float passiveWeight = 2f;
        [Tooltip("새 무기. 낮을수록 빌드가 분산되지 않는다")]
        public float newWeaponWeight = 1f;
    }
}
