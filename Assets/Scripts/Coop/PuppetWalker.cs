using Samkuk.Data;
using Samkuk.Player;
using UnityEngine;

namespace Samkuk.Coop
{
    /// <summary>
    /// 꼭두각시(서버가 움직이는) 캐릭터의 걷기 그림. PlayerAnimator 는 이동 입력을 읽지만 협동에서는 서버 위치/방향이 원본이므로
    /// 방향과 "걷는 중" 을 밖에서 받아 걷기 시트 프레임만 돌린다. 시트가 없는 장수는 기본 스프라이트 + 장수 색 + 좌우 반전.
    /// </summary>
    public sealed class PuppetWalker
    {
        readonly SpriteRenderer body;
        readonly Sprite fallback;
        HeroSpriteSet set;
        Color baseColor = Color.white;
        float clock, lookLeft;
        Vector2 lookDir;
        float fps = 9f;

        public FacingDir Direction { get; private set; } = FacingDir.Down;
        public int Frame { get; private set; }
        public bool HasSheet => set != null;
        public SpriteRenderer Body => body;

        public PuppetWalker(SpriteRenderer body)
        {
            this.body = body;
            fallback = body != null ? body.sprite : null;
        }

        public void SetHero(HeroData hero)
        {
            set = hero != null && hero.walkSheet != null ? HeroSpriteSet.Get(hero.walkSheet, hero.walkPixelsPerUnit) : null;
            baseColor = set != null || hero == null ? Color.white : hero.tint;
            clock = 0f;
            Frame = 0;
            if (body != null)
            {
                body.color = baseColor;
                if (set == null && fallback != null) body.sprite = fallback;
            }
            Refresh();
        }

        /// <summary>쓰러짐(반투명)/레벨업 선택 중(금빛)/무적(살짝 흐림) 표시. 평소는 장수 색 그대로.</summary>
        public void SetLook(bool dead, bool choosing, bool invulnerable)
        {
            if (body == null) return;
            var c = baseColor;
            if (choosing) c = Color.Lerp(c, new Color(1f, 0.85f, 0.3f), 0.55f);
            c.a = dead ? 0.35f : invulnerable ? 0.7f : 1f;
            body.color = c;
        }

        /// <summary>무기를 휘두르는 동안 그쪽을 본다 (PlayerAnimator.Look 과 같은 규칙).</summary>
        public void Look(Vector2 direction, float seconds)
        {
            if (direction.sqrMagnitude < 1e-6f || seconds <= 0f) return;
            lookDir = direction;
            lookLeft = seconds;
        }

        /// <summary>facing 은 서버 값(0 아래 1 위 2 왼쪽 3 오른쪽 = FacingDir 순서). moving 이면 프레임을 돌린다.</summary>
        public void Tick(FacingDir facing, bool moving, float dt)
        {
            bool looking = lookLeft > 0f;
            if (looking)
            {
                lookLeft -= dt;
                Direction = PlayerAnimator.DirectionOf(lookDir);
            }
            else Direction = facing;

            if (moving)
            {
                clock += dt * fps;
                Frame = ((int)clock + 1) % HeroSpriteSet.Columns;
            }
            else
            {
                clock = 0f;
                Frame = 0;
            }
            Refresh();
        }

        void Refresh()
        {
            if (body == null) return;
            if (set != null)
            {
                body.sprite = set.Get(Direction, Frame);
                body.flipX = false;
            }
            else if (Direction == FacingDir.Left || Direction == FacingDir.Right)
            {
                body.flipX = Direction == FacingDir.Left;
            }
        }

        /// <summary>서버 facing 정수를 FacingDir 로 (범위 밖이면 아래).</summary>
        public static FacingDir FacingOf(int serverFacing) =>
            serverFacing >= 0 && serverFacing <= 3 ? (FacingDir)serverFacing : FacingDir.Down;
    }
}
