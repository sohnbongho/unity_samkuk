using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Samkuk.Tests
{
    /// <summary>HD-2D 후처리(Step 14-2): 코드로 만든 Volume 프로필(블룸·비네트·색 보정·색온도), 켜고 끄기.</summary>
    public class Hd2dPostFxTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.SortieCastle = null;
            Hd2dSettings.PostFxOverride = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.SortieCastle = null;
            Hd2dSettings.ResetOverrides();
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        BattlePostFx MakeCamera()
        {
            var go = new GameObject("Cam", typeof(Camera));
            toDestroy.Add(go);
            return go.AddComponent<BattlePostFx>();   // Awake 에서 Volume 을 만든다
        }

        [Test]
        public void Preset_BloomOnlyAffectsOverbrightLights()
        {
            Assert.GreaterOrEqual(PostFxPreset.BloomThreshold, 1f, "스프라이트(밝기 1 이하)는 번지지 않고 빛만 번진다");
            Assert.That(PostFxPreset.BloomIntensity, Is.InRange(0.2f, 1.5f));
            Assert.That(PostFxPreset.VignetteIntensity, Is.InRange(0.1f, 0.4f), "비네트는 은은하게");
        }

        [Test]
        public void Preset_Temperature_FollowsTimeOfDay()
        {
            Assert.Greater(PostFxPreset.Temperature(TimeOfDay.Dusk), PostFxPreset.Temperature(TimeOfDay.Day), "해질녘은 따뜻하게");
            Assert.Less(PostFxPreset.Temperature(TimeOfDay.Dawn), PostFxPreset.Temperature(TimeOfDay.Day), "새벽은 차갑게");
            Assert.AreEqual(0f, PostFxPreset.Temperature(TimeOfDay.Day));
        }

        [Test]
        public void PostFx_CreatesGlobalVolume_WithAllFourEffects()
        {
            var fx = MakeCamera();

            Assert.IsTrue(fx.IsEnabled);
            Assert.IsNotNull(fx.Volume);
            Assert.IsTrue(fx.Volume.isGlobal);
            Assert.AreEqual(1f, fx.Volume.weight);
            Assert.AreSame(fx.Profile, fx.Volume.sharedProfile);

            Assert.IsTrue(fx.Profile.TryGet<Bloom>(out var bloom));
            Assert.IsTrue(bloom.intensity.overrideState, "프로필 값이 기본값을 덮어써야 한다");
            Assert.AreEqual(PostFxPreset.BloomThreshold, bloom.threshold.value);
            Assert.AreEqual(PostFxPreset.BloomIntensity, bloom.intensity.value);
            Assert.IsTrue(fx.Profile.TryGet<Vignette>(out var vignette));
            Assert.AreEqual(PostFxPreset.VignetteIntensity, vignette.intensity.value);
            Assert.IsTrue(fx.Profile.TryGet<ColorAdjustments>(out var color));
            Assert.AreEqual(PostFxPreset.Contrast, color.contrast.value);
            Assert.AreEqual(PostFxPreset.Saturation, color.saturation.value);
            Assert.IsTrue(fx.Profile.TryGet<WhiteBalance>(out var wb));
            Assert.AreEqual(0f, wb.temperature.value, "성 없이 시작 = 낮");

            var data = fx.GetComponent<Camera>().GetUniversalAdditionalCameraData();
            Assert.IsTrue(data.renderPostProcessing);
            Assert.AreNotEqual(0, data.volumeLayerMask.value & (1 << fx.Volume.gameObject.layer), "볼륨이 카메라 마스크에 들어간다");
        }

        [Test]
        public void PostFx_Toggle_TurnsCameraPostProcessingOff()
        {
            var fx = MakeCamera();
            var data = fx.GetComponent<Camera>().GetUniversalAdditionalCameraData();

            fx.Apply(false);
            Assert.IsFalse(fx.IsEnabled);
            Assert.IsFalse(data.renderPostProcessing, "꺼지면 비용도 들지 않게 카메라 후처리 자체를 끈다");
            Assert.AreEqual(0f, fx.Volume.weight);

            fx.Apply(true);
            Assert.IsTrue(data.renderPostProcessing);
            Assert.AreEqual(1f, fx.Volume.weight);
        }

        [Test]
        public void PostFx_StartsOff_WhenSettingDisabled()
        {
            Hd2dSettings.PostFxOverride = false;
            var fx = MakeCamera();
            Assert.IsFalse(fx.IsEnabled);
            Assert.IsFalse(fx.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.IsNotNull(fx.Volume, "볼륨은 만들어 두고 무게만 0 (F6 로 바로 켤 수 있게)");
        }

        [Test]
        public void PostFx_UsesSortieCastleTimeOfDay()
        {
            var castle = ScriptableObject.CreateInstance<CastleData>();
            castle.id = "Luoyang"; castle.terrain = CastleTerrain.Plain;   // 생성기 기준 해질녘
            toDestroy.Add(castle);
            GameSession.SortieCastle = castle;

            var fx = MakeCamera();
            Assert.AreEqual(TimeOfDay.Dusk, fx.TimeOfDay);
            fx.Profile.TryGet<WhiteBalance>(out var wb);
            Assert.AreEqual(PostFxPreset.Temperature(TimeOfDay.Dusk), wb.temperature.value);

            fx.SetTimeOfDay(TimeOfDay.Dawn);
            Assert.AreEqual(PostFxPreset.Temperature(TimeOfDay.Dawn), wb.temperature.value);
            Assert.AreEqual(PostFxPreset.Tint(TimeOfDay.Dawn), wb.tint.value);
        }

        [Test]
        public void Hd2dSettings_PostFxOverride_WinsOverSave()
        {
            Hd2dSettings.PostFxOverride = false;
            Assert.IsFalse(Hd2dSettings.PostFx);
            Hd2dSettings.PostFxOverride = true;
            Assert.IsTrue(Hd2dSettings.PostFx);
        }
    }
}
