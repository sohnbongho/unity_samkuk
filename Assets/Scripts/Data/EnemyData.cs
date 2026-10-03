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

        [Header("충돌")]
        [Tooltip("스케일 적용 전 콜라이더 반지름")]
        public float colliderRadius = 0.4f;
    }
}
