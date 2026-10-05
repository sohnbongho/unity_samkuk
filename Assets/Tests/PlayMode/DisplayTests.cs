using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Meta;
using Samkuk.Strategy;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    /// <summary>화면 설정(해상도/창 모드)과 내정 화면의 기준 해상도.</summary>
    public class DisplayTests
    {
        string savePath;
        readonly List<UnityEngine.Object> toDestroy = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            // 실제 저장 파일을 건드리지 않도록 임시 경로를 쓴다
            savePath = Path.Combine(Application.temporaryCachePath, $"test_display_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
            DisplaySettings.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DisplaySettings.ResetForTests();
            Audio.AudioManager.DestroyInstance();
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            foreach (var o in toDestroy) if (o != null) UnityEngine.Object.Destroy(o);
            toDestroy.Clear();
        }

        TitleController MakeTitle()
        {
            var go = new GameObject("TitleDisplayTest");
            go.SetActive(false); // Awake 가 돌지 않게 (버튼 없이 동작만 확인)
            toDestroy.Add(go);
            return go.AddComponent<TitleController>();
        }

        // ───────────────────────── 목록 / 순환 ─────────────────────────

        [Test]
        public void Available_DropsPresetsLargerThanMonitor()
        {
            var fhd = DisplaySettings.Available(1920, 1080);
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) }, fhd);

            var uhd = DisplaySettings.Available(3840, 2160);
            Assert.AreEqual(5, uhd.Count);
            Assert.AreEqual(new Vector2Int(3840, 2160), uhd[uhd.Count - 1]);
        }

        [Test]
        public void Available_KeepsSmallestPreset_OnTinyMonitor()
        {
            var list = DisplaySettings.Available(800, 600);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(new Vector2Int(1280, 720), list[0]);
        }

        [Test]
        public void NextResolution_CyclesAndWraps()
        {
            var save = new SaveData(); // 기본 1920x1080
            Assert.AreEqual(new Vector2Int(2560, 1440), DisplaySettings.NextResolution(save, 3840, 2160));
            Assert.AreEqual(new Vector2Int(1280, 720), DisplaySettings.NextResolution(save, 1920, 1080), "목록 끝에서 처음으로");

            save.displayWidth = 1280; save.displayHeight = 720;
            Assert.AreEqual(new Vector2Int(1600, 900), DisplaySettings.NextResolution(save, 1920, 1080));
        }

        [Test]
        public void NextResolution_FromUnknownSize_UsesNearestPreset()
        {
            var save = new SaveData { displayWidth = 1900, displayHeight = 1000 }; // 1920x1080 에 가장 가깝다
            Assert.AreEqual(new Vector2Int(2560, 1440), DisplaySettings.NextResolution(save, 3840, 2160));
        }

        [Test]
        public void Modes_CycleThroughThree_AndInvalidFallsBackToBorderless()
        {
            Assert.AreEqual(DisplaySettings.ModeBorderless, DisplaySettings.NextMode(DisplaySettings.ModeWindowed));
            Assert.AreEqual(DisplaySettings.ModeExclusive, DisplaySettings.NextMode(DisplaySettings.ModeBorderless));
            Assert.AreEqual(DisplaySettings.ModeWindowed, DisplaySettings.NextMode(DisplaySettings.ModeExclusive));

            Assert.AreEqual(DisplaySettings.ModeBorderless, DisplaySettings.Normalize(99));
            Assert.AreEqual(DisplaySettings.ModeBorderless, DisplaySettings.Normalize(-1));
            Assert.AreEqual(FullScreenMode.Windowed, DisplaySettings.ToFullScreenMode(DisplaySettings.ModeWindowed));
            Assert.AreEqual(FullScreenMode.FullScreenWindow, DisplaySettings.ToFullScreenMode(DisplaySettings.ModeBorderless));
            Assert.AreEqual(FullScreenMode.ExclusiveFullScreen, DisplaySettings.ToFullScreenMode(DisplaySettings.ModeExclusive));
        }

        [Test]
        public void ResolutionLabel_IsAutoInBorderless_AndChoosableOtherwise()
        {
            var save = new SaveData(); // 기본: 테두리 없는 전체화면
            Assert.AreEqual("해상도: 자동", DisplaySettings.ResolutionLabel(save));
            Assert.IsFalse(DisplaySettings.CanChooseResolution(save));

            save.windowMode = DisplaySettings.ModeWindowed;
            Assert.AreEqual("해상도: 1920x1080", DisplaySettings.ResolutionLabel(save));
            Assert.IsTrue(DisplaySettings.CanChooseResolution(save));
        }

        // ───────────────────────── 적용 / 저장 ─────────────────────────

        [Test]
        public void Apply_PassesSavedValues_AndApplyOnceRunsOnlyOnce()
        {
            var calls = new List<(int w, int h, FullScreenMode m)>();
            DisplaySettings.ApplyAction = (w, h, m) => calls.Add((w, h, m));
            var save = new SaveData { displayWidth = 2560, displayHeight = 1440, windowMode = DisplaySettings.ModeExclusive };

            DisplaySettings.ApplyOnce(save);
            DisplaySettings.ApplyOnce(save);
            Assert.AreEqual(1, calls.Count, "ApplyOnce 는 처음 한 번만");
            Assert.AreEqual((2560, 1440, FullScreenMode.ExclusiveFullScreen), calls[0]);

            DisplaySettings.Apply(save);
            Assert.AreEqual(2, calls.Count, "Apply 는 항상 적용");
        }

        [Test]
        public void SaveData_DefaultsMatchProjectDefaults_AndOldSavesStillLoad()
        {
            var fresh = new SaveData();
            Assert.AreEqual(1920, fresh.displayWidth);
            Assert.AreEqual(1080, fresh.displayHeight);
            Assert.AreEqual(DisplaySettings.ModeBorderless, fresh.windowMode);

            // 화면 설정이 생기기 전의 저장 파일: 없는 필드는 기본값으로 남아야 한다
            var old = JsonUtility.FromJson<SaveData>("{\"gold\":123,\"sfxVolume\":0.3}");
            Assert.AreEqual(123, old.gold);
            Assert.AreEqual(1920, old.displayWidth);
            Assert.AreEqual(DisplaySettings.ModeBorderless, old.windowMode);
        }

        [Test]
        public void Title_CycleWindowMode_SavesAndApplies()
        {
            var applied = new List<FullScreenMode>();
            DisplaySettings.ApplyAction = (w, h, m) => applied.Add(m);
            var title = MakeTitle();

            title.CycleWindowMode(); // 전체화면(기본) → 전용 전체화면
            Assert.AreEqual(DisplaySettings.ModeExclusive, SaveSystem.Current.windowMode);
            CollectionAssert.AreEqual(new[] { FullScreenMode.ExclusiveFullScreen }, applied);

            SaveSystem.ResetCache(); // 파일에서 다시 읽어도 유지되어야 한다
            Assert.AreEqual(DisplaySettings.ModeExclusive, SaveSystem.Current.windowMode);
        }

        [Test]
        public void Title_CycleResolution_IgnoredInBorderless_ButWorksInWindowed()
        {
            int applyCount = 0;
            DisplaySettings.ApplyAction = (w, h, m) => applyCount++;
            var title = MakeTitle();
            title.DesktopSize = () => new Vector2Int(2560, 1440);

            title.CycleResolution(); // 기본이 테두리 없는 전체화면이라 바뀌지 않는다
            Assert.AreEqual(1920, SaveSystem.Current.displayWidth);
            Assert.AreEqual(0, applyCount);

            SaveSystem.Current.windowMode = DisplaySettings.ModeWindowed;
            title.CycleResolution();
            Assert.AreEqual(2560, SaveSystem.Current.displayWidth);
            Assert.AreEqual(1440, SaveSystem.Current.displayHeight);
            title.CycleResolution(); // 모니터(2560x1440)보다 큰 것은 없으므로 처음으로
            Assert.AreEqual(1280, SaveSystem.Current.displayWidth);
            Assert.AreEqual(2, applyCount);
        }

        // ───────────────────────── 내정 화면 기준 해상도 ─────────────────────────

        [Test]
        public void StrategyUi_SetsCanvasScalerToItsReferenceResolution()
        {
            var go = new GameObject("StrategyScalerTest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            toDestroy.Add(go);
            go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f); // 예전 씬에 저장된 값
            var catalog = ScriptableObject.CreateInstance<Samkuk.Data.CastleCatalog>();
            toDestroy.Add(catalog);

            var ui = go.AddComponent<StrategyUI>();
            ui.Configure(catalog, null);
            ui.EnsureBuilt();

            var scaler = go.GetComponent<CanvasScaler>();
            Assert.AreEqual(StrategyUI.ReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(2560f, scaler.referenceResolution.x);
            Assert.AreEqual(1440f, scaler.referenceResolution.y);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode, "비율이 달라도 화면 전체가 보여야 한다");
        }
    }
}
