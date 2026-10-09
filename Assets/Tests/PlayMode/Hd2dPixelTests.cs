using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Player;
using Samkuk.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.Tests
{
    /// <summary>도트 규격(Step 14-3): PPU 32 / 640x360, Pixel Perfect Camera 켜고 끄기, 48칸 걷기 시트 슬라이스.</summary>
    public class Hd2dPixelTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp() => Hd2dSettings.PixelPerfectOverride = true;

        [TearDown]
        public void TearDown()
        {
            Hd2dSettings.ResetOverrides();
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        [Test]
        public void PixelArt_SupportedResolutions_AreIntegerZoom()
        {
            Assert.AreEqual(3, PixelArt.IntegerZoom(1080));
            Assert.AreEqual(4, PixelArt.IntegerZoom(1440));
            Assert.AreEqual(0, 1080 % PixelArt.RefHeight, "1080p 는 가상 해상도의 정수 배");
            Assert.AreEqual(0, 1440 % PixelArt.RefHeight, "1440p 는 가상 해상도의 정수 배");
            Assert.AreEqual(16f / 9f, (float)PixelArt.RefWidth / PixelArt.RefHeight, 1e-5f, "16:9");
            Assert.AreEqual(5.625f, PixelArt.OrthographicSize, 1e-5f);
            Assert.AreEqual(4f, (float)PixelArt.GroundTile / PixelArt.PPU, "바닥 타일은 4유닛 그대로");
        }

        BattlePixelCamera MakeCamera()
        {
            var go = new GameObject("Cam", typeof(Camera));
            toDestroy.Add(go);
            go.GetComponent<Camera>().orthographic = true;
            go.GetComponent<Camera>().orthographicSize = PixelArt.DefaultOrthographicSize;
            return go.AddComponent<BattlePixelCamera>();
        }

        [Test]
        public void PixelCamera_ConfiguresPixelPerfect_FromPixelArt()
        {
            var pc = MakeCamera();
            var ppc = pc.PixelPerfect;
            Assert.IsNotNull(ppc);
            Assert.IsTrue(pc.IsEnabled);
            Assert.IsTrue(ppc.enabled);
            Assert.AreEqual(PixelArt.PPU, ppc.assetsPPU);
            Assert.AreEqual(PixelArt.RefWidth, ppc.refResolutionX);
            Assert.AreEqual(PixelArt.RefHeight, ppc.refResolutionY);
            Assert.AreEqual(PixelPerfectCamera.CropFrame.None, ppc.cropFrame, "화면을 자르지 않는다");
            Assert.AreEqual(PixelPerfectCamera.GridSnapping.PixelSnapping, ppc.gridSnapping, "업스케일 렌더 텍스처는 쓰지 않는다 (조명·이펙트는 고해상도)");
        }

        [Test]
        public void PixelCamera_Toggle_RestoresDefaultSize()
        {
            var pc = MakeCamera();
            var cam = pc.GetComponent<Camera>();
            cam.orthographicSize = PixelArt.OrthographicSize;   // 켜진 동안 Pixel Perfect 가 바꿔 둔 값처럼

            pc.Apply(false);
            Assert.IsFalse(pc.IsEnabled);
            Assert.IsFalse(pc.PixelPerfect.enabled);
            Assert.AreEqual(PixelArt.DefaultOrthographicSize, cam.orthographicSize, 1e-5f, "끄면 Step 1 의 카메라 크기로");

            pc.Apply(true);
            Assert.IsTrue(pc.PixelPerfect.enabled);
        }

        [Test]
        public void PixelCamera_StartsOff_WhenSettingDisabled()
        {
            Hd2dSettings.PixelPerfectOverride = false;
            var pc = MakeCamera();
            Assert.IsFalse(pc.IsEnabled);
            Assert.IsFalse(pc.PixelPerfect.enabled);
        }

        [Test]
        public void WalkSheet_PixelCells_SliceTo48_And1_5Units()
        {
            var tex = new Texture2D(PixelArt.WalkCell * 4, PixelArt.WalkCell * 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            toDestroy.Add(tex);
            Assert.IsTrue(PixelArt.IsPixelWalkSheet(tex.width));
            Assert.IsFalse(PixelArt.IsPixelWalkSheet(PixelArt.LegacyWalkCell * 4));

            var set = HeroSpriteSet.Get(tex, PixelArt.PPU);
            var s = set.Get(FacingDir.Down, 0);
            Assert.AreEqual(PixelArt.WalkCell, s.rect.width);
            Assert.AreEqual(PixelArt.PPU, s.pixelsPerUnit);
            Assert.AreEqual(1.5f, s.bounds.size.y, 1e-4f, "한 칸 = 1.5유닛 (몸은 약 1유닛, 나머지는 말/외곽선 여백)");
        }

        [Test]
        public void Hd2dSettings_PixelPerfectOverride_WinsOverSave()
        {
            Hd2dSettings.PixelPerfectOverride = false;
            Assert.IsFalse(Hd2dSettings.PixelPerfect);
            Hd2dSettings.PixelPerfectOverride = true;
            Assert.IsTrue(Hd2dSettings.PixelPerfect);
        }
    }
}
