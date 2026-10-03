using UnityEngine;

namespace Samkuk.Enemies
{
    /// <summary>적이 쏜 투사체 하나의 상태. 이동/충돌은 EnemyProjectileSystem이 일괄 처리한다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyProjectile : MonoBehaviour
    {
        SpriteRenderer sr;

        public Vector2 Velocity { get; private set; }
        public int Damage { get; private set; }
        public float LifeLeft { get; internal set; }
        public float Radius { get; private set; }

        void Awake() => sr = GetComponent<SpriteRenderer>();

        public void Init(Vector2 position, Vector2 velocity, int damage, float lifetime, float radius, Color tint)
        {
            Velocity = velocity;
            Damage = damage;
            LifeLeft = lifetime;
            Radius = radius;

            sr.color = tint;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

            // 스프라이트 크기를 충돌 반지름에 맞춘다 (지름 = 반지름 × 2)
            float spriteSize = sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
            float scale = spriteSize > 0f ? radius * 2f / spriteSize : 1f;
            transform.localScale = Vector3.one * scale;
        }
    }
}
