using System;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using Samkuk.Weapons;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Allies
{
    /// <summary>
    /// 플레이어를 따라다니며 자기 장수의 시작 무기로 자동 공격하는 아군.
    /// 적은 플레이어와 아군 중 가까운 쪽을 노리고(<see cref="IEnemyTarget"/>), 체력이 다하면 쓰러졌다가
    /// <see cref="AllyConfig.ReviveSeconds"/> 뒤 플레이어 곁에서 되살아난다. 걷기 그림은 장수의 걷기 시트를 쓴다.
    /// </summary>
    public class AllyController : MonoBehaviour, IEnemyTarget, IWeaponStats
    {
        const float ArriveDistance = 0.15f;
        const float LookInterval = 0.25f;
        const float LookRange = 6f;
        const float AnimFps = 9f;

        static Sprite pixelSprite;

        HeroData hero;
        int slot;
        PlayerController playerController;
        PlayerStats playerStats;
        EnemyManager enemies;

        SpriteRenderer body;
        Sprite fallbackSprite;
        HeroSpriteSet walkSet;
        Color baseColor = Color.white;
        Weapon weapon;

        float hp;
        float maxHp;
        float invulnTimer;
        float reviveTimer;
        bool downed;

        FacingDir facing = FacingDir.Down;
        float animClock;
        float lookTimer;
        readonly Enemy[] nearest = new Enemy[1];

        Transform barRoot;
        SpriteRenderer barBack;
        SpriteRenderer barFill;

        public HeroData Hero => hero;
        public int Slot => slot;
        public float Hp => hp;
        public float MaxHp => maxHp;
        public bool IsDowned => downed;
        /// <summary>되살아나기까지 남은 시간 (쓰러져 있지 않으면 0).</summary>
        public float ReviveRemaining => downed ? Mathf.Max(0f, reviveTimer) : 0f;
        /// <summary>쓰러진 뒤 되살아나기까지의 시간. 기본은 <see cref="AllyConfig.ReviveSeconds"/> (테스트에서 줄일 수 있음).</summary>
        public float ReviveDuration { get; set; } = AllyConfig.ReviveSeconds;
        public bool IsInvulnerable => invulnTimer > 0f;
        public FacingDir Facing => facing;
        public int WalkFrame { get; private set; }
        public Weapon Weapon => weapon;
        public SpriteRenderer Body => body;

        public event Action<AllyController> Downed;
        public event Action<AllyController> Revived;

        // ───────────────────────── IEnemyTarget / IWeaponStats ─────────────────────────

        public Vector2 Position => transform.position;
        public float Radius => AllyConfig.BodyRadius;
        public bool IsTargetable => !downed;

        /// <summary>적의 접촉/투사체 피해. 무적 시간이거나 쓰러져 있으면 무시한다.</summary>
        public bool TryContactDamage(float amount)
        {
            if (downed || invulnTimer > 0f || amount <= 0f) return false;

            invulnTimer = AllyConfig.HitInvulnerableSeconds;
            TakeDamage(amount * AllyConfig.DamageTakenFactor);
            return true;
        }

        public float DamageMultiplier => (hero != null ? hero.damageMultiplier : 1f) * AllyConfig.DamageFactor;
        public float CooldownMultiplier => 1f;

        // ───────────────────────── 생성 ─────────────────────────

        /// <summary>
        /// 아군을 설정한다. playerWeapons 는 이펙트(Fx)와 투사체 풀을 빌려 주는 플레이어의 무기 관리자다.
        /// fallback 은 걷기 그림이 없는 장수에게 쓸 기본 스프라이트.
        /// </summary>
        public void Initialize(HeroData heroData, int slotIndex, PlayerController player, WeaponController playerWeapons,
            EnemyManager enemyManager, Sprite fallback)
        {
            hero = heroData;
            slot = slotIndex;
            playerController = player;
            playerStats = player != null ? player.GetComponent<PlayerStats>() : null;
            enemies = enemyManager;
            fallbackSprite = fallback;

            maxHp = AllyConfig.BaseHp + (hero != null ? hero.maxHpBonus : 0f);
            hp = maxHp;

            BuildBody();
            BuildHpBar();

            walkSet = hero != null && hero.walkSheet != null ? HeroSpriteSet.Get(hero.walkSheet, hero.walkPixelsPerUnit) : null;
            baseColor = walkSet != null ? Color.white : (hero != null ? hero.tint : Color.white);
            body.sprite = walkSet != null ? walkSet.Get(facing, 0) : (fallback != null ? fallback : (hero != null ? hero.portrait : null));
            body.color = baseColor;

            if (hero != null && hero.startingWeapon != null) BuildWeapon(hero.startingWeapon, playerWeapons);
            transform.position = GoalPosition();
            RefreshBar();
        }

        void BuildBody()
        {
            var go = new GameObject("Body");
            go.transform.SetParent(transform, false);
            body = go.AddComponent<SpriteRenderer>();
            body.sortingLayerName = GameLayers.Sorting.Player;
            body.sortingOrder = -1; // 주인공 뒤
        }

        void BuildWeapon(WeaponData data, WeaponController playerWeapons)
        {
            var go = new GameObject($"Weapon_{data.displayName}");
            go.transform.SetParent(transform, false);

            weapon = WeaponFactory.Attach(go, data.type);
            if (weapon == null)
            {
                Destroy(go);
                return;
            }
            weapon.Initialize(data, playerWeapons, transform, this, enemies);
        }

        // ───────────────────────── 체력 / 쓰러짐 / 부활 ─────────────────────────

        public void TakeDamage(float amount)
        {
            if (downed || amount <= 0f) return;

            hp = Mathf.Max(0f, hp - amount);
            RefreshBar();
            if (hp <= 0f) Down();
        }

        /// <summary>쓰러뜨린다 (적 피해로 체력이 다하면 자동 호출).</summary>
        public void Down()
        {
            if (downed) return;

            downed = true;
            hp = 0f;
            reviveTimer = ReviveDuration;
            invulnTimer = 0f;
            if (weapon != null) weapon.gameObject.SetActive(false);

            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
            RefreshBar();
            Downed?.Invoke(this);
        }

        /// <summary>되살아나 플레이어 곁에 선다 (체력 일부 회복 + 잠깐 무적).</summary>
        public void Revive()
        {
            if (!downed) return;

            downed = false;
            hp = maxHp * AllyConfig.ReviveHpRatio;
            invulnTimer = AllyConfig.ReviveInvulnerableSeconds;
            if (weapon != null) weapon.gameObject.SetActive(true);

            body.transform.localRotation = Quaternion.identity;
            body.color = baseColor;
            transform.position = GoalPosition();
            RefreshBar();
            Revived?.Invoke(this);
        }

        /// <summary>최대 체력을 즉시 채운다.</summary>
        public void HealFull()
        {
            if (downed) return;
            hp = maxHp;
            RefreshBar();
        }

        /// <summary>플레이어 레벨이 오르면 호출한다. 일정 레벨마다 아군 무기도 강해진다.</summary>
        public void SetPlayerLevel(int playerLevel)
        {
            if (weapon == null) return;

            int wanted = AllyConfig.WeaponLevelFor(playerLevel);
            while (weapon.Level < wanted && !weapon.IsMaxLevel) weapon.LevelUp();
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            float dt = Time.deltaTime;

            if (downed)
            {
                reviveTimer -= dt;
                RefreshBar();
                if (reviveTimer <= 0f) Revive();
                return;
            }

            UpdateInvulnerability(dt);
            bool moving = Follow(dt, out Vector2 moveDir);
            UpdateFacing(dt, moving, moveDir);
            UpdateAnimation(dt, moving);
        }

        void UpdateInvulnerability(float dt)
        {
            if (invulnTimer <= 0f) return;

            invulnTimer -= dt;
            if (invulnTimer <= 0f)
                body.color = baseColor;
            else
                body.color = Mathf.FloorToInt(invulnTimer * 20f) % 2 == 0
                    ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f)
                    : baseColor;
        }

        Vector2 GoalPosition()
        {
            Vector2 center = playerController != null ? (Vector2)playerController.transform.position : (Vector2)transform.position;
            Vector2 goal = center + AllyConfig.OffsetOf(slot);
            // 자리에 나무가 서 있으면 그 곁으로 (나무 속을 목표로 삼으면 영원히 밀어대기만 한다)
            var terrain = TerrainCollision.Active;
            return terrain != null ? terrain.PushOut(goal, AllyConfig.BodyRadius) : goal;
        }

        /// <summary>플레이어 곁 자리로 이동한다. 움직였으면 true.</summary>
        bool Follow(float dt, out Vector2 moveDir)
        {
            moveDir = Vector2.zero;
            if (playerController == null) return false;

            Vector2 pos = transform.position;
            Vector2 goal = GoalPosition();
            Vector2 to = goal - pos;
            float dist = to.magnitude;

            if (dist > AllyConfig.TeleportDistance)
            {
                transform.position = goal;
                return false;
            }
            if (dist <= ArriveDistance) return false;

            float speed = playerController.MoveSpeed * (playerStats != null ? playerStats.MoveSpeedMultiplier : 1f)
                          * AllyConfig.FollowSpeedMultiplier;
            var terrain = TerrainCollision.Active;
            if (terrain != null) speed *= terrain.SpeedFactor(pos);   // 플레이어처럼 물에서 느려진다

            // 자리에 가까워지면 천천히 도착해 떨리지 않게
            float step = Mathf.Min(dist, speed * dt * Mathf.Clamp(dist * 2f, 0.4f, 1f));
            moveDir = to / dist;
            Vector2 velocity = moveDir * (dt > 1e-5f ? step / dt : 0f);
            if (terrain != null) velocity = terrain.Resolve(pos, AllyConfig.BodyRadius, velocity, dt);   // 나무/바위를 따라 미끄러진다
            if (velocity.sqrMagnitude < 1e-6f) return false;

            transform.position = pos + velocity * dt;
            return true;
        }

        void UpdateFacing(float dt, bool moving, Vector2 moveDir)
        {
            if (moving)
            {
                facing = PlayerAnimator.PickDirection(moveDir, facing);
                return;
            }

            // 서 있을 때는 가까운 적을 바라본다
            lookTimer -= dt;
            if (lookTimer > 0f || enemies == null) return;
            lookTimer = LookInterval;

            if (enemies.FindNearest(transform.position, LookRange, nearest) > 0 && nearest[0] != null)
                facing = PlayerAnimator.PickDirection(nearest[0].Position - (Vector2)transform.position, facing);
        }

        void UpdateAnimation(float dt, bool moving)
        {
            if (walkSet == null) return;

            if (moving)
            {
                animClock += dt * AnimFps;
                WalkFrame = ((int)animClock + 1) % HeroSpriteSet.Columns;
            }
            else
            {
                animClock = 0f;
                WalkFrame = 0;
            }

            var sprite = walkSet.Get(facing, WalkFrame);
            if (body.sprite != sprite) body.sprite = sprite;
        }

        void OnDestroy()
        {
            // 파괴된 아군을 적이 계속 노리지 않도록
            if (enemies != null) enemies.UnregisterTarget(this);
        }

        // ───────────────────────── 체력 막대 ─────────────────────────

        static Sprite PixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // 피벗을 왼쪽 가운데로 두어 scale.x 로 길이를 줄이면 왼쪽부터 채워진다
            pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            return pixelSprite;
        }

        SpriteRenderer MakeBarPart(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(barRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprite();
            sr.color = color;
            sr.sortingLayerName = GameLayers.Sorting.Player;
            sr.sortingOrder = order;
            return sr;
        }

        void BuildHpBar()
        {
            var go = new GameObject("HpBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            barRoot = go.transform;

            barBack = MakeBarPart("Back", new Color(0.08f, 0.06f, 0.06f, 0.85f), 20);
            barBack.transform.localPosition = new Vector3(-0.42f, 0f, 0f);
            barBack.transform.localScale = new Vector3(0.84f, 0.13f, 1f);

            barFill = MakeBarPart("Fill", new Color(0.35f, 0.9f, 0.45f), 21);
            barFill.transform.localPosition = new Vector3(-0.4f, 0f, 0f);
            barFill.transform.localScale = new Vector3(0.8f, 0.09f, 1f);
        }

        /// <summary>살아 있으면 체력 비율, 쓰러져 있으면 부활까지 진행률(푸른색)을 보여 준다.</summary>
        void RefreshBar()
        {
            if (barFill == null) return;

            float ratio;
            if (downed)
            {
                ratio = 1f - Mathf.Clamp01(reviveTimer / Mathf.Max(0.01f, ReviveDuration));
                barFill.color = new Color(0.45f, 0.7f, 1f);
            }
            else
            {
                ratio = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
                barFill.color = Color.Lerp(new Color(0.95f, 0.3f, 0.25f), new Color(0.35f, 0.9f, 0.45f), ratio);
            }
            barFill.transform.localScale = new Vector3(0.8f * ratio, 0.09f, 1f);
        }
    }
}
