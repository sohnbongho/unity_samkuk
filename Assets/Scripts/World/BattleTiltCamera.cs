using Samkuk.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 카메라의 2.5D 틸트(Step 16-1, 셋업 Step 16-1 이 Main Camera 에 붙인다). 켜면 원근 카메라(<see cref="TiltPreset"/>)로 바꾸고
    /// URP 에셋의 3D 렌더러(<see cref="TiltView.RendererName"/>, 그림자·3D 빛)를 쓰며, 끄면 직교 + 기본(2D) 렌더러로 돌아간다.
    /// 카메라 자리·회전은 <see cref="CameraFollow"/> 가, 픽셀 퍼펙트는 <see cref="BattlePixelCamera"/> 가 틸트일 때 스스로 비켜 준다.
    /// 3D 렌더러나 셰이더가 없으면(셋업 전, 셰이더 오류) 경고만 내고 탑다운으로 둔다. 설정 <see cref="Hd2dSettings.Tilt"/>, 디버그 키 F10.
    /// 컬링은 시야각을 조금 넓힌 행렬로 한다: 서 있는 그림은 눕힌 트랜스폼의 범위보다 화면에서 높이 솟아 아래 가장자리에서 먼저 잘릴 수 있다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattleTiltCamera : MonoBehaviour
    {
        Camera cam;
        UniversalAdditionalCameraData data;
        int rendererIndex = -1;
        bool warned;

        public bool IsEnabled { get; private set; }
        /// <summary>URP 에셋 안 3D 렌더러 자리 (없으면 -1).</summary>
        public int RendererIndex => rendererIndex;

        void Awake()
        {
            cam = GetComponent<Camera>();
            data = cam.GetUniversalAdditionalCameraData();
        }

        void OnEnable()
        {
            rendererIndex = TiltView.FindRendererIndex();
            bool ok = rendererIndex >= 0 && TiltView.ShaderAvailable;
            if (!ok && !warned)
            {
                warned = true;
                Debug.LogWarning($"[Samkuk] 2.5D 틸트: 3D 렌더러({TiltView.RendererName}.asset){(TiltView.ShaderAvailable ? "" : " 또는 셰이더 " + TiltView.ShaderName)} 가 없어 탑다운으로 둡니다 (메뉴 Step 16-1 실행)");
            }
            TiltView.Changed += OnChanged;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            TiltView.CameraTransform = transform;
            TiltView.SetReady(ok);     // 바뀌면 Changed → Apply
            Apply(TiltView.Active);    // 바뀌지 않았어도 한 번은 맞춘다
        }

        void OnDisable()
        {
            TiltView.Changed -= OnChanged;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (TiltView.CameraTransform == transform) TiltView.CameraTransform = null;
            TiltView.SetReady(false);
        }

        void OnChanged() => Apply(TiltView.Active);

        /// <summary>켜면 기울인 직교 카메라 + 3D 렌더러, 끄면 탑다운 직교 + 기본 렌더러. 픽셀 퍼펙트는 다시 맞춘다.</summary>
        public void Apply(bool enabled)
        {
            IsEnabled = enabled;
            if (cam == null) cam = GetComponent<Camera>();
            if (data == null) data = cam.GetUniversalAdditionalCameraData();
            if (enabled)
            {
                cam.orthographic = true;
                cam.orthographicSize = TiltPreset.OrthoSize;
                cam.nearClipPlane = TiltPreset.Near;
                cam.farClipPlane = TiltPreset.Far;
                if (rendererIndex >= 0) data.SetRenderer(rendererIndex);
            }
            else
            {
                cam.ResetCullingMatrix();
                cam.orthographic = true;
                data.SetRenderer(-1);   // 기본 렌더러(2D)
            }
            var pixel = GetComponent<BattlePixelCamera>();
            if (pixel != null) pixel.Apply(Hd2dSettings.PixelPerfect);
        }

        bool lightingChecked;

        void LateUpdate()
        {
            // 지형 맵이 없어(셋업 전, 그림 없음) BattleLighting 이 안 생긴 판에도 빛은 있어야 한다 — 모든 Awake 가 끝난 첫 프레임에 한 번 확인
            if (!IsEnabled || lightingChecked) return;
            lightingChecked = true;
            if (FindAnyObjectByType<BattleLighting>() != null) return;
            var lighting = gameObject.AddComponent<BattleLighting>();
            lighting.Initialize(Samkuk.Data.CastleTerrain.Plain, CastleMood.Of(GameSession.SortieCastle));
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != cam || !IsEnabled) return;
            float h = cam.orthographicSize + TiltPreset.CullMargin, w = cam.orthographicSize * cam.aspect + TiltPreset.CullMargin;
            cam.cullingMatrix = Matrix4x4.Ortho(-w, w, -h, h, cam.nearClipPlane, cam.farClipPlane) * cam.worldToCameraMatrix;
        }
    }
}
