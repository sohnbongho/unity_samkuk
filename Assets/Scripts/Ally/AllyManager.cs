using System;
using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Allies
{
    /// <summary>
    /// 이번 판에 데려온 아군들을 만들고 관리한다. 적이 아군도 노리도록 EnemyManager 에 등록하고,
    /// 플레이어 레벨이 오르면 아군 무기도 함께 강해지게 한다.
    /// </summary>
    public class AllyManager : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] WeaponController playerWeapons;
        [SerializeField] EnemyManager enemies;
        [SerializeField] PlayerExperience experience;

        readonly List<AllyController> allies = new List<AllyController>(AllyConfig.MaxAllies);

        public IReadOnlyList<AllyController> Allies => allies;
        public PlayerController Player { get => player; set => player = value; }
        public WeaponController PlayerWeapons { get => playerWeapons; set => playerWeapons = value; }
        public EnemyManager Enemies { get => enemies; set => enemies = value; }
        public PlayerExperience Experience
        {
            get => experience;
            set
            {
                if (experience != null) experience.LevelUp -= OnPlayerLevelUp;
                experience = value;
                if (experience != null && isActiveAndEnabled) experience.LevelUp += OnPlayerLevelUp;
            }
        }

        /// <summary>아군이 생기거나 사라졌을 때.</summary>
        public event Action Changed;

        void Start() => ResolveReferences();

        void OnEnable()
        {
            if (experience != null) experience.LevelUp += OnPlayerLevelUp;
        }

        void OnDisable()
        {
            if (experience != null) experience.LevelUp -= OnPlayerLevelUp;
        }

        void ResolveReferences()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (playerWeapons == null && player != null) playerWeapons = player.GetComponent<WeaponController>();
            if (enemies == null) enemies = FindAnyObjectByType<EnemyManager>();
            if (experience == null && player != null)
            {
                experience = player.GetComponent<PlayerExperience>();
                if (experience != null && isActiveAndEnabled) experience.LevelUp += OnPlayerLevelUp;
            }
        }

        /// <summary>
        /// 기존 아군을 모두 없애고 heroes 로 새로 만든다. 최대 <see cref="AllyConfig.MaxAllies"/> 명,
        /// 중복과 빈 항목은 건너뛴다.
        /// </summary>
        public void Spawn(IReadOnlyList<HeroData> heroes)
        {
            ResolveReferences();
            Clear();
            if (heroes == null || player == null) return;

            Sprite fallback = null;
            var playerBody = player.GetComponentInChildren<SpriteRenderer>();
            if (playerBody != null) fallback = playerBody.sprite;

            var seen = new HashSet<HeroData>();
            foreach (var hero in heroes)
            {
                if (hero == null || !seen.Add(hero)) continue;
                if (allies.Count >= AllyConfig.MaxAllies) break;

                var go = new GameObject($"Ally_{hero.displayName}");
                var ally = go.AddComponent<AllyController>();
                ally.Initialize(hero, allies.Count, player, playerWeapons, enemies, fallback);
                if (enemies != null) enemies.RegisterTarget(ally);
                if (experience != null) ally.SetPlayerLevel(experience.Level);
                allies.Add(ally);
            }

            Changed?.Invoke();
        }

        /// <summary>모든 아군을 없앤다.</summary>
        public void Clear()
        {
            foreach (var ally in allies)
            {
                if (ally == null) continue;
                if (enemies != null) enemies.UnregisterTarget(ally);
                Destroy(ally.gameObject);
            }
            bool had = allies.Count > 0;
            allies.Clear();
            if (had) Changed?.Invoke();
        }

        void OnPlayerLevelUp(int level)
        {
            foreach (var ally in allies)
                if (ally != null) ally.SetPlayerLevel(level);
        }

        void OnDestroy()
        {
            if (experience != null) experience.LevelUp -= OnPlayerLevelUp;
            foreach (var ally in allies)
                if (ally != null && enemies != null) enemies.UnregisterTarget(ally);
        }
    }
}
