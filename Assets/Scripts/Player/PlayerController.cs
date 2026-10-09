using Samkuk.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Player
{
    /// <summary>
    /// 플레이어 이동. WASD / 방향키 / 게임패드 왼쪽 스틱.
    /// 마지막으로 향한 방향(FacingDirection)은 이후 무기 시스템에서 사용한다.
    /// 전투 맵에 지형 충돌(<see cref="TerrainCollision.Active"/>)이 있으면 나무/바위에 막혀 미끄러지고 강물/연못에서 느려진다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        /// <summary>지형 충돌용 몸 반지름 (적의 접촉 판정 playerRadius 0.4 보다 조금 작게: 나무 사이를 지나기 쉽게).</summary>
        public const float BodyRadius = 0.32f;

        [SerializeField] float moveSpeed = 4f;
        [SerializeField] SpriteRenderer body;

        Rigidbody2D rb;
        PlayerStats stats;
        PlayerAnimator animator;
        InputAction moveAction;
        Vector2 moveInput;

        /// <summary>현재 이동 입력 (길이 0~1).</summary>
        public Vector2 MoveInput => moveInput;
        /// <summary>마지막으로 입력이 있었던 방향 (정규화). 기본값은 오른쪽.</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
        /// <summary>지금 서 있는 지형의 속도 배율 (1 = 평소, 물 위 0.5 등). HUD/연출이 "물에 들어갔다"를 알 때 쓴다.</summary>
        public float TerrainSpeedFactor { get; private set; } = 1f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();
            animator = GetComponent<PlayerAnimator>();
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            BuildInput();
        }

        void BuildInput()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
        }

        void OnEnable() => moveAction.Enable();

        void OnDisable()
        {
            moveAction.Disable();
            moveInput = Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        void OnDestroy() => moveAction?.Dispose();

        void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

            if (moveInput.sqrMagnitude > 0.0001f)
            {
                FacingDirection = moveInput.normalized;
                // 걷기 시트가 있으면 방향별 그림이 따로 있으므로 좌우 반전은 하지 않는다 (PlayerAnimator)
                bool animated = animator != null && animator.HasSheet;
                if (body != null && !animated && Mathf.Abs(moveInput.x) > 0.01f)
                    body.flipX = moveInput.x < 0f;
            }
        }

        void FixedUpdate()
        {
            float speed = moveSpeed * (stats != null ? stats.MoveSpeedMultiplier : 1f);
            Vector2 velocity = moveInput * speed;

            var terrain = TerrainCollision.Active;
            if (terrain != null)
            {
                Vector2 p = rb.position;
                TerrainSpeedFactor = terrain.SpeedFactor(p);
                // 막는 소품 쪽 성분만 지워 미끄러진다. 플레이어는 정면으로 막히면 그냥 멈춘다 (스스로 돌아가게 하면 조작감이 흐트러진다)
                velocity = terrain.Resolve(p, BodyRadius, velocity * TerrainSpeedFactor, Time.fixedDeltaTime);
            }
            else TerrainSpeedFactor = 1f;

            rb.linearVelocity = velocity;
        }
    }
}
