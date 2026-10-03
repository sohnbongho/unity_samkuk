using System.Collections.Generic;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.Pool;

namespace Samkuk.Pickups
{
    /// <summary>
    /// 적이 죽으면 경험치 보석을 떨어뜨리고(풀링), 플레이어가 가까이 오면 끌려가 경험치로 흡수시킨다.
    /// 보석이 너무 많아지면 새 보석은 즉시 경험치로 지급한다.
    /// </summary>
    public class ExpGemManager : MonoBehaviour
    {
        [SerializeField] ExpGem gemPrefab;
        [SerializeField] Transform target;
        [SerializeField] PlayerExperience experience;
        [SerializeField, Tooltip("이 거리 안에 들어오면 끌려오기 시작 (패시브로 확장)")] float magnetRadius = 2.2f;
        [SerializeField] float collectRadius = 0.35f;
        [SerializeField] float startSpeed = 4f;
        [SerializeField] float acceleration = 30f;
        [SerializeField] float maxSpeed = 20f;
        [SerializeField] int maxGems = 300;

        readonly List<ExpGem> gems = new List<ExpGem>(128);
        ObjectPool<ExpGem> pool;
        PlayerStats stats;
        Transform statsTarget;

        public ExpGem GemPrefab { get => gemPrefab; set => gemPrefab = value; }
        public Transform Target { get => target; set => target = value; }
        public PlayerExperience Experience { get => experience; set => experience = value; }
        public int MaxGems { get => maxGems; set => maxGems = value; }
        public float MagnetRadius { get => magnetRadius; set => magnetRadius = value; }
        public int ActiveCount => gems.Count;
        public int PooledTotal => pool?.CountAll ?? 0;

        void OnEnable() => Enemy.Died += OnEnemyDied;
        void OnDisable() => Enemy.Died -= OnEnemyDied;

        void Start()
        {
            if (target == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
            if (experience == null && target != null) experience = target.GetComponent<PlayerExperience>();
        }

        void OnEnemyDied(Enemy e)
        {
            if (e.Data == null) return;
            Spawn(e.Position, e.Data.expReward);
        }

        /// <summary>보석을 하나 떨어뜨린다. 한도를 넘으면 즉시 경험치로 지급하고 null을 반환.</summary>
        public ExpGem Spawn(Vector2 position, int value)
        {
            if (value <= 0 || gemPrefab == null) return null;

            if (gems.Count >= maxGems)
            {
                if (experience != null) experience.AddExp(value);
                return null;
            }

            EnsurePool();
            var gem = pool.Get();
            gem.Init(position, value);
            gems.Add(gem);
            return gem;
        }

        /// <summary>떨어져 있는 모든 보석을 플레이어에게 끌어당긴다 (스킬용).</summary>
        public void AttractAll()
        {
            foreach (var gem in gems)
                if (!gem.Attracted) gem.Attract(startSpeed);
        }

        void Update()
        {
            if (target == null || experience == null || gems.Count == 0) return;

            if (statsTarget != target)
            {
                statsTarget = target;
                stats = target.GetComponent<PlayerStats>();
            }

            float dt = Time.deltaTime;
            Vector2 tp = target.position;
            float radius = magnetRadius * (stats != null ? stats.PickupRadiusMultiplier : 1f);
            float radiusSqr = radius * radius;

            for (int i = gems.Count - 1; i >= 0; i--)
            {
                ExpGem gem = gems[i];
                Vector2 p = gem.transform.position;
                Vector2 toTarget = tp - p;
                float distSqr = toTarget.sqrMagnitude;

                if (!gem.Attracted)
                {
                    if (distSqr > radiusSqr) continue;
                    gem.Attract(startSpeed);
                }

                gem.Speed = Mathf.Min(maxSpeed, gem.Speed + acceleration * dt);
                float step = gem.Speed * dt;
                float dist = Mathf.Sqrt(distSqr);

                if (dist <= collectRadius || step >= dist)
                {
                    experience.AddExp(gem.Value);
                    ReleaseAt(i);
                    continue;
                }

                gem.transform.position = p + toTarget / dist * step;
            }
        }

        void ReleaseAt(int index)
        {
            ExpGem gem = gems[index];
            int last = gems.Count - 1;
            gems[index] = gems[last];
            gems.RemoveAt(last);
            pool.Release(gem);
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ObjectPool<ExpGem>(
                createFunc: () => Instantiate(gemPrefab, transform),
                actionOnGet: g => g.gameObject.SetActive(true),
                actionOnRelease: g => g.gameObject.SetActive(false),
                actionOnDestroy: g => { if (g != null) Destroy(g.gameObject); },
                collectionCheck: false,
                defaultCapacity: 128,
                maxSize: 1000);
        }
    }
}
