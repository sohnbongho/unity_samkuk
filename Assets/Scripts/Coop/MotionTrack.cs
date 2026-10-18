using UnityEngine;

namespace Samkuk.Coop
{
    /// <summary>
    /// 스냅샷(10 Hz)으로 띄엄띄엄 오는 위치를 매 프레임 부드럽게 잇는다: 새 위치가 오면 지금 보이는 자리에서 그 위치까지
    /// 스냅샷 간격 동안 직선으로 움직인다(한 스냅샷 뒤에서 따라가는 보간). 순수 계산이라 테스트한다.
    /// </summary>
    public sealed class MotionTrack
    {
        Vector2 from, to;
        float t, interval;
        bool started;

        /// <summary>지금 그릴 위치.</summary>
        public Vector2 Current { get; private set; }
        /// <summary>마지막으로 받은 서버 위치.</summary>
        public Vector2 Target => to;
        /// <summary>보간 구간의 속도(유닛/초). 바라보는 방향/걷기 판단에 쓴다.</summary>
        public Vector2 Velocity => interval > 0f ? (to - from) / interval : Vector2.zero;
        public bool IsMoving => (to - from).sqrMagnitude > 1e-4f && t < 1f;

        /// <summary>처음 자리에 바로 놓는다 (보간 없음).</summary>
        public void Snap(Vector2 position)
        {
            from = to = Current = position;
            t = 1f;
            started = true;
        }

        /// <summary>새 서버 위치. intervalSeconds 동안 지금 자리에서 그리로 간다. 너무 멀면(순간이동/재배치) 바로 놓는다.</summary>
        public void Push(Vector2 position, float intervalSeconds, float snapDistance = 6f)
        {
            if (!started || (position - Current).sqrMagnitude > snapDistance * snapDistance) { Snap(position); return; }
            from = Current;
            to = position;
            interval = Mathf.Max(0.01f, intervalSeconds);
            t = 0f;
        }

        public void Advance(float dt)
        {
            if (t >= 1f) { Current = to; return; }
            t = Mathf.Min(1f, t + dt / interval);
            Current = Vector2.Lerp(from, to, t);
        }
    }
}
