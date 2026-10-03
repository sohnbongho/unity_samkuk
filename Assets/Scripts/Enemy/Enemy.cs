using System;
using Samkuk.Core;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 개별 적. 이동/겹침 방지는 EnemyManager가 일괄 처리하므로 이 클래스는 체력과 피격 연출을 담당한다.
    /// 풀링 대상이며 Init 으로 매번 재설정된다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        const float FlashDuration = 0.1f;
        const float FlashScalePunch = 0.25f;

        Rigidbody2D body;
        SpriteRenderer sr;
        CircleCollider2D col;
        Sprite defaultSprite;
        float flashTimer;
        float baseScale = 1f;

        public EnemyData Data { get; private set; }
        public float Hp { get; private set; }
        public bool Alive { get; private set; }
        /// <summary>월드 기준 반지름 (콜라이더 반지름 × 스케일).</summary>
        public float Radius { get; private set; }
        public Rigidbody2D Body => body;
        public Vector2 Position => body.position;

        /// <summary>피해를 받았을 때 (적, 피해량). 데미지 숫자 표시 등에 사용.</summary>
        public static event Action<Enemy, float> Damaged;
        /// <summary>사망했을 때. 경험치 드롭 등에 사용 (이 시점에 위치/데이터는 아직 유효).</summary>
        public static event Action<Enemy> Died;

        /// <summary>EnemyManager 내부 리스트 인덱스 (-1이면 미등록).</summary>
        internal int ManagerIndex = -1;
        /// <summary>범위 질의 중복 방지용 스탬프.</summary>
        internal int QueryStamp;
        /// <summary>풀 반환 콜백 (스포너가 설정).</summary>
        internal Action<Enemy> DespawnHandler;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<CircleCollider2D>();
            defaultSprite = sr.sprite;
            enabled = false; // Update는 피격 연출 중에만 돈다
        }

        public void Init(EnemyData data, Vector2 position)
        {
            Data = data;
            Hp = data.maxHp;
            Alive = true;
            baseScale = data.scale;
            flashTimer = 0f;
            enabled = false;
            sr.sprite = data.sprite != null ? data.sprite : defaultSprite;
            sr.color = data.tint;
            sr.flipX = false;
            transform.localScale = Vector3.one * baseScale;
            col.radius = data.colliderRadius;
            Radius = data.colliderRadius * baseScale;
            Teleport(position);
        }

        public void Teleport(Vector2 position)
        {
            transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public void SetFacing(bool faceLeft) => sr.flipX = faceLeft;

        public void TakeDamage(float amount)
        {
            if (!Alive || amount <= 0f) return;

            Hp -= amount;
            Damaged?.Invoke(this, amount);

            if (Hp <= 0f)
            {
                Alive = false;
                Died?.Invoke(this);
                Despawn();
            }
            else
            {
                flashTimer = FlashDuration;
                enabled = true;
            }
        }

        void Update()
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / FlashDuration);
            transform.localScale = Vector3.one * (baseScale * (1f + FlashScalePunch * t));
            if (flashTimer <= 0f) enabled = false;
        }

        /// <summary>풀로 반환한다.</summary>
        public void Despawn() => DespawnHandler?.Invoke(this);
    }
}
