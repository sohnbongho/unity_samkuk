using System;
using System.Collections.Generic;
using Samkuk.Allies;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using Samkuk.Skills;
using Samkuk.UI;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Heroes
{
    /// <summary>
    /// 게임 시작 시 장수를 고르게 하고, 선택된 장수의 능력치/시작 무기/스킬/색을 플레이어에 적용한다.
    /// 이어서 함께 싸울 아군 장수를 고르게 한다(아군 선택 화면이 없으면 건너뜀). 선택 중에는 게임이 멈춘다.
    /// </summary>
    public class HeroSelectController : MonoBehaviour
    {
        [SerializeField] HeroCatalog catalog;
        [SerializeField] PlayerStats stats;
        [SerializeField] PlayerHealth health;
        [SerializeField] WeaponController weapons;
        [SerializeField] SkillController skills;
        [SerializeField] HeroSelectUI ui;
        // 아래 둘은 비워 두면 씬에서 찾는다 (장수 선택 셋업을 다시 돌려도 연결이 유지되도록)
        [SerializeField] AllySelectUI allyUi;
        [SerializeField] AllyManager allyManager;

        IHeroSelectView view;
        IAllySelectView allyView;
        bool selectingAllies;

        public HeroCatalog Catalog { get => catalog; set => catalog = value; }
        public PlayerStats Stats { get => stats; set => stats = value; }
        public PlayerHealth Health { get => health; set => health = value; }
        public WeaponController Weapons { get => weapons; set => weapons = value; }
        public SkillController Skills { get => skills; set => skills = value; }
        public IHeroSelectView View { get => view; set => view = value; }
        public IAllySelectView AllyView { get => allyView; set => allyView = value; }
        public AllyManager Allies { get => allyManager; set => allyManager = value; }

        public HeroData Current { get; private set; }
        public bool IsSelecting { get; private set; }

        public event Action<HeroData> HeroSelected;
        /// <summary>아군 선택이 확정되었을 때 (고른 아군들, 0명 가능).</summary>
        public event Action<IReadOnlyList<HeroData>> AlliesSelected;

        void Awake()
        {
            if (view == null) view = ui;
            if (allyView == null) allyView = allyUi != null ? allyUi : FindAnyObjectByType<AllySelectUI>();
            if (allyManager == null) allyManager = FindAnyObjectByType<AllyManager>();
        }

        void Start() => Begin();

        /// <summary>선택 화면을 띄운다. 화면이 없으면 마지막 선택(또는 첫 장수)을 바로 적용한다.</summary>
        public void Begin()
        {
            if (catalog == null || catalog.heroes.Count == 0) return;

            if (view == null)
            {
                Apply(GameSession.SelectedHero != null ? GameSession.SelectedHero : catalog.heroes[0]);
                SpawnSessionAllies();
                return;
            }

            IsSelecting = true;
            Time.timeScale = 0f;
            view.Show(catalog.heroes, Choose);
        }

        /// <summary>목록의 index번째 장수를 고른다 (UI 클릭/단축키가 호출).</summary>
        public void Choose(int index)
        {
            if (!IsSelecting || selectingAllies || catalog == null || index < 0 || index >= catalog.heroes.Count) return;

            view?.Hide();
            Apply(catalog.heroes[index]);
            if (!BeginAllySelection(catalog.heroes[index])) Finish();
        }

        void Finish()
        {
            IsSelecting = false;
            Time.timeScale = 1f;
        }

        /// <summary>고른 장수를 뺀 나머지 중에서 아군을 고르게 한다. 보여 줄 화면이 없으면 false(바로 시작).</summary>
        bool BeginAllySelection(HeroData chosen)
        {
            if (allyView == null || allyManager == null) return false;

            var candidates = new List<HeroData>();
            foreach (var h in catalog.heroes)
                if (h != null && h != chosen) candidates.Add(h);
            if (candidates.Count == 0) return false;

            selectingAllies = true;
            allyView.Show(candidates, AllyConfig.MaxAllies, GameSession.SelectedAllies, OnAlliesConfirmed);
            return true;
        }

        void OnAlliesConfirmed(IReadOnlyList<HeroData> picked)
        {
            if (!selectingAllies) return;

            selectingAllies = false;
            allyView?.Hide();
            GameSession.SetAllies(picked);
            allyManager.Spawn(picked);
            AlliesSelected?.Invoke(picked);
            Finish();
        }

        /// <summary>선택 화면 없이 시작할 때: 지난번에 고른 아군(고른 장수 제외)을 그대로 데려간다.</summary>
        void SpawnSessionAllies()
        {
            if (allyManager == null) return;

            var picked = new List<HeroData>();
            foreach (var h in GameSession.SelectedAllies)
                if (h != Current) picked.Add(h);
            if (picked.Count > 0) allyManager.Spawn(picked);
        }

        /// <summary>장수의 보정/무기/스킬/색을 플레이어에 적용한다.</summary>
        public void Apply(HeroData hero)
        {
            if (hero == null) return;
            Current = hero;
            GameSession.SelectedHero = hero;

            if (stats != null) stats.ApplyHero(hero);
            // 걷기 그림이 있으면 그림 색 그대로, 없으면 기본 스프라이트에 장수 색을 입힌다
            var animator = health != null ? health.GetComponent<PlayerAnimator>() : null;
            if (animator != null) animator.SetHero(hero);
            bool hasArt = animator != null && animator.HasSheet;
            if (health != null) health.SetBaseColor(hasArt ? Color.white : hero.tint);
            if (weapons != null && hero.startingWeapon != null) weapons.AddWeapon(hero.startingWeapon);
            if (skills != null) skills.SetSkill(hero.skill);

            HeroSelected?.Invoke(hero);
        }
    }
}
