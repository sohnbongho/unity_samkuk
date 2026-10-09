using System.Collections.Generic;
using Samkuk.Data;
using Samkuk.Enemies;
using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 검기 하나(Step 10-9): 타격 자리에서 앞으로 날아가며 옅어지는 스프라이트. 베기(반원)와 찌르기(뾰족한)가 함께 쓴다.
    /// 빠르게 나가다 느려지고, 무기의 검기 값이 있으면 멀리(trailTravel) 날며 지나치는 적을 적마다 한 번 다치게 한다
    /// (피해는 <see cref="Weapon.TrailStrike"/> 가 준다). 렌더러는 무기가 만들어 넘긴다.
    /// </summary>
    public sealed class WeaponTrail
    {
        /// <summary>검기가 보이는 최소 시간. 무기의 duration 이나 비거리/속도가 더 길면 그 값.</summary>
        public const float MinSeconds = 0.22f;
        /// <summary>멀리 날아가는 검기의 속도(유닛/초). 비거리를 이 속도로 나눈 시간만큼 보인다.</summary>
        public const float Speed = 10f;
        /// <summary>검기의 피해 원 반지름 = 검기 반지름 x 이 값 (그림 안쪽은 비어 있으므로 조금 작게).</summary>
        public const float HitRadiusRatio = 0.7f;

        readonly List<Enemy> already = new List<Enemy>(32);
        Vector2 from, dir;
        float radius, distance, seconds, left;

        public SpriteRenderer Renderer { get; }
        public bool Active => Renderer != null && Renderer.enabled;
        public Vector2 Direction => dir;
        /// <summary>지금 검기 그림의 자리(손 높이).</summary>
        public Vector2 Position { get; private set; }
        public float HitRadius => radius * HitRadiusRatio;
        /// <summary>이 검기에 이미 맞은 적 (적마다 한 번만).</summary>
        public List<Enemy> AlreadyHit => already;

        public WeaponTrail(SpriteRenderer renderer) => Renderer = renderer;

        /// <summary>검기 비거리(유닛): 무기 값이 있으면 그 값, 없으면 장식용 기본 거리.</summary>
        public static float DistanceFor(WeaponData data, float defaultDistance) =>
            data.trailTravel > 0f ? data.trailTravel : defaultDistance;

        /// <summary>검기가 보이는 시간: 최소 시간, 무기 duration, 비거리/속도 중 가장 긴 것.</summary>
        public static float SecondsFor(WeaponData data, float distance) =>
            Mathf.Max(Mathf.Max(MinSeconds, data.duration), distance / Speed);

        /// <summary>검기를 띄운다. radius 는 그림 반지름(유닛), spriteRadiusUnits 는 배율 1 일 때 그림의 반지름.</summary>
        public void Start(Vector2 fromPosition, Vector2 direction, float angleDegrees, float radiusUnits, float spriteRadiusUnits,
            float travelDistance, float visibleSeconds, Color tint)
        {
            if (Renderer == null) return;
            from = fromPosition;
            dir = direction;
            radius = radiusUnits;
            distance = travelDistance;
            seconds = Mathf.Max(0.01f, visibleSeconds);
            left = seconds;
            already.Clear();
            Position = from;

            var t = Renderer.transform;
            t.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
            t.localScale = Vector3.one * (radiusUnits / spriteRadiusUnits);
            t.position = from;
            Renderer.color = tint;
            Renderer.enabled = true;
        }

        /// <summary>시간을 밀어 자리/투명도를 갱신한다. 끝나면 숨긴다.</summary>
        public void Tick(float deltaTime, Color tint)
        {
            if (!Active) return;

            left -= deltaTime;
            float p = 1f - Mathf.Clamp01(left / seconds);     // 0 → 1
            float ease = 1f - (1f - p) * (1f - p);             // 빠르게 나가다 느려진다
            Position = from + dir * (distance * ease);
            Renderer.transform.position = Position;
            tint.a *= 1f - p;
            Renderer.color = tint;
            if (left <= 0f) Renderer.enabled = false;
        }

        public void Stop()
        {
            if (Renderer != null) Renderer.enabled = false;
            already.Clear();
        }
    }
}
