using System;
using System.Collections.Generic;
using Samkuk.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 오브젝트 풀링으로 적을 생성하고, 카메라 화면 바깥의 원 위에서 스폰한다.
    /// 플레이어에게서 너무 멀어진 적은 풀로 돌려보내지 않고 다시 화면 밖 원 위로 재배치한다.
    /// (웨이브 연동은 Step 7에서 추가)
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public EnemyData data;
            public float weight;
        }

        [SerializeField] Enemy enemyPrefab;
        [SerializeField] EnemyManager manager;
        [SerializeField] Camera cam;
        [SerializeField] List<Entry> spawnTable = new List<Entry>();
        [SerializeField, Tooltip("초당 스폰 수")] float spawnPerSecond = 6f;
        [SerializeField, Tooltip("동시에 존재할 수 있는 최대 적 수")] int maxAlive = 400;
        [SerializeField, Tooltip("화면 대각선 바깥으로 얼마나 더 떨어져서 스폰할지")] float spawnMargin = 1.5f;

        public bool autoSpawn = true;

        ObjectPool<Enemy> pool;
        float accumulator;

        public Enemy EnemyPrefab { get => enemyPrefab; set => enemyPrefab = value; }
        public EnemyManager Manager { get => manager; set => manager = value; }
        public Camera Cam { get => cam; set => cam = value; }
        public List<Entry> SpawnTable => spawnTable;
        public float SpawnPerSecond { get => spawnPerSecond; set => spawnPerSecond = value; }
        public int MaxAlive { get => maxAlive; set => maxAlive = value; }
        /// <summary>풀이 지금까지 만든 총 인스턴스 수 (활성 + 비활성).</summary>
        public int PooledTotal => pool?.CountAll ?? 0;

        void OnEnable()
        {
            if (manager != null) manager.EnemyTooFar += OnEnemyTooFar;
        }

        void OnDisable()
        {
            if (manager != null) manager.EnemyTooFar -= OnEnemyTooFar;
        }

        /// <summary>manager를 코드로 바꿨을 때 이벤트 구독을 갱신한다.</summary>
        public void BindManager(EnemyManager m)
        {
            if (manager != null) manager.EnemyTooFar -= OnEnemyTooFar;
            manager = m;
            if (manager != null && isActiveAndEnabled) manager.EnemyTooFar += OnEnemyTooFar;
        }

        void Start()
        {
            if (!autoSpawn) return;
            if (enemyPrefab == null) Debug.LogWarning("[EnemySpawner] enemyPrefab이 비어 있습니다. Samkuk > Step 3 셋업을 다시 실행하세요.", this);
            if (manager == null) Debug.LogWarning("[EnemySpawner] manager가 비어 있습니다.", this);
            if (spawnTable.Count == 0 || spawnTable.TrueForAll(s => s.data == null))
                Debug.LogWarning("[EnemySpawner] spawnTable에 EnemyData가 없습니다.", this);
        }

        void Update()
        {
            if (!autoSpawn || manager == null || manager.Target == null || enemyPrefab == null) return;

            accumulator += spawnPerSecond * Time.deltaTime;
            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                if (manager.Count >= maxAlive)
                {
                    accumulator = 0f;
                    break;
                }
                Spawn();
            }
        }

        /// <summary>스폰 테이블에서 무작위로 골라 화면 밖에 한 마리 스폰.</summary>
        public Enemy Spawn()
        {
            var data = PickData();
            if (data == null) return null;
            return SpawnAt(data, RandomRingPosition());
        }

        /// <summary>디버그/웨이브용: 여러 마리를 한 번에 스폰.</summary>
        public void SpawnBurst(int count)
        {
            for (int i = 0; i < count; i++) Spawn();
        }

        public Enemy SpawnAt(EnemyData data, Vector2 position)
        {
            EnsurePool();
            var e = pool.Get();
            e.Init(data, position);
            manager.Register(e);
            return e;
        }

        public void Despawn(Enemy e)
        {
            if (e == null || !e.gameObject.activeSelf) return; // 중복 반환 방지
            pool.Release(e);
        }

        /// <summary>카메라 화면(대각선) 바깥의 원 위 임의 위치.</summary>
        public Vector2 RandomRingPosition()
        {
            Vector2 center = manager.Target.position;
            float halfH = cam != null ? cam.orthographicSize : 6f;
            float halfW = halfH * (cam != null ? cam.aspect : 16f / 9f);
            float radius = Mathf.Sqrt(halfW * halfW + halfH * halfH) + spawnMargin;
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        void OnEnemyTooFar(Enemy e) => e.Teleport(RandomRingPosition());

        EnemyData PickData()
        {
            float total = 0f;
            foreach (var entry in spawnTable)
                if (entry.data != null) total += Mathf.Max(0f, entry.weight);
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;
            foreach (var entry in spawnTable)
            {
                if (entry.data == null) continue;
                roll -= Mathf.Max(0f, entry.weight);
                if (roll <= 0f) return entry.data;
            }
            return spawnTable[spawnTable.Count - 1].data;
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ObjectPool<Enemy>(
                createFunc: () =>
                {
                    var e = Instantiate(enemyPrefab, transform);
                    e.DespawnHandler = Despawn;
                    return e;
                },
                actionOnGet: e => e.gameObject.SetActive(true),
                actionOnRelease: e =>
                {
                    manager.Unregister(e);
                    e.gameObject.SetActive(false);
                },
                actionOnDestroy: e => { if (e != null) Destroy(e.gameObject); },
                collectionCheck: false,
                defaultCapacity: 256,
                maxSize: 4000);
        }
    }
}
