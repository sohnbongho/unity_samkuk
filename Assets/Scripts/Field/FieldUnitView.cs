using Samkuk.Core;
using Samkuk.World;
using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 부대 하나의 그림: 모델의 보간 위치(<see cref="FieldUnit.VisualCol"/>)를 월드로 옮겨 서고, 이동 방향에 따라 앞/뒷모습과 좌우 반전을 고르며,
    /// 움직일 때만 걷기 프레임을 번갈아 보여 준다. World 정렬 레이어(발 y 로 앞뒤)를 쓰고, 발밑 마름모 테로 선택/목표를 표시한다.
    /// 무너지거나 빠져나간 부대는 숨긴다.
    /// </summary>
    public class FieldUnitView : MonoBehaviour
    {
        /// <summary>걷기 프레임이 바뀌는 간격 (초).</summary>
        public const float FrameInterval = 0.2f;
        /// <summary>병사 수 라벨을 띄울 높이 (유닛): 그림 위쪽 여유.</summary>
        public const float LabelHeight = 1.25f;

        public FieldUnit Unit { get; private set; }
        public bool Selected { get; set; }
        public bool Targeted { get; set; }

        SpriteRenderer sr;
        SpriteRenderer ring;
        FieldSpriteSet set;
        Sprite placeholder;
        IsoFacing facing;
        float animClock;
        int frame;

        public SpriteRenderer Renderer => sr;
        public IsoFacing Facing => facing;
        public int Frame => frame;

        public void Init(FieldUnit unit, FieldSpriteSet spriteSet, Sprite fallback, Sprite ringSprite)
        {
            Unit = unit;
            set = spriteSet;
            placeholder = fallback;
            facing = IsoGrid.DefaultFacing(unit.Side);

            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            WorldSorting.Configure(sr);

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = ringSprite;
            ring.sortingLayerName = GameLayers.Sorting.Background;
            ring.sortingOrder = 5;   // 타일(0) 위, 부대(World 레이어) 아래
            ring.enabled = false;

            Sync(0f);
        }

        public void Sync(float dt)
        {
            var u = Unit;
            if (!u.Alive)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            IsoGrid.CellToWorld(u.VisualCol, u.VisualRow, out float x, out float y);
            transform.localPosition = new Vector3(x, y, 0f);

            facing = IsoGrid.FacingFor(u.FacingDx, u.FacingDy, facing);
            if (u.Moving)
            {
                animClock += dt;
                if (animClock >= FrameInterval) { animClock -= FrameInterval; frame = (frame + 1) % FieldArtSpec.WalkFrames; }
            }
            else { frame = 0; animClock = 0f; }

            sr.sprite = set != null ? set.Get(IsoGrid.UsesBackSprite(facing), frame) : placeholder;
            sr.flipX = IsoGrid.FlipX(facing);

            bool showRing = Selected || Targeted;
            ring.enabled = showRing;
            if (showRing) ring.color = Targeted ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.9f, 0.3f);
        }

        /// <summary>병사 수 라벨을 띄울 월드 위치.</summary>
        public Vector3 LabelWorldPosition => transform.position + Vector3.up * LabelHeight;
    }
}
