using Samkuk.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 카메라의 도트 규격(Step 14-3): URP Pixel Perfect Camera 를 <see cref="PixelArt"/> 값(PPU 32, 640x360)으로 맞추고
    /// 켜고 끈다. 업스케일 렌더 텍스처는 쓰지 않는다 — 스프라이트만 픽셀 격자에 맞추고 조명·이펙트·후처리는 화면 해상도 그대로(HD-2D).
    /// 켜면 카메라 반높이가 5.625 유닛(640x360 / 32)이 되고, 끄면 Step 1 의 6 으로 돌아간다. 설정 <see cref="Hd2dSettings.PixelPerfect"/>, 디버그 키 F7.
    /// 셋업(Step 14-3)이 전투 씬 Main Camera 에 붙인다. 맵 편집기 카메라에는 붙이지 않는다(자유 줌).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattlePixelCamera : MonoBehaviour
    {
        Camera cam;
        PixelPerfectCamera ppc;

        public PixelPerfectCamera PixelPerfect => ppc;
        public bool IsEnabled { get; private set; }

        void Awake()
        {
            cam = GetComponent<Camera>();
            ppc = GetComponent<PixelPerfectCamera>();
            if (ppc == null) ppc = gameObject.AddComponent<PixelPerfectCamera>();
            Configure(ppc);
            Apply(Hd2dSettings.PixelPerfect);
        }

        /// <summary>Pixel Perfect Camera 를 도트 규격으로 맞춘다 (셋업과 실행 중 모두 이 한 곳).</summary>
        public static void Configure(PixelPerfectCamera p)
        {
            p.assetsPPU = PixelArt.PPU;
            p.refResolutionX = PixelArt.RefWidth;
            p.refResolutionY = PixelArt.RefHeight;
            p.cropFrame = PixelPerfectCamera.CropFrame.None;
            p.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
        }

        /// <summary>켜면 픽셀 격자 맞춤, 끄면 예전 카메라 크기로.</summary>
        public void Apply(bool enabled)
        {
            IsEnabled = enabled;
            ppc.enabled = enabled;
            if (!enabled) cam.orthographicSize = PixelArt.DefaultOrthographicSize;
        }
    }
}
