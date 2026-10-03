using UnityEngine;

namespace Samkuk.Data
{
    public enum SkillType
    {
        Blessing,         // 체력 회복 + 화면의 경험치 보석 모두 흡수
        GreenDragonSlash, // 주변 광역 강타
        Roar,             // 주변 적 기절 + 약한 피해
        Scheme,           // 일정 시간 공격력 증가/쿨다운 감소
        Warrior           // 일정 시간 무적 + 이동속도 증가 + 주변에 지속 피해
    }

    /// <summary>장수 고유 액티브 스킬 (스페이스바).</summary>
    [CreateAssetMenu(menuName = "Samkuk/Skill Data", fileName = "Skill_New")]
    public class SkillData : ScriptableObject
    {
        public string displayName = "스킬";
        [TextArea] public string description;
        public SkillType type;
        public float cooldown = 20f;

        [Header("효과 수치 (타입별 의미)")]
        [Tooltip("범위 반지름")] public float radius = 5f;
        [Tooltip("피해량 (Warrior는 틱당 피해)")] public float damage = 50f;
        [Tooltip("Blessing: 회복 비율 / Scheme: 공격력 증가 비율 / Warrior: 이동속도 증가 비율")] public float power = 0.4f;
        [Tooltip("Scheme: 쿨다운 감소 비율")] public float power2 = 0f;
        [Tooltip("Roar: 기절 시간 / Scheme, Warrior: 지속 시간")] public float duration = 3f;
        [Tooltip("Warrior: 피해 간격")] public float tickInterval = 0.25f;

        [Header("연출")]
        public Sprite effectSprite;
        public Color effectColor = Color.white;
    }
}
