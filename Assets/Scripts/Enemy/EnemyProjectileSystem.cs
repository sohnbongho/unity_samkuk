using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.Pool;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 적 투사체(궁병의 화살 등)를 풀링해서 발사하고, 직선 이동과 플레이어 명중을 일괄 처리한다.
    /// 명중 시 피격 후 무적 시간/스킬 무적을 따르는 접촉 피해로 처리되며, 무적이어도 투사체는 소멸한다.
    /// </summary>
    public class EnemyProjectileSystem : MonoBehaviour
    {
        [SerializeField] EnemyProjectile prefab;
        [SerializeField] Transform target;
        [SerializeField, Tooltip("플레이어 충돌 반지름")] float playerRadius = 0.4f;
        [SerializeField] int maxProjectiles = 300;

        readonly List<EnemyProjectile> active = new List<EnemyProjectile>(64);
        ObjectPool<EnemyProjectile> pool;
        PlayerHealth playerHealth;
        Transform healthLookupTarget;

        public EnemyProjectile Prefab { get => prefab; set => prefab = value; }
        public Transform Target { get => target; set => target = value; }
        public int ActiveCount => active.Count;
        /// <summary>지금까지 발사된 투사체 수.</summary>
        public int FiredCount { get; private set; }
        /// <summary>지금까지 플레이어에게 명중한 투사체 수 (무적으로 피해가 무시된 것 포함).</summary>
        public int HitCount { get; private set; }
        public int PooledTotal => pool?.CountAll ?? 0;

        void Start()
        {
            if (target == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
        }

        /// <summary>적 data의 설정으로 position에서 dir 방향으로 투사체를 발사한다.</summary>
        public EnemyProjectile Fire(Vector2 position, Vector2 dir, EnemyData data)
        {
            if (prefab == null || active.Count >= maxProjectiles) return null;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;

            EnsurePool();
            var p = pool.Get();
            p.Init(position, dir.normalized * data.projectileSpeed, data.projectileDamage,
                data.projectileLifetime, data.projectileSize, data.projectileTint);
            active.Add(p);
            FiredCount++;
            Audio.AudioManager.Play(Audio.SfxId.EnemyShot, 0.7f);
            return p;
        }

        void Update()
        {
            if (active.Count == 0 || target == null) return;

            if (healthLookupTarget != target)
            {
                healthLookupTarget = target;
                playerHealth = target.GetComponent<PlayerHealth>();
            }

            float dt = Time.deltaTime;
            Vector2 tp = target.position;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                EnemyProjectile p = active[i];
                Vector2 pos = (Vector2)p.transform.position + p.Velocity * dt;
                p.transform.position = pos;
                p.LifeLeft -= dt;

                if (p.LifeLeft <= 0f)
                {
                    ReleaseAt(i);
                    continue;
                }

                float reach = p.Radius + playerRadius;
                if ((tp - pos).sqrMagnitude > reach * reach) continue;

                HitCount++;
                if (playerHealth != null) playerHealth.TryContactDamage(p.Damage);
                ReleaseAt(i);
            }
        }

        void ReleaseAt(int index)
        {
            EnemyProjectile p = active[index];
            int last = active.Count - 1;
            active[index] = active[last];
            active.RemoveAt(last);
            pool.Release(p);
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ObjectPool<EnemyProjectile>(
                createFunc: () => Instantiate(prefab, transform),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                collectionCheck: false,
                defaultCapacity: 64,
                maxSize: 600);
        }
    }
}
