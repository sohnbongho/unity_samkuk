using UnityEngine;
using UnityEngine.InputSystem;

namespace Samkuk.Player
{
    /// <summary>
    /// 플레이어 이동. WASD / 방향키 / 게임패드 왼쪽 스틱.
    /// 마지막으로 향한 방향(FacingDirection)은 이후 무기 시스템에서 사용한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 4f;
        [SerializeField] SpriteRenderer body;

        Rigidbody2D rb;
        PlayerStats stats;
        InputAction moveAction;
        Vector2 moveInput;

        /// <summary>현재 이동 입력 (길이 0~1).</summary>
        public Vector2 MoveInput => moveInput;
        /// <summary>마지막으로 입력이 있었던 방향 (정규화). 기본값은 오른쪽.</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();
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
                if (body != null && Mathf.Abs(moveInput.x) > 0.01f)
                    body.flipX = moveInput.x < 0f;
            }
        }

        void FixedUpdate()
        {
            float speed = moveSpeed * (stats != null ? stats.MoveSpeedMultiplier : 1f);
            rb.linearVelocity = moveInput * speed;
        }
    }
}
