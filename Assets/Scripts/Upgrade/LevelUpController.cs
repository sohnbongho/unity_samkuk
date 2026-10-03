using System;
using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Player;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Upgrades
{
    /// <summary>
    /// 레벨업 흐름 관리: 레벨이 오르면 게임을 멈추고 선택지를 보여준다.
    /// 한 번에 여러 레벨이 오르면 선택을 차례로 반복한다.
    /// </summary>
    public class LevelUpController : MonoBehaviour
    {
        [SerializeField] PlayerExperience experience;
        [SerializeField] WeaponController weapons;
        [SerializeField] PlayerStats stats;
        [SerializeField] PlayerHealth health;
        [SerializeField] UpgradeCatalog catalog;
        [SerializeField] LevelUpUI ui;
        [SerializeField] int choiceCount = 3;
        [SerializeField] int maxWeapons = 4;

        ILevelUpView view;
        List<UpgradeOption> currentOptions;

        public PlayerExperience Experience { get => experience; set => SetExperience(value); }
        public WeaponController Weapons { get => weapons; set => weapons = value; }
        public PlayerStats Stats { get => stats; set => stats = value; }
        public PlayerHealth Health { get => health; set => health = value; }
        public UpgradeCatalog Catalog { get => catalog; set => catalog = value; }
        public ILevelUpView View { get => view; set => view = value; }

        /// <summary>선택을 기다리는 레벨업 횟수 (표시 중인 것 포함).</summary>
        public int PendingCount { get; private set; }
        public bool IsShowing { get; private set; }
        public IReadOnlyList<UpgradeOption> CurrentOptions => currentOptions;

        public event Action<UpgradeOption> Chosen;

        void Awake()
        {
            if (view == null) view = ui;
        }

        void OnEnable()
        {
            if (experience != null) experience.LevelUp += OnLevelUp;
        }

        void OnDisable()
        {
            if (experience != null) experience.LevelUp -= OnLevelUp;
        }

        void SetExperience(PlayerExperience value)
        {
            if (experience != null) experience.LevelUp -= OnLevelUp;
            experience = value;
            if (experience != null && isActiveAndEnabled) experience.LevelUp += OnLevelUp;
        }

        void OnLevelUp(int newLevel)
        {
            PendingCount++;
            if (!IsShowing) ShowNext();
        }

        void ShowNext()
        {
            if (health != null && health.IsDead)
            {
                PendingCount = 0;
                return;
            }

            currentOptions = UpgradeGenerator.Generate(catalog, weapons, stats, health, choiceCount, maxWeapons);
            if (currentOptions.Count == 0)
            {
                // 선택지가 전혀 없으면 건너뜀
                PendingCount--;
                if (PendingCount > 0) ShowNext();
                return;
            }

            IsShowing = true;
            Time.timeScale = 0f;
            view?.Show(currentOptions, Choose);
        }

        /// <summary>선택지를 고른다 (UI 클릭/단축키가 호출).</summary>
        public void Choose(int index)
        {
            if (!IsShowing || currentOptions == null || index < 0 || index >= currentOptions.Count) return;

            var option = currentOptions[index];
            IsShowing = false;
            currentOptions = null;
            view?.Hide();

            option.Apply?.Invoke();
            Chosen?.Invoke(option);

            PendingCount--;
            if (PendingCount > 0)
            {
                ShowNext();
            }
            else if (health == null || !health.IsDead)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
