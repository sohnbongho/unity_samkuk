using System.Collections.Generic;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.UI;
using UnityEditor;
using UnityEngine;

namespace Samkuk.Tests
{
    /// <summary>친구에게 나눠 주는 빌드(Step 11)에 필요한 것: 버전 표시, 릴리스에서 꺼지는 디버그 도구, 빌드할 씬.</summary>
    public class BuildTests
    {
        readonly List<Object> toDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
            toDestroy.Clear();
        }

        [Test]
        public void VersionText_ShowsApplicationVersion()
        {
            string text = TitleController.VersionText();
            StringAssert.StartsWith("v", text);
            StringAssert.Contains(Application.version, text);
            StringAssert.DoesNotContain("(dev)", text, "에디터에서는 개발용 표시를 붙이지 않는다");
        }

        [Test]
        public void BuildSettings_ListsTitleBattleStrategyInOrder()
        {
            var paths = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) paths.Add(s.path);

            CollectionAssert.AreEqual(
                new[] { "Assets/Scenes/TitleScene.unity", "Assets/Scenes/BattleScene.unity", "Assets/Scenes/StrategyScene.unity" },
                paths, "빌드에는 타이틀(0), 전투(1), 내정(2) 씬이 이 순서로 있어야 한다. 메뉴 Samkuk > Run All Setup 으로 등록된다");
        }

        [Test]
        public void DebugOverlay_StaysEnabledInEditor()
        {
            // 릴리스 빌드(에디터가 아니고 Development Build 도 아님)에서만 꺼진다. 에디터에서는 개발 편의를 위해 켜져 있어야 한다.
            var go = new GameObject("DebugOverlayTest");
            toDestroy.Add(go);
            var overlay = go.AddComponent<DebugOverlay>();
            Assert.IsTrue(overlay.enabled);
        }
    }
}
