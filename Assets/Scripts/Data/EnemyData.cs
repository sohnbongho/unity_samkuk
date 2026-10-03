using UnityEngine;

namespace Samkuk.Data
{
    /// <summary>적 종류별 능력치/외형 데이터.</summary>
    [CreateAssetMenu(menuName = "Samkuk/Enemy Data", fileName = "Enemy_New")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "황건적";
        [Tooltip("비워두면 프리팹의 기본 스프라이트를 사용")]
        public Sprite sprite;
        public Color tint = Color.white;
        public float scale = 1f;

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

        [Header("충돌")]
        [Tooltip("스케일 적용 전 콜라이더 반지름")]
        public float colliderRadius = 0.4f;
    }
}
