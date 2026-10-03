using System;
using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 개별 적. 이동/겹침 방지는 EnemyManager가 일괄 처리하므로 이 클래스는 상태만 가진다.
    /// 풀링 대상이며 Init 으로 매번 재설정된다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Enemy : MonoBehaviour
    {
        Rigidbody2D body;
        SpriteRenderer sr;
        CircleCollider2D col;
        Sprite defaultSprite;

        public EnemyData Data { get; private set; }
        /// <summary>월드 기준 반지름 (콜라이더 반지름 × 스케일).</summary>
        public float Radius { get; private set; }
        public Rigidbody2D Body => body;
        public Vector2 Position => body.position;

        /// <summary>EnemyManager 내부 리스트 인덱스 (-1이면 미등록).</summary>
        internal int ManagerIndex = -1;
        /// <summary>풀 반환 콜백 (스포너가 설정).</summary>
        internal Action<Enemy> DespawnHandler;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<CircleCollider2D>();
            defaultSprite = sr.sprite;
        }

        public void Init(EnemyData data, Vector2 position)
        {
            Data = data;
            sr.sprite = data.sprite != null ? data.sprite : defaultSprite;
            sr.color = data.tint;
            sr.flipX = false;
            transform.localScale = Vector3.one * data.scale;
            col.radius = data.colliderRadius;
            Radius = data.colliderRadius * data.scale;
            Teleport(position);
        }

        public void Teleport(Vector2 position)
        {
            transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public void SetFacing(bool faceLeft) => sr.flipX = faceLeft;

        /// <summary>풀로 반환한다.</summary>
        public void Despawn() => DespawnHandler?.Invoke(this);
    }
}
