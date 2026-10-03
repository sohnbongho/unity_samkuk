using System;
using System.Collections.Generic;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 풀링되는 투사체. 물리 대신 EnemyManager의 그리드 질의로 명중을 판정한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Projectile : MonoBehaviour
    {
        SpriteRenderer sr;
        Sprite defaultSprite;
        EnemyManager enemies;
        Action<Projectile> release;

        Vector2 direction;
        float speed;
        float damage;
        float radius;
        float lifeLeft;
        int pierceLeft;

        readonly List<Enemy> hitBuffer = new List<Enemy>(16);
        readonly List<Enemy> alreadyHit = new List<Enemy>(8);

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            defaultSprite = sr.sprite;
        }

        public void Launch(EnemyManager manager, Action<Projectile> releaseCallback,
            Vector2 position, Vector2 dir, float dmg, float spd, int pierce, float lifetime,
            float hitRadius, Sprite sprite, Color tint)
        {
            enemies = manager;
            release = releaseCallback;
            direction = dir.normalized;
            damage = dmg;
            speed = spd;
            pierceLeft = Mathf.Max(1, pierce);
            lifeLeft = lifetime;
            radius = hitRadius;
            alreadyHit.Clear();

            sr.sprite = sprite != null ? sprite : defaultSprite;
            sr.color = tint;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += (Vector3)(direction * (speed * dt));

            lifeLeft -= dt;
            if (lifeLeft <= 0f)
            {
                release?.Invoke(this);
                return;
            }

            hitBuffer.Clear();
            enemies.OverlapCircle(transform.position, radius, hitBuffer);

            for (int i = 0; i < hitBuffer.Count; i++)
            {
                Enemy e = hitBuffer[i];
                if (alreadyHit.Contains(e)) continue;

                alreadyHit.Add(e);
                e.TakeDamage(damage);

                if (--pierceLeft <= 0)
                {
                    release?.Invoke(this);
                    return;
                }
            }
        }
    }
}
