using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>대상(플레이어)을 부드럽게 따라가는 2D 카메라.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField, Tooltip("0이면 즉시 추적, 클수록 빠르게 따라붙음")] float smoothSpeed = 10f;
        [SerializeField] float zOffset = -10f;

        public Transform Target { get => target; set => target = value; }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 goal = new Vector3(target.position.x, target.position.y, zOffset);
            transform.position = smoothSpeed <= 0f
                ? goal
                : Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
        }

        /// <summary>대상 위치로 카메라를 즉시 이동 (씬 시작/텔레포트용).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = new Vector3(target.position.x, target.position.y, zOffset);
        }
    }
}
