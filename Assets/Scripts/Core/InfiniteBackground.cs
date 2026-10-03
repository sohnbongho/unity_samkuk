using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>
    /// 타일링(Tiled) 스프라이트를 카메라 위치에 맞춰 타일 크기 단위로 스냅시켜
    /// 무한히 이어지는 배경처럼 보이게 한다.
    /// 스프라이트 렌더러는 DrawMode=Tiled, 크기는 화면을 충분히 덮을 만큼 커야 한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class InfiniteBackground : MonoBehaviour
    {
        [SerializeField] Transform followTarget;

        SpriteRenderer sr;
        Vector2 tileSize;

        public Transform FollowTarget { get => followTarget; set => followTarget = value; }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            RefreshTileSize();
        }

        void Start()
        {
            if (followTarget == null && Camera.main != null)
                followTarget = Camera.main.transform;
        }

        void RefreshTileSize()
        {
            if (sr.sprite != null)
                tileSize = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;
        }

        void LateUpdate()
        {
            if (followTarget == null || tileSize.x <= 0f || tileSize.y <= 0f) return;

            Vector3 p = followTarget.position;
            transform.position = new Vector3(
                Mathf.Round(p.x / tileSize.x) * tileSize.x,
                Mathf.Round(p.y / tileSize.y) * tileSize.y,
                transform.position.z);
        }
    }
}
