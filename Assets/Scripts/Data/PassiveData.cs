using UnityEngine;

namespace Samkuk.Data
{
    public enum PassiveType
    {
        Damage,       // 공격력 배율 (+value 비율/레벨)
        Cooldown,     // 쿨다운 감소 (value 비율/레벨)
        MoveSpeed,    // 이동 속도 배율
        MaxHp,        // 최대 체력 (+value/레벨, 고정값)
        PickupRadius, // 경험치 획득 범위 배율
        ExpGain,      // 경험치 획득량 배율
        Regen         // 초당 체력 재생 (+value/레벨)
    }

    /// <summary>패시브 아이템 데이터. 레벨당 효과량(valuePerLevel)이 누적된다.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Passive Data", fileName = "Passive_New")]
    public class PassiveData : ScriptableObject
    {
        public string displayName = "패시브";
        [TextArea] public string description;
        public PassiveType type;
        public float valuePerLevel = 0.1f;
        public int maxLevel = 5;
        public Sprite icon;
    }
}
