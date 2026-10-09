using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 찌르기 한 번의 순수 규칙(Step 10-9): 창을 살짝 당겼다가 앞으로 내지르고 거둔다. 손 축에서 창이 나간 거리(유닛)를 준다.
    /// 타격은 내지르는 중간(<see cref="HitFraction"/>)에 들어간다. 각도는 월드 기준 도.
    /// </summary>
    public sealed class ThrustMotion
    {
        public const float DefaultDuration = 0.22f;
        /// <summary>당기는 구간이 끝나는 진행 비율.</summary>
        public const float PullEnd = 0.25f;
        /// <summary>내지르기가 끝나는(가장 멀리 나간) 진행 비율.</summary>
        public const float LungeEnd = 0.55f;
        /// <summary>호의 어느 지점에서 피해가 들어가는가. 내지르는 도중.</summary>
        public const float HitFraction = 0.4f;
        /// <summary>당길 때 뒤로 빠지는 거리 = reach x 이 값.</summary>
        public const float PullRatio = 0.15f;

        float elapsed, duration, reach, center;
        bool hitDone;

        public bool Active { get; private set; }
        public float Angle => center;
        public bool FacesLeft => Mathf.Cos(center * Mathf.Deg2Rad) < 0f;
        public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        /// <summary>손 축에서 창 손잡이가 나간 거리(유닛). 당기면 음수, 내지르면 reach 까지.</summary>
        public float Offset
        {
            get
            {
                float p = Progress;
                if (p < PullEnd) return -reach * PullRatio * (p / PullEnd);
                if (p < LungeEnd)
                {
                    float q = (p - PullEnd) / (LungeEnd - PullEnd);
                    float eased = 1f - (1f - q) * (1f - q);              // 빠르게 나가다 느려진다
                    return Mathf.Lerp(-reach * PullRatio, reach, eased);
                }
                float r = (p - LungeEnd) / (1f - LungeEnd);
                return Mathf.Lerp(reach, 0f, r * r);                     // 천천히 거둔다
            }
        }

        /// <summary>찌르기를 시작한다. reach 는 내지를 때 손잡이가 나가는 거리(유닛). duration 이 0 이하면 기본값.</summary>
        public void Start(float angleDegrees, float reachUnits, float durationSeconds)
        {
            center = angleDegrees;
            reach = Mathf.Max(0f, reachUnits);
            duration = durationSeconds > 0f ? durationSeconds : DefaultDuration;
            elapsed = 0f;
            hitDone = false;
            Active = true;
        }

        /// <summary>시간을 밀고, 이번 호출에서 타격 시점을 지났으면 true 를 한 번만 돌려준다.</summary>
        public bool Advance(float deltaTime)
        {
            if (!Active) return false;

            elapsed += Mathf.Max(0f, deltaTime);
            bool hitNow = false;
            if (!hitDone && elapsed >= duration * HitFraction)
            {
                hitDone = true;
                hitNow = true;
            }
            if (elapsed >= duration) Active = false;
            return hitNow;
        }

        public void Stop()
        {
            Active = false;
            hitDone = true;
        }
    }
}
