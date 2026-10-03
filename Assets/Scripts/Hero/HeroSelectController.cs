using System;
using System.Collections.Generic;
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
    /// 선택 중에는 게임이 멈춘다.
    /// </summary>
    public class HeroSelectController : MonoBehaviour
    {
        [SerializeField] HeroCatalog catalog;
        [SerializeField] PlayerStats stats;
        [SerializeField] PlayerHealth health;
        [SerializeField] WeaponController weapons;
        [SerializeField] SkillController skills;
        [SerializeField] HeroSelectUI ui;

        IHeroSelectView view;

        public HeroCatalog Catalog { get => catalog; set => catalog = value; }
        public PlayerStats Stats { get => stats; set => stats = value; }
        public PlayerHealth Health { get => health; set => health = value; }
        public WeaponController Weapons { get => weapons; set => weapons = value; }
        public SkillController Skills { get => skills; set => skills = value; }
        public IHeroSelectView View { get => view; set => view = value; }

        public HeroData Current { get; private set; }
        public bool IsSelecting { get; private set; }

        public event Action<HeroData> HeroSelected;

        void Awake()
        {
            if (view == null) view = ui;
        }

        void Start() => Begin();

        /// <summary>선택 화면을 띄운다. 화면이 없으면 마지막 선택(또는 첫 장수)을 바로 적용한다.</summary>
        public void Begin()
        {
            if (catalog == null || catalog.heroes.Count == 0) return;

            if (view == null)
            {
                Apply(GameSession.SelectedHero != null ? GameSession.SelectedHero : catalog.heroes[0]);
                return;
            }

            IsSelecting = true;
            Time.timeScale = 0f;
            view.Show(catalog.heroes, Choose);
        }

        /// <summary>목록의 index번째 장수를 고른다 (UI 클릭/단축키가 호출).</summary>
        public void Choose(int index)
        {
            if (!IsSelecting || catalog == null || index < 0 || index >= catalog.heroes.Count) return;

            IsSelecting = false;
            view?.Hide();
            Apply(catalog.heroes[index]);
            Time.timeScale = 1f;
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
