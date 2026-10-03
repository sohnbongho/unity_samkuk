using System;
using System.Collections.Generic;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Pickups;
using Samkuk.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Skills
{
    /// <summary>
    /// 장수 고유 액티브 스킬. 스페이스바(또는 게임패드 A)로 발동하며 쿨다운 후 다시 쓸 수 있다.
    /// 쿨다운은 패시브/버프의 쿨다운 배율을 따른다.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class SkillController : MonoBehaviour
    {
        [SerializeField] SkillData skill;
        [SerializeField] EnemyManager enemies;
        [SerializeField] ExpGemManager gems;

        readonly List<Enemy> hits = new List<Enemy>(128);
        PlayerHealth health;
        PlayerStats stats;
        InputAction useAction;
        SpriteRenderer fx;

        float cooldownLeft;
        float cooldownTotal;

        // 지속형 스킬(Warrior) 상태
        float auraLeft;
        float auraTick;

        // 연출
        float fxLeft;
        float fxDuration;
        float fxRadius;
        bool fxFollow;
        Color fxColor;

        public SkillData Skill => skill;
        public bool HasSkill => skill != null;
        public bool IsReady => skill != null && cooldownLeft <= 0f;
        public float CooldownLeft => Mathf.Max(0f, cooldownLeft);
        /// <summary>0(준비 완료) ~ 1(방금 사용).</summary>
        public float CooldownRatio => cooldownTotal > 0f ? Mathf.Clamp01(cooldownLeft / cooldownTotal) : 0f;
        public bool AuraActive => auraLeft > 0f;
        public EnemyManager Enemies { get => enemies; set => enemies = value; }
        public ExpGemManager Gems { get => gems; set => gems = value; }

        /// <summary>스킬을 사용했을 때 호출된다.</summary>
        public event Action<SkillData> Used;

        void Awake()
        {
            health = GetComponent<PlayerHealth>();
            stats = GetComponent<PlayerStats>();

            useAction = new InputAction("Skill", InputActionType.Button);
            useAction.AddBinding("<Keyboard>/space");
            useAction.AddBinding("<Gamepad>/buttonSouth");

            var go = new GameObject("SkillFx");
            go.transform.SetParent(transform, false);
            fx = go.AddComponent<SpriteRenderer>();
            fx.sortingLayerName = GameLayers.Sorting.Effect;
            fx.enabled = false;
        }

        void Start()
        {
            if (enemies == null) enemies = FindAnyObjectByType<EnemyManager>();
            if (gems == null) gems = FindAnyObjectByType<ExpGemManager>();
        }

        void OnEnable() => useAction.Enable();
        void OnDisable() => useAction.Disable();
        void OnDestroy() => useAction?.Dispose();

        /// <summary>장수가 정해졌을 때 스킬을 장착한다. 처음에는 바로 쓸 수 있다.</summary>
        public void SetSkill(SkillData data)
        {
            skill = data;
            cooldownLeft = 0f;
            cooldownTotal = 0f;
            auraLeft = 0f;
            if (fx != null) fx.sprite = data != null ? data.effectSprite : null;
        }

        void Update()
        {
            // 일시정지/선택 화면 중(timeScale 0)에는 스킬을 쓸 수 없다
            if (Time.timeScale <= 0f) return;

            float dt = Time.deltaTime;
            if (cooldownLeft > 0f) cooldownLeft -= dt;

            UpdateAura(dt);
            UpdateFx(dt);

            if (skill != null && !health.IsDead && useAction.WasPressedThisFrame())
                Use();
        }

        /// <summary>스킬을 발동한다. 쿨다운 중이거나 스킬이 없으면 false.</summary>
        public bool Use()
        {
            if (!IsReady || health.IsDead) return false;

            Execute();

            cooldownTotal = skill.cooldown * (stats != null ? stats.CooldownMultiplier : 1f);
            cooldownLeft = cooldownTotal;
            Used?.Invoke(skill);
            return true;
        }

        void Execute()
        {
            Vector2 center = transform.position;
            float dmgMult = stats != null ? stats.DamageMultiplier : 1f;

            switch (skill.type)
            {
                case SkillType.Blessing:
                    health.Heal(health.Max * skill.power);
                    if (gems != null) gems.AttractAll();
                    StartFx(skill.radius, 0.5f, false);
                    break;

                case SkillType.GreenDragonSlash:
                    DamageAround(center, skill.radius, skill.damage * dmgMult, 0f);
                    StartFx(skill.radius, 0.35f, false);
                    break;

                case SkillType.Roar:
                    DamageAround(center, skill.radius, skill.damage * dmgMult, skill.duration);
                    StartFx(skill.radius, 0.5f, false);
                    break;

                case SkillType.Scheme:
                    if (stats != null) stats.AddBuff(skill.power, skill.power2, 0f, skill.duration);
                    StartFx(2.2f, skill.duration, true);
                    break;

                case SkillType.Warrior:
                    health.SetInvulnerable(skill.duration);
                    if (stats != null) stats.AddBuff(0f, 0f, skill.power, skill.duration);
                    auraLeft = skill.duration;
                    auraTick = 0f;
                    StartFx(skill.radius, skill.duration, true);
                    break;
            }
        }

        /// <summary>범위 안의 적에게 피해를 주고, stunSeconds가 0보다 크면 기절시킨다.</summary>
        void DamageAround(Vector2 center, float radius, float damage, float stunSeconds)
        {
            if (enemies == null) return;

            hits.Clear();
            enemies.OverlapCircle(center, radius, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                Enemy e = hits[i];
                if (stunSeconds > 0f) e.Stun(stunSeconds);
                if (damage > 0f) e.TakeDamage(damage);
            }
        }

        void UpdateAura(float dt)
        {
            if (auraLeft <= 0f) return;

            auraLeft -= dt;
            auraTick -= dt;
            if (auraTick > 0f) return;

            auraTick = Mathf.Max(0.05f, skill.tickInterval);
            float dmgMult = stats != null ? stats.DamageMultiplier : 1f;
            DamageAround(transform.position, skill.radius, skill.damage * dmgMult, 0f);
        }

        // ───────────────────────── 연출 ─────────────────────────

        void StartFx(float radius, float duration, bool follow)
        {
            if (fx == null || fx.sprite == null) return;

            fxRadius = radius;
            fxDuration = Mathf.Max(0.05f, duration);
            fxLeft = fxDuration;
            fxFollow = follow;
            fxColor = skill.effectColor;

            fx.transform.position = transform.position;
            fx.enabled = true;
            ApplyFx(0f);
        }

        void UpdateFx(float dt)
        {
            if (fx == null || !fx.enabled) return;

            fxLeft -= dt;
            if (fxLeft <= 0f)
            {
                fx.enabled = false;
                return;
            }

            if (fxFollow) fx.transform.position = transform.position;
            ApplyFx(1f - fxLeft / fxDuration);
        }

        /// <summary>progress 0→1: 지속형은 일정 크기로 점점 옅어지고, 즉발형은 커지면서 사라진다.</summary>
        void ApplyFx(float progress)
        {
            float spriteSize = fx.sprite.bounds.size.x;
            if (spriteSize <= 0f) return;

            float scale = fxFollow ? 1f : Mathf.Lerp(0.3f, 1f, progress);
            float diameter = fxRadius * 2f * scale;
            fx.transform.localScale = Vector3.one * (diameter / spriteSize);

            var c = fxColor;
            c.a *= fxFollow ? 0.55f : 1f - progress;
            fx.color = c;
        }
    }
}
