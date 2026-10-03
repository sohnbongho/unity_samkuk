using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.Pool;

namespace Samkuk.Weapons
{
    /// <summary>플레이어가 보유한 무기를 관리하고 투사체 풀을 제공한다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] Projectile projectilePrefab;
        [SerializeField] List<WeaponData> startingWeapons = new List<WeaponData>();
        [SerializeField] EnemyManager enemyManager;

        readonly List<Weapon> weapons = new List<Weapon>();
        ObjectPool<Projectile> pool;
        PlayerController owner;

        public IReadOnlyList<Weapon> Weapons => weapons;
        public Projectile ProjectilePrefab { get => projectilePrefab; set => projectilePrefab = value; }
        public EnemyManager EnemyManager
        {
            get => enemyManager;
            set
            {
                enemyManager = value;
                foreach (var w in weapons) w.SetEnemies(enemyManager);
            }
        }

        void Awake() => owner = GetComponent<PlayerController>();

        void Start()
        {
            if (enemyManager == null)
                EnemyManager = FindAnyObjectByType<EnemyManager>();

            foreach (var data in startingWeapons)
                if (data != null) AddWeapon(data);
        }

        /// <summary>무기를 추가한다. 이미 가진 무기라면 레벨업한다.</summary>
        public Weapon AddWeapon(WeaponData data)
        {
            foreach (var w in weapons)
            {
                if (w.Data == data)
                {
                    w.LevelUp();
                    return w;
                }
            }

            var go = new GameObject($"Weapon_{data.displayName}");
            go.transform.SetParent(transform, false);

            Weapon weapon;
            switch (data.type)
            {
                case WeaponType.Arrow: weapon = go.AddComponent<ArrowWeapon>(); break;
                case WeaponType.Slash: weapon = go.AddComponent<SlashWeapon>(); break;
                case WeaponType.Orbit: weapon = go.AddComponent<OrbitWeapon>(); break;
                default:
                    Destroy(go);
                    return null;
            }

            weapon.Initialize(data, this, owner, enemyManager);
            weapons.Add(weapon);
            return weapon;
        }

        public void LevelUpAll()
        {
            foreach (var w in weapons) w.LevelUp();
        }

        // ─────────── 투사체 풀 ───────────

        public Projectile GetProjectile()
        {
            EnsurePool();
            return pool.Get();
        }

        public void ReleaseProjectile(Projectile p)
        {
            if (p == null || !p.gameObject.activeSelf) return;
            pool.Release(p);
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ObjectPool<Projectile>(
                createFunc: () => Instantiate(projectilePrefab, transform.parent),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                collectionCheck: false,
                defaultCapacity: 64,
                maxSize: 1000);
        }
    }
}
