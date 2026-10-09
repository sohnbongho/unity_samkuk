using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.Tests
{
    /// <summary>틸트 시프트(Step 14-5): 수치표, 전투 동안만 렌더러 기능 켜기, 해상도에 맞춘 흐림 반지름.</summary>
    public class Hd2dTiltShiftTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [SetUp]
        public void SetUp() => Hd2dSettings.TiltShiftOverride = true;

        [TearDown]
        public void TearDown()
        {
            Hd2dSettings.ResetOverrides();
            foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
            toDestroy.Clear();
        }

        [Test]
        public void Preset_IsSane()
        {
            Assert.That(TiltShiftPreset.Band, Is.InRange(0.05f, 0.4f), "가운데 띠는 또렷하게 남긴다");
            Assert.AreEqual(0.5f, TiltShiftPreset.Focus, "플레이어는 화면 가운데");
            Assert.AreEqual(6f, TiltShiftPreset.RadiusFor(1080), 1e-4f);
            Assert.AreEqual(12f, TiltShiftPreset.RadiusFor(2160), 1e-4f, "화면이 두 배면 반지름도 두 배 (같은 두께로 보이게)");
            Assert.AreEqual(8f, TiltShiftPreset.RadiusFor(1440), 1e-4f);
        }

        [Test]
        public void Preset_Apply_WritesMaterialValues()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            toDestroy.Add(mat);
            TiltShiftPreset.Apply(mat, 1440);
            Assert.AreEqual(TiltShiftPreset.Amount, mat.GetFloat(TiltShiftPreset.AmountId));
            Assert.AreEqual(TiltShiftPreset.Focus, mat.GetFloat(TiltShiftPreset.FocusId));
            Assert.AreEqual(TiltShiftPreset.Band, mat.GetFloat(TiltShiftPreset.BandId));
            Assert.AreEqual(TiltShiftPreset.RadiusFor(1440), mat.GetFloat(TiltShiftPreset.RadiusId), 1e-4f);
        }

        BattleTiltShift MakeCamera(out FullScreenPassRendererFeature feature, out Material mat)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.SetActive(false);
            toDestroy.Add(feature);
            mat = new Material(Shader.Find("Sprites/Default"));
            toDestroy.Add(mat);
            var go = new GameObject("Cam", typeof(Camera));
            go.SetActive(false);   // Awake 전에 연결
            toDestroy.Add(go);
            var tilt = go.AddComponent<BattleTiltShift>();
            tilt.Feature = feature;
            tilt.Material = mat;
            go.SetActive(true);
            return tilt;
        }

        [Test]
        public void TiltShift_EnablesFeature_OnlyWhileAlive()
        {
            var tilt = MakeCamera(out var feature, out var mat);
            Assert.IsTrue(tilt.IsEnabled);
            Assert.IsTrue(feature.isActive, "전투 카메라가 살아 있는 동안 켜진다");
            Assert.AreEqual(TiltShiftPreset.Amount, mat.GetFloat(TiltShiftPreset.AmountId));

            tilt.Apply(false);
            Assert.IsFalse(feature.isActive, "끄면 패스가 돌지 않는다");
            tilt.Apply(true);
            Assert.IsTrue(feature.isActive);

            tilt.gameObject.SetActive(false);   // 씬을 떠날 때처럼
            Assert.IsFalse(feature.isActive, "카메라가 사라지면 다른 씬이 흐려지지 않게 끈다");
            tilt.gameObject.SetActive(true);
            Assert.IsTrue(feature.isActive, "돌아오면 다시 켠다");
        }

        [Test]
        public void TiltShift_StartsOff_WhenSettingDisabled()
        {
            Hd2dSettings.TiltShiftOverride = false;
            var tilt = MakeCamera(out var feature, out _);
            Assert.IsFalse(tilt.IsEnabled);
            Assert.IsFalse(feature.isActive);
            tilt.gameObject.SetActive(false);
            tilt.gameObject.SetActive(true);
            Assert.IsFalse(feature.isActive, "꺼 둔 상태는 다시 켜질 때도 유지");
        }

        [Test]
        public void TiltShift_Refresh_FollowsScreenHeight()
        {
            var tilt = MakeCamera(out _, out var mat);
            tilt.Refresh(2160);
            Assert.AreEqual(TiltShiftPreset.RadiusFor(2160), mat.GetFloat(TiltShiftPreset.RadiusId), 1e-4f);
        }

        [Test]
        public void Hd2dSettings_TiltShiftOverride_WinsOverSave()
        {
            Hd2dSettings.TiltShiftOverride = false;
            Assert.IsFalse(Hd2dSettings.TiltShift);
            Hd2dSettings.TiltShiftOverride = true;
            Assert.IsTrue(Hd2dSettings.TiltShift);
        }
    }
}
