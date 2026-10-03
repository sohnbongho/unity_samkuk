using Samkuk.Feedback;
using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>
    /// 대상(플레이어)을 부드럽게 따라가는 2D 카메라.
    /// 추적 위치(basePos)와 화면 흔들림(ScreenShake.Offset)을 분리해서 관리한다 —
    /// 흔들림이 추적 보간에 섞여 들어가 위치가 누적되어 어긋나는 일이 없다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField, Tooltip("0이면 즉시 추적, 클수록 빠르게 따라붙음")] float smoothSpeed = 10f;
        [SerializeField] float zOffset = -10f;

        ScreenShake shake;
        Vector3 basePos;
        Vector3 appliedPos;
        bool initialized;

        public Transform Target { get => target; set => target = value; }

        void Awake() => shake = GetComponent<ScreenShake>();

        void LateUpdate()
        {
            if (target == null) return;

            // 처음이거나 다른 코드가 카메라를 직접 옮겼다면 현재 위치를 새 기준으로 삼는다
            if (!initialized || (transform.position - appliedPos).sqrMagnitude > 1e-8f)
            {
                basePos = transform.position;
                initialized = true;
            }

            Vector3 goal = new Vector3(target.position.x, target.position.y, zOffset);
            basePos = smoothSpeed <= 0f
                ? goal
                : Vector3.Lerp(basePos, goal, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));

            Apply();
        }

        void Apply()
        {
            Vector2 offset = shake != null ? shake.Offset : Vector2.zero;
            appliedPos = basePos + (Vector3)offset;
            transform.position = appliedPos;
        }

        /// <summary>대상 위치로 카메라를 즉시 이동 (씬 시작/텔레포트용).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            basePos = new Vector3(target.position.x, target.position.y, zOffset);
            initialized = true;
            Apply();
        }
    }
}
