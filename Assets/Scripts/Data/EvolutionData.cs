using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>
    /// 무기 진화 조합: baseWeapon이 requiredLevel 이상이고 requiredPassive를 requiredPassiveLevel 이상
    /// 보유하면 레벨업 선택지에 "진화"가 나타나며, 선택하면 baseWeapon이 evolvedWeapon으로 교체된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/Evolution Data", fileName = "Evolution_New")]
    public class EvolutionData : ScriptableObject
    {
        public WeaponData baseWeapon;
        public PassiveData requiredPassive;
        public WeaponData evolvedWeapon;

        [Tooltip("기본 무기의 필요 레벨 (기본 무기의 최대 레벨보다 크면 최대 레벨로 간주)")]
        public int requiredLevel = 5;
        [Tooltip("필요 패시브의 최소 레벨")]
        public int requiredPassiveLevel = 1;

        public bool IsValid => baseWeapon != null && requiredPassive != null && evolvedWeapon != null;
    }
}
