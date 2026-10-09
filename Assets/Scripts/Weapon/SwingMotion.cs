using UnityEngine;

namespace Samkuk.Weapons
{
    /// <summary>
    /// 휘두르기 한 번의 순수 규칙(Step 10-9): 손 축을 중심으로 호를 그리는 무기 각도와, 호의 중간에 들어가는 타격 시점.
    /// MonoBehaviour 가 아니라 테스트에서 시간을 직접 밀어 검사한다. 각도는 월드 기준 도(0 = 오른쪽, 90 = 위).
    /// 들어 올림 → 휘두름 → 거둠이 한 호 안에 들어 있고(느리게 시작해 빠르게 지나 느리게 끝남), 타격은 <see cref="HitFraction"/> 지점.
    /// </summary>
    public sealed class SwingMotion
    {
        /// <summary>무기 데이터의 값이 0 일 때 쓰는 기본값. 베기 계열이 모두 같은 느낌이 되지 않도록 셋업이 무기마다 다르게 채운다.</summary>
        public const float DefaultArcDegrees = 120f;
        public const float DefaultDuration = 0.25f;
        /// <summary>호의 어느 지점에서 피해가 들어가는가(0~1). 들어 올리는 동작이 보인 뒤 맞아야 휘두름과 피해가 맞아 보인다.</summary>
        public const float HitFraction = 0.4f;

        float elapsed;
        float duration;
        float arc;
        float center;
        int sign;
        bool hitDone;

        /// <summary>휘두르는 중인가. 끝나면 false 가 되고 무기 그림을 숨긴다.</summary>
        public bool Active { get; private set; }
        /// <summary>휘두르는 방향(호의 가운데) 각도.</summary>
        public float CenterAngle => center;
        /// <summary>진행 비율 0~1 (시간 기준, 완화 전).</summary>
        public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        /// <summary>왼쪽으로 휘두르는가(그림을 좌우 반전할지 정한다). 호의 가운데가 왼쪽 반평면이면 참.</summary>
        public bool FacesLeft => Mathf.Cos(center * Mathf.Deg2Rad) < 0f;

        /// <summary>
        /// 지금 무기가 가리키는 각도. 호의 한쪽 끝에서 시작해 반대쪽 끝에서 끝난다.
        /// 오른쪽을 향하면 위에서 아래로(시계 방향), 왼쪽을 향하면 역시 위에서 아래로(반시계) 내려치도록 부호를 정한다.
        /// </summary>
        public float Angle
        {
            get
            {
                float p = Progress;
                float eased = p * p * (3f - 2f * p); // 느리게 시작해 빠르게 지나 느리게 끝남
                return center + sign * arc * (0.5f - eased);
            }
        }

        /// <summary>휘두르기를 시작한다. arcDegrees/duration 이 0 이하면 기본값.</summary>
        public void Start(float centerAngleDegrees, float arcDegrees, float durationSeconds)
        {
            center = centerAngleDegrees;
            arc = arcDegrees > 0f ? arcDegrees : DefaultArcDegrees;
            duration = durationSeconds > 0f ? durationSeconds : DefaultDuration;
            sign = FacesLeft ? -1 : 1;
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
