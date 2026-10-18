using Samkuk.Data;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 극(여포의 방천화극): 가까운 적 쪽으로 **찌르기와 휩쓸기를 번갈아** 한다.
    /// 찌르기는 창과 같은 직선 판정(길이 range, 반폭 size)에 뾰족한 검기, 휩쓸기는 베기와 같은 원 판정에 넓은 호(기본 200도)와 반원 검기이며
    /// 적을 더 세게 밀어낸다(<see cref="SweepKnockScale"/>). count 가 2 면 반대편도 함께 찌르고 휩쓴다.
    /// 동작 규칙은 창(<see cref="ThrustMotion"/>)과 베기(<see cref="SwingMotion"/>)를 그대로 쓰고, 그림 한 장이 두 동작을 오간다.
    /// 사거리 안에 적이 있을 때만 움직인다(쿨다운을 낭비하지 않음).
    /// </summary>
    public class HalberdWeapon : Weapon
    {
        /// <summary>동시에 움직이는 최대 수(앞/뒤).</summary>
        public const int MaxStrikes = 2;
        public const float HandHeight = SlashWeapon.HandHeight;
        /// <summary>휩쓸기 호의 기본 각도. 무기의 swingArcDegrees 가 0 이면 이 값(베기보다 넓게).</summary>
        public const float DefaultSweepArc = 200f;
        /// <summary>휩쓸기 넉백 배율(찌르기는 1).</summary>
        public const float SweepKnockScale = 1.5f;
        /// <summary>찌를 때 손잡이가 나가는 거리 = range x 이 값 (창과 같음).</summary>
        public const float ReachRatio = ThrustWeapon.ReachRatio;
        public const float LookHoldSeconds = SlashWeapon.LookHoldSeconds;

        readonly Enemy[] nearest = new Enemy[1];
        readonly ThrustMotion[] thrusts = new ThrustMotion[MaxStrikes];
        readonly SwingMotion[] sweeps = new SwingMotion[MaxStrikes];
        readonly SpriteRenderer[] held = new SpriteRenderer[MaxStrikes];
        readonly WeaponTrail[] trails = new WeaponTrail[MaxStrikes];
        ILookOverride look;
        float timer;

        /// <summary>다음 공격이 휩쓸기인가(처음은 찌르기).</summary>
        public bool NextIsSweep { get; private set; }

        public bool IsThrusting
        {
            get
            {
                for (int i = 0; i < thrusts.Length; i++) if (thrusts[i].Active) return true;
                return false;
            }
        }

        public bool IsSweeping
        {
            get
            {
                for (int i = 0; i < sweeps.Length; i++) if (sweeps[i].Active) return true;
                return false;
            }
        }

        public SpriteRenderer HeldRenderer(int i) => i >= 0 && i < held.Length ? held[i] : null;
        public SpriteRenderer TrailRenderer(int i) => i >= 0 && i < trails.Length ? trails[i].Renderer : null;

        protected override void OnInitialized()
        {
            timer = Cooldown;
            look = Owner != null ? Owner.GetComponent<ILookOverride>() : null;
            for (int i = 0; i < MaxStrikes; i++)
            {
                thrusts[i] = new ThrustMotion();
                sweeps[i] = new SwingMotion();
                trails[i] = new WeaponTrail(MakeEffectRenderer($"Trail{i}", WaveSprites.Point(), 0));
                if (Data.heldSprite != null) held[i] = MakeEffectRenderer($"Held{i}", Data.heldSprite, 1);
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < MaxStrikes; i++)
            {
                thrusts[i]?.Stop();
                sweeps[i]?.Stop();
                trails[i]?.Stop();
                if (held[i] != null) held[i].enabled = false;
            }
        }

        void Update()
        {
            for (int i = 0; i < trails.Length; i++) TickTrail(trails[i]);
            TickMotions();

            if (Enemies == null) return;

            timer = Mathf.Min(timer + Time.deltaTime, Cooldown);
            if (timer < Cooldown) return;

            Vector2 origin = OwnerPosition;
            if (Enemies.FindNearest(origin, Data.range + 0.5f, nearest) == 0 || nearest[0] == null) return;

            timer = 0f;
            Vector2 toTarget = nearest[0].Position - origin;
            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            nearest[0] = null;

            int n = Mathf.Clamp(Count, 1, MaxStrikes);
            float seconds;
            if (NextIsSweep)
            {
                PlayAttackSound(WeaponType.Slash);
                float arc = Data.swingArcDegrees > 0f ? Data.swingArcDegrees : DefaultSweepArc;
                for (int i = 0; i < n; i++) sweeps[i].Start(angle + 180f * i, arc, Data.swingDuration);
                seconds = Data.swingDuration > 0f ? Data.swingDuration : SwingMotion.DefaultDuration;
            }
            else
            {
                PlayAttackSound(WeaponType.Thrust);
                float reach = Data.range * ReachRatio;
                for (int i = 0; i < n; i++) thrusts[i].Start(angle + 180f * i, reach, Data.swingDuration);
                seconds = Data.swingDuration > 0f ? Data.swingDuration : ThrustMotion.DefaultDuration;
            }
            NextIsSweep = !NextIsSweep;

            look?.Look(DirectionOf(angle), seconds + LookHoldSeconds);
        }

        void TickMotions()
        {
            Vector2 hand = OwnerPosition + new Vector2(0f, HandHeight);
            for (int i = 0; i < MaxStrikes; i++)
            {
                var t = thrusts[i];
                var s = sweeps[i];
                var sr = held[i];

                if (t.Active)
                {
                    if (t.Advance(Time.deltaTime)) Thrust(i, OwnerPosition, t.Angle);
                    if (sr == null) continue;
                    if (!t.Active) { sr.enabled = false; continue; }
                    // 그림은 손잡이가 아래, 날이 위(+y): 각도 - 90 만큼 돌리고 손 축에서 Offset 만큼 앞으로 (창과 같음)
                    sr.enabled = true;
                    sr.flipX = t.FacesLeft;
                    sr.transform.position = hand + DirectionOf(t.Angle) * t.Offset;
                    sr.transform.rotation = Quaternion.Euler(0f, 0f, t.Angle - 90f);
                }
                else if (s.Active)
                {
                    if (s.Advance(Time.deltaTime)) Sweep(i, s.CenterAngle);
                    if (sr == null) continue;
                    if (!s.Active) { sr.enabled = false; continue; }
                    sr.enabled = true;
                    sr.flipX = s.FacesLeft;
                    sr.transform.position = hand;
                    sr.transform.rotation = Quaternion.Euler(0f, 0f, s.Angle - 90f);
                }
            }
        }

        /// <summary>찌르기: 창과 같은 직선 판정, 극 끝에서 뾰족한 검기.</summary>
        void Thrust(int index, Vector2 origin, float angleDegrees)
        {
            Vector2 dir = DirectionOf(angleDegrees);
            float length = Data.range;
            StrikeLine(origin, dir, length, Data.size, Damage);

            float scale = Data.trailScale > 0f ? Data.trailScale : 1f;
            float radius = Mathf.Max(0.3f, Data.size * ThrustWeapon.TrailRadiusRatio) * scale;
            float distance = WeaponTrail.DistanceFor(Data, length * ThrustWeapon.TrailTravel);
            trails[index].Renderer.sprite = WaveSprites.Point();
            trails[index].Start(origin + new Vector2(0f, HandHeight) + dir * length, dir, angleDegrees, radius, WaveSprites.RadiusUnits,
                distance, WeaponTrail.SecondsFor(Data, distance), Data.tint);
        }

        /// <summary>휩쓸기: 베기와 같은 원 판정(중심 range*0.5, 반지름 range*0.6)에 더 센 넉백, 반원 검기.</summary>
        void Sweep(int index, float angleDegrees)
        {
            float radius = Data.range * 0.6f;
            Vector2 dir = DirectionOf(angleDegrees);
            Vector2 center = OwnerPosition + dir * (Data.range * 0.5f);
            StrikeCircle(center, radius, OwnerPosition, Damage, SweepKnockScale);

            float scale = Data.trailScale > 0f ? Data.trailScale : 1f;
            float distance = WeaponTrail.DistanceFor(Data, Data.range * SlashWeapon.TrailTravel);
            trails[index].Renderer.sprite = WaveSprites.Arc();
            trails[index].Start(center + new Vector2(0f, HandHeight), dir, angleDegrees, radius * scale, WaveSprites.RadiusUnits,
                distance, WeaponTrail.SecondsFor(Data, distance), Data.tint);
        }
    }
}
