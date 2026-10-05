using Samkuk.Data;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Core
{
    /// <summary>
    /// 타일링(Tiled) 스프라이트를 카메라 위치에 맞춰 타일 크기 단위로 스냅시켜
    /// 무한히 이어지는 배경처럼 보이게 한다.
    /// 스프라이트 렌더러는 DrawMode=Tiled, 크기는 화면을 충분히 덮을 만큼 커야 한다.
    /// 내정에서 출진한 판(<see cref="GameSession.SortieCastle"/>)이면 그 성의 지형으로 맵을 바꾼다:
    /// 지형 바닥 타일 + 성마다 다르게 흩뿌려지는 소품(<see cref="TerrainPropSpawner"/>).
    /// 성 없이 시작한 판도 평야 + 강 맵을 쓴다(아무것도 없는 빈 평지가 되지 않게).
    /// 지형 그림이 없으면(셋업 전) 기존 배경 위에 지형 색을 한 겹 덮는 방식으로 대신한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class InfiniteBackground : MonoBehaviour
    {
        const string OverlayName = "TerrainOverlay";

        [SerializeField] Transform followTarget;

        SpriteRenderer sr;
        Vector2 tileSize;
        Sprite defaultSprite;
        Color defaultColor;
        GameObject propsRoot;
        TerrainPropSpawner spawner;

        public Transform FollowTarget
        {
            get => followTarget;
            set
            {
                followTarget = value;
                if (spawner != null) spawner.SetFollow(value);
            }
        }

        /// <summary>지형 소품을 만드는 쪽 (지형 맵이 적용된 경우에만 있다).</summary>
        public TerrainPropSpawner Props => spawner;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            defaultSprite = sr.sprite;
            defaultColor = sr.color;
            RefreshTileSize();
            ApplyCastle(GameSession.SortieCastle);
        }

        void Start()
        {
            if (followTarget == null && Camera.main != null)
                followTarget = Camera.main.transform;
            if (spawner != null) spawner.SetFollow(followTarget);
        }

        void OnDestroy()
        {
            if (propsRoot != null) Destroy(propsRoot);
        }

        /// <summary>
        /// 성의 지형에 맞는 맵을 적용한다. null 이면 성 없이 시작한 판의 맵(평야 + 강, <see cref="TerrainMap.CreateFreeBattle"/>).
        /// 지형 그림이 없으면(셋업 전) 기본 배경 그대로(성이 있을 땐 색 덮개)다.
        /// </summary>
        public void ApplyCastle(CastleData castle)
        {
            ResetToDefault();

            var catalog = TerrainThemeCatalog.Load();
            var map = castle != null ? TerrainMap.Create(castle, catalog) : TerrainMap.CreateFreeBattle(catalog);
            if (map == null)
            {
                if (castle != null) ApplyTerrainOverlay(BattleTerrain.Overlay(castle.terrain)); // 지형 그림이 없을 때의 대체
                return;
            }

            if (map.Theme.groundTile != null)
            {
                sr.sprite = map.Theme.groundTile;
                sr.color = map.GroundTint;
                RefreshTileSize();
            }
            else ApplyTerrainOverlay(BattleTerrain.Overlay(map.Castle.terrain));

            // 소품은 배경과 따로 둔다 (배경은 카메라를 따라 움직이므로 자식으로 두면 소품도 함께 끌려온다)
            propsRoot = new GameObject("TerrainProps");
            spawner = propsRoot.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, followTarget, sr);
        }

        /// <summary>지형 적용을 모두 지우고 기본 배경(Step 1 의 단색 타일)으로 되돌린다.</summary>
        public void ResetToDefault()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (defaultSprite == null) { defaultSprite = sr.sprite; defaultColor = sr.color; }

            ApplyTerrainOverlay(null);
            if (propsRoot != null) { Destroy(propsRoot); propsRoot = null; spawner = null; }
            sr.sprite = defaultSprite;
            sr.color = defaultColor;
            RefreshTileSize();
        }

        static Sprite whiteSprite;

        /// <summary>지형 색을 배경 위에 한 겹 덮는다 (null 이면 기존 덮개를 없앤다). 배경과 같은 정렬 레이어에서 한 칸 위.</summary>
        public void ApplyTerrainOverlay(Color? overlay)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            var existing = transform.Find(OverlayName);
            if (overlay == null)
            {
                if (existing != null) Destroy(existing.gameObject);
                return;
            }

            SpriteRenderer layer;
            if (existing != null) layer = existing.GetComponent<SpriteRenderer>();
            else
            {
                var go = new GameObject(OverlayName, typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                layer = go.GetComponent<SpriteRenderer>();
            }
            if (whiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f); // 1유닛 크기
                whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            layer.sprite = whiteSprite;
            layer.color = overlay.Value;
            layer.sortingLayerID = sr.sortingLayerID;
            layer.sortingOrder = sr.sortingOrder + 1;
            layer.transform.localScale = new Vector3(Mathf.Max(1f, sr.size.x), Mathf.Max(1f, sr.size.y), 1f);
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
