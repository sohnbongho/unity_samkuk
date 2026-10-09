using Samkuk.Core;
using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 드리운 그림자(Step 14-4): 어떤 SpriteRenderer(캐릭터 몸, 서 있는 소품)의 그림을 검고 반투명하게 한 번 더 그리되
    /// 발밑(스프라이트 피벗)에서 비스듬히 눕힌다. 원본의 자식으로 3단 트랜스폼(회전 → 배율 → 회전, <see cref="ShadowPreset.Decompose"/>)을 두어
    /// 전단을 만들고, 매 프레임 원본의 스프라이트/반전을 따라간다(바뀔 때만 대입). 그림자는 배경 레이어에 그려져 캐릭터·소품 아래에 깔린다.
    /// 시간대별 모양은 <see cref="Settings"/>(전역, <see cref="BattleLighting"/> 이 정함)이고 <see cref="Hd2dSettings.Shadows"/> 로 끈다.
    /// 적이 너무 많을 때는 <see cref="Attach"/> 의 blob 으로 발밑 타원(발밑 그림자)을 쓸 수 있다.
    /// </summary>
    public class CastShadow : MonoBehaviour
    {
        public const string NodeName = "Shadow";

        static ShadowSettings settings = ShadowPreset.For(TimeOfDay.Day);
        static int version;
        static Sprite blobSprite;

        SpriteRenderer source;
        SpriteRenderer shadow;
        Transform scaleNode, spriteNode;
        bool blob;
        int appliedVersion = -1;

        /// <summary>지금 모든 그림자가 쓰는 모양.</summary>
        public static ShadowSettings Settings => settings;

        /// <summary>모양을 바꾼다 (시간대). 살아 있는 그림자는 다음 프레임에 따라온다.</summary>
        public static void SetSettings(ShadowSettings s)
        {
            settings = s;
            version++;
        }

        public SpriteRenderer Renderer => shadow;
        public SpriteRenderer Source => source;
        public bool IsBlob => blob;
        public Transform ScaleNode => scaleNode;
        public Transform SpriteNode => spriteNode;

        /// <summary>
        /// <paramref name="source"/> 에 그림자를 붙인다 (이미 있으면 그것을 돌려준다). <paramref name="blob"/> 이면 전단 없이 발밑 타원.
        /// 원본의 스프라이트 피벗이 발이어야 그림자가 발에서 시작한다(걷기 시트, 서 있는 소품은 그렇다).
        /// </summary>
        public static CastShadow Attach(SpriteRenderer source, bool blob = false)
        {
            if (source == null) return null;
            var existing = source.transform.Find(NodeName);
            if (existing != null)
            {
                var old = existing.GetComponent<CastShadow>();
                if (old != null && old.blob == blob) return old;
                Destroy(existing.gameObject);
            }

            var root = new GameObject(NodeName);
            root.transform.SetParent(source.transform, false);
            var mid = new GameObject("Scale");
            mid.transform.SetParent(root.transform, false);
            var leaf = new GameObject("Sprite");
            leaf.transform.SetParent(mid.transform, false);
            var sr = leaf.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = GameLayers.Sorting.Background;
            sr.sortingOrder = ShadowPreset.SortingOrder;
            sr.spriteSortPoint = SpriteSortPoint.Center;
            if (source.sharedMaterial != null) sr.sharedMaterial = source.sharedMaterial;

            var cs = root.AddComponent<CastShadow>();
            cs.source = source;
            cs.shadow = sr;
            cs.scaleNode = mid.transform;
            cs.spriteNode = leaf.transform;
            cs.blob = blob;
            cs.Sync();
            return cs;
        }

        void LateUpdate() => Sync();

        /// <summary>원본을 따라 보이기/스프라이트/반전/모양을 맞춘다.</summary>
        public void Sync()
        {
            if (source == null) return;
            bool on = Hd2dSettings.Shadows && source.enabled && source.sprite != null;
            if (shadow.enabled != on) shadow.enabled = on;
            if (!on) return;

            if (appliedVersion != version) ApplyLayout();
            if (blob) return;
            if (shadow.sprite != source.sprite) shadow.sprite = source.sprite;
            if (shadow.flipX != source.flipX) shadow.flipX = source.flipX;
        }

        void ApplyLayout()
        {
            appliedVersion = version;
            var s = settings;
            shadow.color = new Color(0f, 0f, 0f, s.alpha);
            if (blob)
            {
                // 발밑 타원: 전단 없이 납작하게. 길이가 길수록 조금 더 넓게
                shadow.sprite = BlobSprite;
                transform.localRotation = Quaternion.identity;
                scaleNode.localScale = new Vector3(0.9f + s.length * 0.3f, 0.45f, 1f);
                spriteNode.localRotation = Quaternion.identity;
                return;
            }
            ShadowPreset.Matrix(s, out float a, out float b, out float c, out float d);
            ShadowPreset.Decompose(a, b, c, d, out float outer, out float sx, out float sy, out float inner);
            transform.localRotation = Quaternion.Euler(0f, 0f, outer);
            scaleNode.localScale = new Vector3(sx, sy, 1f);
            spriteNode.localRotation = Quaternion.Euler(0f, 0f, inner);
        }

        /// <summary>발밑 타원 그림 (코드로 만든 16x8 흰 타원, 1유닛 폭, 피벗 가운데).</summary>
        public static Sprite BlobSprite
        {
            get
            {
                if (blobSprite != null) return blobSprite;
                const int w = 16, h = 8;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                        bool inside = dx * dx + dy * dy <= 1f;
                        px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                blobSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);   // 1유닛 폭
                blobSprite.hideFlags = HideFlags.HideAndDontSave;
                blobSprite.name = "ShadowBlob";
                return blobSprite;
            }
        }
    }
}
