using Samkuk.Data;
using Samkuk.Feedback;
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
        TerrainCollision collision;
        WaterRippleFx ripples;
        BattleLighting lighting;

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

        /// <summary>지형이 이동에 주는 효과(막는 소품, 느려지는 물). 맵이 적용된 동안 <see cref="TerrainCollision.Active"/> 로도 찾을 수 있다.</summary>
        public TerrainCollision Collision => collision;

        /// <summary>물에 들어간 것의 발밑 물결 (지형 맵이 적용된 경우에만 있다).</summary>
        public WaterRippleFx Ripples => ripples;

        /// <summary>전역광 색조와 플레이어 빛 (지형 맵이 적용된 경우에만 있다).</summary>
        public BattleLighting Lighting => lighting;

        /// <summary>HD-2D 조명을 붙일 수 있는 배경인가. 맵 편집기는 끈다(편집 화면은 밝고 일정하게, 조명은 전투 테스트로 확인). 맵을 적용하기 전에 정한다.</summary>
        public bool LightingAllowed { get; set; } = true;

        /// <summary>HD-2D 조명을 켜거나 끈다 (디버그 키 F5): 전역광 색조, 플레이어 빛, 소품 점광원을 한꺼번에.</summary>
        public void SetLightingEnabled(bool enabled)
        {
            if (lighting != null) lighting.Apply(enabled);
            if (spawner != null) spawner.SetLightsEnabled(enabled);
        }

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
            ReleaseCollision();
        }

        /// <summary>
        /// 성의 지형에 맞는 맵을 적용한다. null 이면 성 없이 시작한 판의 맵(평야 + 강, <see cref="TerrainMap.CreateFreeBattle"/>).
        /// 지형 그림이 없으면(셋업 전) 기본 배경 그대로(성이 있을 땐 색 덮개)다.
        /// </summary>
        public void ApplyCastle(CastleData castle)
        {
            ResetToDefault();

            // 맵 편집기로 고쳐 둔 맵이 있으면 그 내용이 자동 생성 맵 위에 덮인다
            var catalog = TerrainThemeCatalog.Load();
            var map = castle != null ? TerrainMap.CreateForBattle(castle, catalog) : TerrainMap.CreateFreeBattle(catalog);
            if (map == null)
            {
                if (castle != null) ApplyTerrainOverlay(BattleTerrain.Overlay(castle.terrain)); // 지형 그림이 없을 때의 대체
                return;
            }
            BuildMap(map);
        }

        /// <summary>이미 만든 맵을 그대로 적용한다 (맵 편집기가 편집 중인 맵을 보여 줄 때).</summary>
        public void ApplyMap(TerrainMap map)
        {
            ResetToDefault();
            if (map != null) BuildMap(map);
        }

        void BuildMap(TerrainMap map)
        {
            if (map.Theme.groundTile != null)
            {
                sr.sprite = map.Theme.groundTile;
                sr.color = map.GroundTint;
                RefreshTileSize();
            }
            else ApplyTerrainOverlay(BattleTerrain.Overlay(map.Terrain));

            // 소품은 배경과 따로 둔다 (배경은 카메라를 따라 움직이므로 자식으로 두면 소품도 함께 끌려온다)
            propsRoot = new GameObject("TerrainProps");
            spawner = propsRoot.AddComponent<TerrainPropSpawner>();
            spawner.Initialize(map, followTarget, sr, LightingAllowed);

            // 보이는 것과 같은 배치에서 막는 소품/느려지는 물을 뽑아 플레이어·적·아군의 이동이 참조하게 한다
            collision = new TerrainCollision(map);
            TerrainCollision.Active = collision;

            // 물에 들어가면 발밑에 물결: 느려지는 자리가 눈에 보이게 (소품 루트와 함께 사라진다)
            ripples = propsRoot.AddComponent<WaterRippleFx>();
            ripples.Initialize(collision, sr.sharedMaterial);

            // HD-2D 조명: 성 그림과 같은 시간대의 색조를 전역광에 입힌다 (소품 루트와 함께 사라지며 전역광을 되돌린다)
            if (LightingAllowed)
            {
                lighting = propsRoot.AddComponent<BattleLighting>();
                lighting.Initialize(map.Terrain, CastleMood.Of(map.Castle));
            }
        }

        /// <summary>이 배경이 만든 지형 충돌을 내린다 (다른 것이 올려 둔 것은 건드리지 않는다).</summary>
        void ReleaseCollision()
        {
            if (collision != null && TerrainCollision.Active == collision) TerrainCollision.Active = null;
            collision = null;
        }

        /// <summary>지형 적용을 모두 지우고 기본 배경(Step 1 의 단색 타일)으로 되돌린다.</summary>
        public void ResetToDefault()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (defaultSprite == null) { defaultSprite = sr.sprite; defaultColor = sr.color; }

            ApplyTerrainOverlay(null);
            if (propsRoot != null) { Destroy(propsRoot); propsRoot = null; spawner = null; ripples = null; lighting = null; }
            ReleaseCollision();
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
