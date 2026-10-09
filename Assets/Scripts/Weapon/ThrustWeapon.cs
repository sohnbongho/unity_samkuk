using System.Collections.Generic;
using Samkuk.Enemies;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 가장 가까운 적 방향으로 긴 창을 내질러 직선상(길이 range, 폭 size)의 모든 적을 꿰뚫는다.
    /// count가 2 이상이면 360도를 균등 분할한 방향으로 동시에 찌른다.
    /// 찌르기 동작(Step 10-9): 창 그림이 손 축에서 살짝 당겨졌다가 앞으로 나가고 거둔다(<see cref="ThrustMotion"/>, 몸도 그쪽을 본다).
    /// 내지르는 중간에 직선 판정이 들어가고, 창 끝에서 뾰족한 검기가 앞으로 날아간다(장비의 장팔사모는 멀리 날며 피해).
    /// 창이 닿을 만한 거리 안에 적이 있을 때만 찌른다(헛손질로 쿨다운을 낭비하지 않음).
    /// </summary>
    public class ThrustWeapon : Weapon
    {
        /// <summary>동시에 찌르는 최대 수(그림/검기 개수). count 가 더 커도 이만큼만 그린다.</summary>
        public const int MaxThrusts = 4;
        /// <summary>손 축 높이(베기와 같음).</summary>
        public const float HandHeight = SlashWeapon.HandHeight;
        /// <summary>내지를 때 손잡이가 나가는 거리 = range x 이 값. 창 그림 길이(약 1.4유닛)를 더하면 창 끝이 대략 사거리에 닿는다.</summary>
        public const float ReachRatio = 0.45f;
        /// <summary>검기 기본 비거리(range 배율, 장식용). 무기의 trailTravel 이 있으면 그 값(유닛).</summary>
        public const float TrailTravel = 0.3f;
        /// <summary>검기 그림 반지름 = size(반폭) x 이 값. 창은 가늘어 검기도 작다.</summary>
        public const float TrailRadiusRatio = 2.2f;
        public const float LookHoldSeconds = SlashWeapon.LookHoldSeconds;

        readonly Enemy[] nearest = new Enemy[1];
        readonly List<Enemy> hits = new List<Enemy>(64);
        readonly ThrustMotion[] thrusts = new ThrustMotion[MaxThrusts];
        readonly SpriteRenderer[] held = new SpriteRenderer[MaxThrusts];
        readonly WeaponTrail[] trails = new WeaponTrail[MaxThrusts];
        ILookOverride look;
        float timer;

        public bool IsThrusting
        {
            get
            {
                for (int i = 0; i < thrusts.Length; i++) if (thrusts[i].Active) return true;
                return false;
            }
        }

        public SpriteRenderer HeldRenderer(int i) => i >= 0 && i < held.Length ? held[i] : null;
        public SpriteRenderer TrailRenderer(int i) => i >= 0 && i < trails.Length ? trails[i].Renderer : null;

        protected override void OnInitialized()
        {
            timer = Cooldown;
            look = Owner != null ? Owner.GetComponent<ILookOverride>() : null;
            for (int i = 0; i < MaxThrusts; i++)
            {
                thrusts[i] = new ThrustMotion();
                trails[i] = new WeaponTrail(MakeEffectRenderer($"Trail{i}", WaveSprites.Point(), 0));
                if (Data.heldSprite != null) held[i] = MakeEffectRenderer($"Held{i}", Data.heldSprite, 1);
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < thrusts.Length; i++)
            {
                thrusts[i]?.Stop();
                trails[i]?.Stop();
                if (held[i] != null) held[i].enabled = false;
            }
        }

        void Update()
        {
            for (int i = 0; i < trails.Length; i++) TickTrail(trails[i]);
            TickThrusts();

            if (Enemies == null) return;

            timer = Mathf.Min(timer + Time.deltaTime, Cooldown);
            if (timer < Cooldown) return;

            Vector2 origin = OwnerPosition;
            // 창이 닿을 만한 거리 안에 적이 있을 때만 찌른다 (헛손질로 쿨다운을 낭비하지 않음)
            if (Enemies.FindNearest(origin, Data.range + 1f, nearest) == 0 || nearest[0] == null) return;

            timer = 0f;
            PlayAttackSound();
            Vector2 toTarget = nearest[0].Position - origin;
            float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            nearest[0] = null;

            int n = Mathf.Clamp(Count, 1, MaxThrusts);
            float reach = Data.range * ReachRatio;
            for (int i = 0; i < n; i++)
                thrusts[i].Start(baseAngle + 360f * i / n, reach, Data.swingDuration);

            float seconds = Data.swingDuration > 0f ? Data.swingDuration : ThrustMotion.DefaultDuration;
            look?.Look(DirectionOf(baseAngle), seconds + LookHoldSeconds);
        }

        void TickThrusts()
        {
            Vector2 hand = OwnerPosition + new Vector2(0f, HandHeight);
            for (int i = 0; i < thrusts.Length; i++)
            {
                var t = thrusts[i];
                if (!t.Active) continue;

                if (t.Advance(Time.deltaTime)) Thrust(i, OwnerPosition, t.Angle);

                var sr = held[i];
                if (sr == null) continue;
                if (!t.Active)
                {
                    sr.enabled = false;
                    continue;
                }
                // 창 그림은 손잡이가 아래, 촉이 위(+y): 각도 - 90 만큼 돌리고 손 축에서 Offset 만큼 앞으로
                sr.enabled = true;
                sr.flipX = t.FacesLeft;
                sr.transform.position = hand + DirectionOf(t.Angle) * t.Offset;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, t.Angle - 90f);
            }
        }

        void Thrust(int index, Vector2 origin, float angleDegrees)
        {
            Vector2 dir = DirectionOf(angleDegrees);
            float length = Data.range;
            float halfWidth = Data.size;

            hits.Clear();
            Enemies.OverlapCircle(origin + dir * (length * 0.5f), length * 0.5f + halfWidth, hits);

            float dmg = Damage;
            for (int i = 0; i < hits.Count; i++)
            {
                Enemy e = hits[i];
                Vector2 rel = e.Position - origin;
                float along = Vector2.Dot(rel, dir);
                float side = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);

                if (along < -e.Radius || along > length + e.Radius) continue;
                if (side > halfWidth + e.Radius) continue;

                e.TakeDamage(dmg);
                Knock(e, origin);
            }

            // 검기: 창 끝(사거리 끝) 손 높이에서 뾰족한 띠가 앞으로 날아간다
            float scale = Data.trailScale > 0f ? Data.trailScale : 1f;
            float radius = Mathf.Max(0.3f, halfWidth * TrailRadiusRatio) * scale;
            float distance = WeaponTrail.DistanceFor(Data, length * TrailTravel);
            trails[index].Start(origin + new Vector2(0f, HandHeight) + dir * length, dir, angleDegrees, radius, WaveSprites.RadiusUnits,
                distance, WeaponTrail.SecondsFor(Data, distance), Data.tint);
        }
    }
}
