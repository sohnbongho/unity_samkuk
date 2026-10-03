using UnityEngine;

namespace Samkuk.Data
{
    public enum WeaponType
    {
        Arrow,  // 가장 가까운 적을 향해 투사체 발사
        Slash,  // 바라보는 방향의 근접 범위 공격
        Orbit,  // 플레이어 주위를 도는 칼날
        Thrust, // 가까운 적 방향으로 긴 창을 내질러 직선상의 적을 꿰뚫음
        FireZone, // 적이 있는 곳에 불길(장판)을 깔아 지속 피해
        Lightning, // 가까운 적 위에 벼락을 떨어뜨림 (범위 피해)
        Rain,   // 적 주변에 화살비를 쏟아 예고 후 폭격
        Nova    // 플레이어를 중심으로 퍼지는 충격파 (넉백)
    }

    /// <summary>무기 기본 능력치. 레벨당 증가량도 여기서 정의한다.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Weapon Data", fileName = "Weapon_New")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "무기";
        [TextArea] public string description;
        public WeaponType type;
        [Tooltip("투사체/이펙트/칼날 스프라이트 (비우면 기본값)")]
        public Sprite sprite;
        public Color tint = Color.white;

        [Header("기본 능력치")]
        public float damage = 10f;
        [Tooltip("발사/공격 간격(초). Orbit은 사용하지 않음")]
        public float cooldown = 1f;
        [Tooltip("Arrow: 타겟 탐색 거리 / Slash: 공격 범위 / Orbit: 공전 반지름")]
        public float range = 8f;
        [Tooltip("발사 수 / 베기 방향 수 / 칼날 수")]
        public int count = 1;
        [Tooltip("투사체 속도 (Arrow)")]
        public float projectileSpeed = 10f;
        [Tooltip("관통 가능한 적 수 (Arrow)")]
        public int pierce = 1;
        [Tooltip("투사체 수명 / 베기 이펙트 지속시간")]
        public float duration = 2f;
        [Tooltip("투사체/칼날 충돌 반지름")]
        public float size = 0.25f;
        [Tooltip("Orbit 회전 속도 (도/초)")]
        public float rotateSpeed = 180f;
        [Tooltip("Orbit/FireZone 피해 간격(초)")]
        public float tickInterval = 0.3f;
        [Tooltip("맞은 적을 밀어내는 힘 (0이면 밀지 않음)")]
        public float knockback = 0f;

        [Header("레벨 성장")]
        public int maxLevel = 8;
        [Tooltip("레벨당 피해량 증가 비율")]
        public float damagePerLevel = 0.2f;
        [Tooltip("레벨당 쿨다운 감소 비율")]
        public float cooldownReductionPerLevel = 0.05f;
        [Tooltip("N레벨마다 count +1 (0이면 증가 없음)")]
        public int levelsPerExtraCount = 2;
    }
}
