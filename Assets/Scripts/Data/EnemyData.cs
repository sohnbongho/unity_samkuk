using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>적의 병과. 행동 방식은 데이터(attackRange, chargeInterval 등)로 결정되며 이 값은 분류/표시용이다.</summary>
    public enum EnemyRole
    {
        Infantry, // 보병: 플레이어에게 곧장 달려듦
        Archer,   // 궁병: 거리를 유지하며 원거리 공격
        Cavalry   // 기병: 빠르게 접근, 돌진 패턴
    }

    /// <summary>적 종류별 능력치/외형 데이터.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Enemy Data", fileName = "Enemy_New")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "황건적";
        public EnemyRole role = EnemyRole.Infantry;
        [Tooltip("비워두면 프리팹의 기본 스프라이트를 사용")]
        public Sprite sprite;
        public Color tint = Color.white;
        public float scale = 1f;
        [Tooltip("걷기 스프라이트 시트 (4열 x 4행: 열=걷기 프레임, 행=아래/위/왼쪽/오른쪽). 있으면 sprite/tint 대신 쓰고 그림 색 그대로 보인다")]
        public Texture2D walkSheet;
        [Tooltip("걷기 시트의 픽셀/유닛 (클수록 작게 보임). 도트 규격(칸 48) 시트는 모두 32 이고 크기 차이는 그림 안에서 낸다")] public float walkPixelsPerUnit = 96f;

        [Header("능력치")]
        public float moveSpeed = 1.5f;
        public int maxHp = 10;
        public int contactDamage = 5;
        public int expReward = 1;

        [Header("돌진 패턴 (보스/기병용)")]
        [Tooltip("돌진 주기(초). 0이면 돌진하지 않음")]
        public float chargeInterval = 0f;
        [Tooltip("돌진 전 예고 시간(초): 멈춰서 붉게 깜빡임")]
        public float chargeWindup = 0.6f;
        [Tooltip("돌진 지속 시간(초)")]
        public float chargeDuration = 0.8f;
        [Tooltip("돌진 중 이동 속도 배율")]
        public float chargeSpeedMultiplier = 3f;

        [Header("원거리 공격 (궁병용)")]
        [Tooltip("유지하려는 사격 거리. 0이면 근접 적(원거리 공격 없음)")]
        public float attackRange = 0f;
        [Tooltip("발사 간격(초). 발사 0.4초 전부터 노랗게 깜빡여 예고한다")]
        public float fireInterval = 2.4f;
        public float projectileSpeed = 6f;
        public int projectileDamage = 6;
        public float projectileLifetime = 3f;
        [Tooltip("투사체 충돌 반지름")]
        public float projectileSize = 0.18f;
        public Color projectileTint = new Color(1f, 0.45f, 0.25f);

        [Header("충돌")]
        [Tooltip("스케일 적용 전 콜라이더 반지름")]
        public float colliderRadius = 0.4f;
    }
}
