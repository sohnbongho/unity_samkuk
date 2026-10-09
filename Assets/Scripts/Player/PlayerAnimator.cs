using Samkuk.Data;
using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>
    /// 장수의 걷기 스프라이트 시트로 4방향 걷기 애니메이션을 재생한다.
    /// 이동 입력의 방향으로 바라보는 쪽을 정하고, 움직이는 동안 프레임을 돌린다(멈추면 정지 자세).
    /// 시트가 없는 장수는 아무것도 하지 않아 기존 스프라이트와 좌우 반전(<see cref="PlayerController"/>)이 그대로 쓰인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour, ILookOverride
    {
        /// <summary>대각선 이동에서 방향이 계속 바뀌지 않도록, 다른 축이 이 배수보다 커야 방향을 바꾼다.</summary>
        const float AxisSwitchRatio = 1.2f;

        [SerializeField, Tooltip("걷는 동안 초당 재생하는 프레임 수 (이동 입력이 약하면 느려짐)")]
        float framesPerSecond = 9f;

        SpriteRenderer body;
        Sprite fallbackSprite;
        PlayerController controller;
        HeroSpriteSet set;
        float clock;
        Vector2 lookDir;
        float lookLeft;

        /// <summary>걷기 시트를 쓰는 중인가 (false 면 기존 스프라이트 그대로).</summary>
        public bool HasSheet => set != null;
        public FacingDir Direction { get; private set; } = FacingDir.Down;
        /// <summary>현재 보이는 프레임 번호 (0~3).</summary>
        public int Frame { get; private set; }
        public float FramesPerSecond { get => framesPerSecond; set => framesPerSecond = value; }

        void Awake()
        {
            body = GetComponentInChildren<SpriteRenderer>();
            if (body != null) fallbackSprite = body.sprite;
            controller = GetComponent<PlayerController>();
        }

        /// <summary>장수가 정해지면 호출한다. 걷기 시트가 없으면 애니메이션을 끈다.</summary>
        public void SetHero(HeroData hero)
        {
            set = hero != null && hero.walkSheet != null
                ? HeroSpriteSet.Get(hero.walkSheet, hero.walkPixelsPerUnit)
                : null;
            clock = 0f;
            Frame = 0;
            Refresh();
        }

        /// <summary>무기가 휘두르는 동안 그쪽을 본다 (<see cref="ILookOverride"/>). 이동 입력보다 우선하고, 걷기 프레임은 계속 돈다.</summary>
        public void Look(Vector2 direction, float seconds)
        {
            if (direction.sqrMagnitude < 1e-6f || seconds <= 0f) return;
            lookDir = direction;
            lookLeft = seconds;
        }

        /// <summary>떨림 방지 없이 벡터가 가장 가까운 4방향. 가로/세로가 같으면 가로.</summary>
        public static FacingDir DirectionOf(Vector2 v)
        {
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? FacingDir.Right : FacingDir.Left;
            return v.y >= 0f ? FacingDir.Up : FacingDir.Down;
        }

        /// <summary>
        /// 이동 방향으로 바라보는 쪽을 고른다. 가로/세로 중 더 큰 축을 따르되,
        /// 지금 방향의 축에서 1.2배 넘게 다른 축이 커야 바꾼다 (대각선 떨림 방지).
        /// </summary>
        public static FacingDir PickDirection(Vector2 move, FacingDir current)
        {
            float ax = Mathf.Abs(move.x), ay = Mathf.Abs(move.y);
            if (ax < 0.0001f && ay < 0.0001f) return current;

            bool currentHorizontal = current == FacingDir.Left || current == FacingDir.Right;
            bool horizontal = currentHorizontal ? !(ay > ax * AxisSwitchRatio) : ax > ay * AxisSwitchRatio;

            if (horizontal) return move.x >= 0f ? FacingDir.Right : FacingDir.Left;
            return move.y >= 0f ? FacingDir.Up : FacingDir.Down;
        }

        void LateUpdate()
        {
            if (set == null || body == null) return;

            Vector2 move = controller != null ? controller.MoveInput : Vector2.zero;
            bool moving = move.sqrMagnitude > 0.0001f;

            bool looking = lookLeft > 0f;
            if (looking)
            {
                lookLeft -= Time.deltaTime;
                Direction = DirectionOf(lookDir);
            }

            if (moving)
            {
                if (!looking) Direction = PickDirection(move, Direction);
                clock += Time.deltaTime * framesPerSecond * Mathf.Max(0.6f, move.magnitude);
                // 멈춘 자세(0)에서 출발하므로 걷기는 1번 프레임부터
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
            if (set == null)
            {
                // 시트가 없는 장수로 바뀌면 처음의 기본 스프라이트로 되돌린다
                if (fallbackSprite != null && body.sprite != fallbackSprite) body.sprite = fallbackSprite;
                return;
            }

            body.sprite = set.Get(Direction, Frame);
            body.flipX = false;
        }
    }
}
