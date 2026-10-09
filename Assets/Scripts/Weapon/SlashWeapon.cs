using System.Collections.Generic;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 주기적으로 근접 범위(원)를 베어 적에게 피해를 준다. count가 2 이상이면 반대편도 동시에 벤다.
    /// 휘두르기(Step 10-9): 쿨다운이 차면 가장 가까운 적 쪽으로 무기 그림을 호를 그리며 돌리고(몸도 그쪽을 본다),
    /// 호의 중간(<see cref="SwingMotion.HitFraction"/>)에서 그 방향의 원에 피해가 들어가며 반원 검기(호 잔상)가 앞으로 날아간다.
    /// 사거리(range) 안에 적이 없으면 쿨다운을 소모하지 않고 기다렸다가, 들어오는 순간 바로 휘두른다(화살과 같은 규칙).
    /// 무기 그림(<see cref="Data.heldSprite"/>)이 없어도 타이밍과 잔상은 같다.
    /// </summary>
    public class SlashWeapon : Weapon
    {
        /// <summary>동시에 휘두르는 최대 수. count 가 더 커도 두 번(앞/뒤)까지만 — 예전 동작과 같다.</summary>
        public const int MaxSwings = 2;
        /// <summary>손 축: 발(트랜스폼)에서 위로 띄우는 높이. 걷기 그림의 몸(약 1유닛) 중간쯤.</summary>
        public const float HandHeight = 0.55f;
        /// <summary>검기가 피해 원의 중심에서 앞으로 더 나가는 기본 거리(range 배율). 무기의 trailTravel 이 있으면 그 값(유닛).</summary>
        public const float TrailTravel = 0.35f;
        /// <summary>휘두른 뒤에도 그쪽을 더 바라보는 시간. 휘두르는 동안만 보면 깜빡이듯 돌아가 버린다.</summary>
        public const float LookHoldSeconds = 0.15f;

        readonly List<Enemy> hits = new List<Enemy>(64);
        readonly Enemy[] nearest = new Enemy[1];
        readonly SwingMotion[] swings = new SwingMotion[MaxSwings];
        readonly SpriteRenderer[] held = new SpriteRenderer[MaxSwings];
        readonly WeaponTrail[] trails = new WeaponTrail[MaxSwings];
        ILookOverride look;
        float timer;

        /// <summary>테스트/연출 확인용: 지금 휘두르는 중인가.</summary>
        public bool IsSwinging
        {
            get
            {
                for (int i = 0; i < swings.Length; i++) if (swings[i].Active) return true;
                return false;
            }
        }

        /// <summary>i번째 휘두르기의 무기 그림 렌더러 (없으면 null).</summary>
        public SpriteRenderer HeldRenderer(int i) => i >= 0 && i < held.Length ? held[i] : null;
        /// <summary>i번째 휘두르기의 검기(호 잔상) 렌더러.</summary>
        public SpriteRenderer TrailRenderer(int i) => i >= 0 && i < trails.Length ? trails[i].Renderer : null;

        protected override void OnInitialized()
        {
            timer = Cooldown;
            look = Owner != null ? Owner.GetComponent<ILookOverride>() : null;

            for (int i = 0; i < MaxSwings; i++)
            {
                swings[i] = new SwingMotion();
                trails[i] = new WeaponTrail(MakeEffectRenderer($"Trail{i}", WaveSprites.Arc(), 0));
                if (Data.heldSprite != null) held[i] = MakeEffectRenderer($"Held{i}", Data.heldSprite, 1); // 검기보다 앞
            }
        }

        void OnDisable()
        {
            // 아군이 쓰러지면 무기 오브젝트가 꺼진다: 휘두르던 동작을 끊고 그림을 숨긴다
            for (int i = 0; i < swings.Length; i++)
            {
                swings[i]?.Stop();
                trails[i]?.Stop();
                if (held[i] != null) held[i].enabled = false;
            }
        }

        void Update()
        {
            for (int i = 0; i < trails.Length; i++) TickTrail(trails[i]);
            TickSwings();

            if (Enemies == null) return;

            timer = Mathf.Min(timer + Time.deltaTime, Cooldown);
            if (timer < Cooldown) return;

            // 사거리 안에 적이 없으면 준비 상태로 기다린다 (쿨다운은 차 있으므로 적이 들어오면 즉시)
            if (!TryPickAngle(out float angle)) return;
            timer = 0f;
            PlayAttackSound();

            swings[0].Start(angle, Data.swingArcDegrees, Data.swingDuration);
            if (Count >= 2) swings[1].Start(angle + 180f, Data.swingArcDegrees, Data.swingDuration);

            // 몸도 휘두르는 쪽을 본다 (달아나면서 등 뒤로 베는 어색함 방지). 반대편 베기는 보지 않는다
            float swingSeconds = Data.swingDuration > 0f ? Data.swingDuration : SwingMotion.DefaultDuration;
            look?.Look(DirectionOf(angle), swingSeconds + LookHoldSeconds);
        }

        /// <summary>휘두를 방향: 사거리 안 가장 가까운 적. 없으면 false(휘두르지 않음).</summary>
        bool TryPickAngle(out float angle)
        {
            angle = 0f;
            if (Enemies.FindNearest(OwnerPosition, Data.range, nearest) == 0 || nearest[0] == null) return false;
            Vector2 to = nearest[0].Position - OwnerPosition;
            if (to.sqrMagnitude > 1e-6f) angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            return true;
        }

        void TickSwings()
        {
            Vector2 hand = OwnerPosition + new Vector2(0f, HandHeight);
            for (int i = 0; i < swings.Length; i++)
            {
                var s = swings[i];
                if (!s.Active) continue;

                if (s.Advance(Time.deltaTime)) Strike(i, s.CenterAngle);

                var sr = held[i];
                if (sr == null) continue;
                if (!s.Active)
                {
                    sr.enabled = false;
                    continue;
                }
                // 그림은 손잡이가 아래, 날이 위(+y)를 향하므로 각도 - 90 만큼 돌리면 날이 Angle 을 가리킨다
                sr.enabled = true;
                sr.flipX = s.FacesLeft;
                sr.transform.position = hand;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, s.Angle - 90f);
            }
        }

        /// <summary>호의 가운데 방향으로 range*0.5 떨어진 원(반지름 range*0.6)에 피해. 예전 좌/우 베기와 같은 크기.</summary>
        void Strike(int swingIndex, float angleDegrees)
        {
            float radius = Data.range * 0.6f;
            Vector2 dir = DirectionOf(angleDegrees);
            Vector2 center = OwnerPosition + dir * (Data.range * 0.5f);

            hits.Clear();
            Enemies.OverlapCircle(center, radius, hits);
            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                hits[i].TakeDamage(dmg);
                Knock(hits[i], OwnerPosition);
            }

            // 검기: 손 높이의 피해 원 자리에서 반원 띠가 앞으로 날아가며 사라진다. 반원 반지름 = 피해 원 반지름 x 배율
            float scale = Data.trailScale > 0f ? Data.trailScale : 1f;
            float distance = WeaponTrail.DistanceFor(Data, Data.range * TrailTravel);
            trails[swingIndex].Start(center + new Vector2(0f, HandHeight), dir, angleDegrees, radius * scale, WaveSprites.RadiusUnits,
                distance, WeaponTrail.SecondsFor(Data, distance), Data.tint);
        }
    }
}
