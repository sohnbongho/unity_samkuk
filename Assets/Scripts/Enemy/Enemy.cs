using System;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Enemies
{
    /// <summary>
    /// 개별 적. 이동/겹침 방지는 EnemyManager가 일괄 처리하므로 이 클래스는 체력과 피격 연출을 담당한다.
    /// 풀링 대상이며 Init 으로 매번 재설정된다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        const float FlashDuration = 0.1f;
        const float FlashScalePunch = 0.25f;
        static readonly Color StunTint = new Color(0.6f, 0.8f, 1f);
        const float ShotWarningSeconds = 0.4f;
        static readonly Color ShotWarningTint = new Color(1f, 1f, 0.45f);
        const float KnockbackDecay = 8f;
        const float MaxKnockbackSpeed = 14f;

        enum Phase { Chase, Windup, Charge }

        Rigidbody2D body;
        SpriteRenderer sr;
        CircleCollider2D col;
        Sprite defaultSprite;
        float flashTimer;
        float baseScale = 1f;
        float stunTimer;
        Vector2 knockVelocity;
        float shootClock;
        bool telegraphing;

        // 걷기 애니메이션 (EnemyManager가 이동 루프에서 갱신)
        HeroSpriteSet walkSet;
        FacingDir facing = FacingDir.Down;
        float animClock;
        float animPhase;
        float animFps = 6f;

        // 돌진 패턴 상태
        Phase phase;
        float phaseTimer;
        float chargeClock;
        Vector2 chargeDir = Vector2.right;

        public EnemyData Data { get; private set; }
        public float Hp { get; private set; }
        public bool Alive { get; private set; }
        /// <summary>월드 기준 반지름 (콜라이더 반지름 × 스케일).</summary>
        public float Radius { get; private set; }
        public Rigidbody2D Body => body;
        /// <summary>걷기 시트를 쓰는 중인가 (false 면 단색 스프라이트 + 좌우 반전).</summary>
        public bool HasWalkSheet => walkSet != null;
        public FacingDir Facing => facing;
        /// <summary>현재 걷기 프레임 (0~3, 서 있으면 0).</summary>
        public int WalkFrame { get; private set; }
        /// <summary>평소 색. 걷기 그림이 있으면 그림 색 그대로(흰색), 없으면 데이터의 tint.</summary>
        Color BaseColor => walkSet != null ? Color.white : Data.tint;
        public Vector2 Position => body.position;

        /// <summary>피해를 받았을 때 (적, 피해량). 데미지 숫자 표시 등에 사용.</summary>
        public static event Action<Enemy, float> Damaged;
        /// <summary>사망했을 때. 경험치 드롭 등에 사용 (이 시점에 위치/데이터는 아직 유효).</summary>
        public static event Action<Enemy> Died;

        /// <summary>EnemyManager 내부 리스트 인덱스 (-1이면 미등록).</summary>
        internal int ManagerIndex = -1;
        /// <summary>범위 질의 중복 방지용 스탬프.</summary>
        internal int QueryStamp;
        /// <summary>풀 반환 콜백 (스포너가 설정).</summary>
        internal Action<Enemy> DespawnHandler;
        /// <summary>장애물에 정면으로 막혔을 때 돌아가는 쪽 (+1 왼쪽 접선 / -1 오른쪽). 무리가 한쪽으로만 몰리지 않게 마리마다 다르다.</summary>
        internal int SteerSide = 1;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();
            col = GetComponent<CircleCollider2D>();
            defaultSprite = sr.sprite;
            enabled = false; // Update는 피격 연출 중에만 돈다
        }

        public void Init(EnemyData data, Vector2 position)
        {
            Data = data;
            Hp = data.maxHp;
            Alive = true;
            baseScale = data.scale;
            flashTimer = 0f;
            stunTimer = 0f;
            knockVelocity = Vector2.zero;
            shootClock = 0f;
            telegraphing = false;
            phase = Phase.Chase;
            chargeClock = 0f;
            enabled = false;
            walkSet = data.walkSheet != null ? HeroSpriteSet.Get(data.walkSheet, data.walkPixelsPerUnit) : null;
            facing = FacingDir.Down;
            animClock = 0f;
            WalkFrame = 0;
            animPhase = UnityEngine.Random.value * HeroSpriteSet.Columns; // 무리가 같은 발로 걷지 않도록
            SteerSide = UnityEngine.Random.value < 0.5f ? 1 : -1;
            animFps = Mathf.Clamp(data.moveSpeed * 2.5f, 4f, 12f);
            sr.sprite = walkSet != null ? walkSet.Get(facing, 0) : (data.sprite != null ? data.sprite : defaultSprite);
            sr.color = BaseColor;
            sr.flipX = false;
            transform.localScale = Vector3.one * baseScale;
            col.radius = data.colliderRadius;
            Radius = data.colliderRadius * baseScale;
            Teleport(position);
        }

        public void Teleport(Vector2 position)
        {
            transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public void SetFacing(bool faceLeft) => sr.flipX = faceLeft;

        /// <summary>
        /// 걷기 애니메이션을 dt만큼 진행한다. faceDir 쪽을 바라보고(보통 플레이어 방향), moving 이면 프레임을 돌린다.
        /// 시트가 없으면 아무것도 하지 않는다. 스프라이트는 바뀔 때만 교체해 많은 적에서도 가볍다.
        /// </summary>
        public void TickAnimation(Vector2 faceDir, bool moving, float dt)
        {
            if (walkSet == null) return;

            facing = PlayerAnimator.PickDirection(faceDir, facing);
            if (moving)
            {
                animClock += dt * animFps * (IsCharging ? 1.6f : 1f);
                WalkFrame = ((int)(animClock + animPhase) + 1) % HeroSpriteSet.Columns;
            }
            else
            {
                WalkFrame = 0;
            }

            var sprite = walkSet.Get(facing, WalkFrame);
            if (sr.sprite != sprite) sr.sprite = sprite;
        }

        /// <summary>발사 예고 중인가 (노랗게 깜빡임).</summary>
        public bool IsTelegraphingShot => telegraphing;

        /// <summary>
        /// 사격 타이머를 dt만큼 진행시킨다. 사정거리 안(canShoot)이고 발사 간격이 찼으면 true(= 지금 발사).
        /// 사정거리 밖에서는 발사 예고 직전까지만 시간이 쌓인다.
        /// </summary>
        internal bool TickShoot(float dt, bool canShoot)
        {
            if (Data.attackRange <= 0f) return false;

            float interval = Mathf.Max(0.1f, Data.fireInterval);
            float warnAt = Mathf.Max(0f, interval - ShotWarningSeconds);
            shootClock += dt;

            if (!canShoot)
            {
                shootClock = Mathf.Min(shootClock, warnAt);
                SetTelegraph(false);
                return false;
            }

            if (shootClock >= interval)
            {
                shootClock = 0f;
                SetTelegraph(false);
                return true;
            }

            SetTelegraph(shootClock >= warnAt);
            return false;
        }

        void SetTelegraph(bool on)
        {
            if (telegraphing == on) return;
            telegraphing = on;
            sr.color = on ? ShotWarningTint : BaseColor;
        }

        /// <summary>현재 넉백 속도 (시간이 지나며 감쇠).</summary>
        public Vector2 KnockbackVelocity => knockVelocity;

        /// <summary>순간적으로 밀려나게 한다 (velocity는 초당 이동 거리, 감쇠하며 사라짐).</summary>
        public void Knockback(Vector2 velocity)
        {
            if (!Alive) return;
            knockVelocity = Vector2.ClampMagnitude(knockVelocity + velocity, MaxKnockbackSpeed);
        }

        /// <summary>넉백 속도를 반환하고 dt만큼 감쇠시킨다.</summary>
        internal Vector2 TickKnockback(float dt)
        {
            if (knockVelocity == Vector2.zero) return Vector2.zero;

            Vector2 current = knockVelocity;
            knockVelocity *= Mathf.Exp(-KnockbackDecay * dt);
            if (knockVelocity.sqrMagnitude < 0.01f) knockVelocity = Vector2.zero;
            return current;
        }

        public bool IsStunned => stunTimer > 0f;

        /// <summary>일정 시간 움직이지 못하게 한다 (진행 중인 돌진 패턴은 취소).</summary>
        public void Stun(float seconds)
        {
            if (!Alive || seconds <= 0f) return;
            stunTimer = Mathf.Max(stunTimer, seconds);
            shootClock = 0f;
            telegraphing = false;
            phase = Phase.Chase;
            chargeClock = 0f;
            sr.color = StunTint;
        }

        /// <summary>기절 시간을 dt만큼 진행시킨다. 아직 기절 중이면 true.</summary>
        internal bool TickStun(float dt)
        {
            if (stunTimer <= 0f) return false;
            stunTimer -= dt;
            if (stunTimer > 0f) return true;

            sr.color = BaseColor;
            return false;
        }

        /// <summary>돌진 중인가 (예고 포함하지 않음).</summary>
        public bool IsCharging => phase == Phase.Charge;
        /// <summary>돌진 예고 중인가.</summary>
        public bool IsWindingUp => phase == Phase.Windup;

        /// <summary>
        /// 돌진 패턴 상태를 갱신한다. 패턴이 이동을 지배하는 동안(예고/돌진) true를 반환하고
        /// velocity에 원하는 속도(예고 중에는 0)를 돌려준다.
        /// </summary>
        internal bool TickCharge(float dt, Vector2 dirToPlayer, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (Data.chargeInterval <= 0f) return false;

            switch (phase)
            {
                case Phase.Chase:
                    chargeClock += dt;
                    if (chargeClock >= Data.chargeInterval)
                    {
                        phase = Phase.Windup;
                        phaseTimer = Data.chargeWindup;
                        chargeDir = dirToPlayer;
                        sr.color = new Color(1f, 0.3f, 0.3f);
                        return true; // 예고 시작 틱부터 멈춘다
                    }
                    return false;

                case Phase.Windup:
                    chargeDir = dirToPlayer; // 예고 중에는 플레이어를 계속 조준
                    phaseTimer -= dt;
                    if (phaseTimer <= 0f)
                    {
                        phase = Phase.Charge;
                        phaseTimer = Data.chargeDuration;
                        sr.color = BaseColor;
                    }
                    return true;

                default: // Charge
                    phaseTimer -= dt;
                    velocity = chargeDir * (Data.moveSpeed * Data.chargeSpeedMultiplier);
                    if (phaseTimer <= 0f)
                    {
                        phase = Phase.Chase;
                        chargeClock = 0f;
                    }
                    return true;
            }
        }

        public void TakeDamage(float amount)
        {
            if (!Alive || amount <= 0f) return;

            Hp -= amount;
            Damaged?.Invoke(this, amount);

            if (Hp <= 0f)
            {
                Alive = false;
                Died?.Invoke(this);
                Despawn();
            }
            else
            {
                flashTimer = FlashDuration;
                enabled = true;
            }
        }

        void Update()
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / FlashDuration);
            transform.localScale = Vector3.one * (baseScale * (1f + FlashScalePunch * t));
            if (flashTimer <= 0f) enabled = false;
        }

        /// <summary>풀로 반환한다.</summary>
        public void Despawn() => DespawnHandler?.Invoke(this);
    }
}
