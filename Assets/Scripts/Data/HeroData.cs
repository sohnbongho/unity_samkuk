using System.Collections.Generic;
using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>플레이 가능한 장수: 능력치 보정, 시작 무기, 고유 스킬.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Hero Data", fileName = "Hero_New")]
    public class HeroData : ScriptableObject
    {
        public string displayName = "장수";
        [Tooltip("별칭 (예: 인덕의 군주)")] public string title;
        [TextArea] public string description;
        [Tooltip("플레이어 스프라이트에 입힐 색")] public Color tint = Color.white;
        [Tooltip("장수 선택 카드에 보일 초상화 (세로 4:5 권장, 예: 512x640). 비어 있으면 실루엣 + 색으로 대체")]
        public Sprite portrait;
        [Tooltip("게임 안 걷기 스프라이트 시트 (4열 x 4행: 열=걷기 프레임, 행=아래/위/왼쪽/오른쪽). 비어 있으면 기본 스프라이트 + 장수 색")]
        public Texture2D walkSheet;
        [Tooltip("걷기 시트의 픽셀/유닛. 도트 규격(칸 48) 시트는 32(한 칸 1.5유닛), 예전 96칸 시트는 96")] public float walkPixelsPerUnit = 96f;

        [Header("능력치 보정")]
        [Tooltip("최대 체력 증감 (기본 100)")] public float maxHpBonus = 0f;
        public float damageMultiplier = 1f;
        public float moveSpeedMultiplier = 1f;
        public float expMultiplier = 1f;
        public float pickupRadiusMultiplier = 1f;

        [Header("시작 장비")]
        public WeaponData startingWeapon;
        public SkillData skill;

        /// <summary>기본값과 다른 능력치 보정을 줄바꿈으로 나열한 문자열 (선택 카드용).</summary>
        public string StatSummary()
        {
            var parts = new List<string>();
            if (!Mathf.Approximately(maxHpBonus, 0f)) parts.Add($"체력 {maxHpBonus:+0;-0}");
            AddPercent(parts, "공격력", damageMultiplier);
            AddPercent(parts, "이동속도", moveSpeedMultiplier);
            AddPercent(parts, "경험치", expMultiplier);
            AddPercent(parts, "획득 범위", pickupRadiusMultiplier);
            return string.Join("   ", parts);
        }

        static void AddPercent(List<string> parts, string label, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f)) return;
            parts.Add($"{label} {(multiplier - 1f) * 100f:+0;-0}%");
        }
    }
}
