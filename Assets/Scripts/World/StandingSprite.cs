using UnityEngine;
using UnityEngine.Rendering;

namespace Samkuk.World
{
    /// <summary>
    /// 서 있는 그림(Step 16-1): 2.5D 틸트가 켜지면 이 렌더러의 재질을 세우는 재질(<see cref="TiltView.StandingMaterial"/>)로 바꾸고 그림자를 드리우게 하며,
    /// 꺼지면 원래 재질로 되돌린다. 트랜스폼은 건드리지 않는다 — 세우는 것은 셰이더가 한다(Rigidbody2D·정렬·반투명 규칙이 그대로 동작).
    /// 캐릭터 몸, 서 있는 소품, 손에 든 무기, 체력 막대에 붙는다(<see cref="WorldSorting.Configure"/> 가 캐릭터·소품에 붙인다).
    /// <see cref="liftHeight"/> 가 있으면 자기 자리를 발에서 그만큼 "위"(<see cref="TiltView.Lift"/>)로 둔다(체력 막대 뿌리처럼 렌더러 없는 노드에도 쓴다).
    /// </summary>
    [DisallowMultipleComponent]
    public class StandingSprite : MonoBehaviour
    {
        [SerializeField, Tooltip("피벗(발)에서 그림 아래 끝까지 (유닛, 배율 1). 걷기 시트 8px = 0.25")] float footLift;
        [SerializeField, Tooltip("카메라 쪽으로 미는 거리. 몸과 같은 평면에 겹치는 무기/막대가 번쩍이지 않게")] float toward;
        [SerializeField, Tooltip("알파 블렌드(깊이 안 씀, 그림자 없음)")] bool translucent;
        [SerializeField, Tooltip("0 보다 크면 자기 자리를 발에서 이만큼 위로 (체력 막대 뿌리)")] float liftHeight;
        [SerializeField, Tooltip("세로 배율 보정 (서 있는 소품은 TiltPreset.PropStretch, 캐릭터는 1)")] float stretchScale = 1f;

        static readonly int SeedId = Shader.PropertyToID("_StandSeed");
        static MaterialPropertyBlock block;
        static int seedCounter;   // 렌더러마다 다른 값 (GetInstanceID 는 Unity 6 에서 폐기)

        SpriteRenderer sr;
        Material original;
        bool swapped;
        float seed;   // 이 렌더러만의 값 (한 번 정하면 풀에서 돌려 써도 그대로)

        public float FootLift => footLift;
        public float Toward => toward;
        public bool Translucent => translucent;
        public float LiftHeight => liftHeight;
        /// <summary>지금 틸트 재질로 바꿔 둔 상태인가.</summary>
        public bool IsStanding => swapped;

        /// <summary>붙이거나(이미 있으면 값만 고치고) 지금 상태에 맞춘다.</summary>
        public static StandingSprite Attach(GameObject go, float footLift = 0f, float toward = 0f, bool translucent = false, float liftHeight = 0f, float stretchScale = 1f)
        {
            if (go == null) return null;
            var s = go.GetComponent<StandingSprite>();
            if (s == null) s = go.AddComponent<StandingSprite>();
            s.footLift = footLift;
            s.toward = toward;
            s.translucent = translucent;
            s.liftHeight = liftHeight;
            s.stretchScale = stretchScale;
            if (!s.enabled) s.enabled = true;          // 풀에서 돌아온 것(Release 로 꺼 둠): OnEnable 이 맞춘다
            else if (s.isActiveAndEnabled) s.Apply();
            return s;
        }

        /// <summary>풀에서 돌려 쓰는 렌더러가 더는 서 있지 않을 때: 원래 재질로 되돌리고 꺼 둔다.</summary>
        public static void Release(SpriteRenderer sr)
        {
            if (sr == null) return;
            var s = sr.GetComponent<StandingSprite>();
            if (s == null) return;
            s.Restore();
            s.enabled = false;
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            seed = ++seedCounter;   // 정수라 float 로도 1,600만까지 정확히 구분된다
            if (sr != null) original = sr.sharedMaterial;
        }

        void OnEnable()
        {
            TiltView.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            TiltView.Changed -= Apply;
            Restore();
        }

        /// <summary>반투명으로 바꾸거나 되돌린다 (겹친 소품 반투명, Step 14-6).</summary>
        public void SetTranslucent(bool value)
        {
            if (translucent == value) return;
            translucent = value;
            if (swapped) Apply();
        }

        void LateUpdate()
        {
            if (liftHeight > 0f && TiltView.Active) ApplyLift();
            // 컷아웃 재질은 알파를 섞지 못한다: 렌더러 색의 알파가 1 보다 작은 동안(무적 깜빡임, 쓰러진 아군, 흐려진 소품)만 반투명 재질로
            if (!swapped || sr == null) return;
            SetTranslucent(sr.color.a < 0.999f);
        }

        /// <summary>지금 틸트 상태에 맞춘다.</summary>
        public void Apply()
        {
            bool on = TiltView.Active;
            if (sr != null)
            {
                if (on)
                {
                    var mat = TiltView.StandingMaterial(footLift, toward, translucent, stretchScale);
                    if (mat != null)
                    {
                        if (!swapped) original = sr.sharedMaterial;
                        sr.sharedMaterial = mat;
                        sr.shadowCastingMode = translucent ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                        sr.receiveShadows = true;
                        // 스프라이트 배칭을 끊는다: 같은 재질의 스프라이트가 한 메시로 합쳐지면 오브젝트 행렬이 단위 행렬이 되어
                        // 셰이더가 발 위치(원점)를 세계 원점으로 착각해 y>0 은 떠오르고 y<0 은 땅에 묻힌다. 렌더러마다 다른 속성 블록이면 합쳐지지 않는다
                        if (block == null) block = new MaterialPropertyBlock();
                        sr.GetPropertyBlock(block);
                        block.SetFloat(SeedId, seed);
                        sr.SetPropertyBlock(block);
                        swapped = true;
                    }
                }
                else Restore();
            }
            if (liftHeight > 0f) ApplyLift();
        }

        /// <summary>자기 자리를 발에서 liftHeight 만큼 위로 (그 자리의 세로 배율로). 틸트에서는 자리가 바뀌면 배율도 바뀌어 매 프레임 맞춘다.</summary>
        void ApplyLift()
        {
            var parent = transform.parent;
            Vector2 at = parent != null ? (Vector2)parent.position : (Vector2)transform.position;
            var want = TiltView.Active ? TiltView.Lift(liftHeight, at) : new Vector3(0f, liftHeight, 0f);
            if ((transform.localPosition - want).sqrMagnitude > 1e-8f) transform.localPosition = want;
        }

        void Restore()
        {
            if (!swapped || sr == null) { swapped = false; return; }
            sr.sharedMaterial = original;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.SetPropertyBlock(null);   // 2D 로 돌아가면 다시 배칭되게
            swapped = false;
        }
    }
}
