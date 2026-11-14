using System;
using System.IO;
using NUnit.Framework;
using Samkuk.Core;
using Samkuk.Meta;
using Samkuk.UI;
using Samkuk.Compat;
using Samkuk.Compat.UI;

namespace Samkuk.Tests
{
    /// <summary>Godot 판에서 더한 테스트: 타이틀의 [3D 화면] 버튼 (G15).</summary>
    public class View3dButtonTests
    {
        string savePath;
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            // 실제 저장 파일을 건드리지 않도록 임시 경로를 쓴다
            savePath = Path.Combine(Application.temporaryCachePath, $"test_view3d_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
            Hd2dSettings.ResetOverrides();
            root = new GameObject("Title", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Audio.AudioManager.DestroyInstance();
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            Hd2dSettings.ResetOverrides();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            if (root != null) Samkuk.Compat.Object.DestroyImmediate(root);
        }

        static string LabelOf(View3dButton b) => b.GetComponentInChildren<Text>().text;

        [Test]
        public void View3d_DefaultsOn()
        {
            Assert.IsTrue(new SaveData().view3d);
            Assert.IsTrue(Hd2dSettings.View3d);
        }

        [Test]
        public void Attach_IsIdempotent_AndSitsBelowTheTiltButton()
        {
            var anchor = new GameObject("TiltViewButton", typeof(RectTransform));
            anchor.transform.SetParent(root.transform, false);
            var art = (RectTransform)anchor.transform;
            art.anchoredPosition = new Vector2(40f, -750f);
            art.sizeDelta = new Vector2(360f, 70f);
            anchor.AddComponent<Image>();
            anchor.AddComponent<Button>();

            var first = View3dButton.Attach(root.transform);
            var second = View3dButton.Attach(root.transform);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, root.GetComponentsInChildren<View3dButton>(true).Length);

            var rt = (RectTransform)first.transform;
            Assert.AreSame(anchor.transform.parent, rt.parent);
            Assert.AreEqual(40f, rt.anchoredPosition.x, 0.001f);
            Assert.Less(rt.anchoredPosition.y, art.anchoredPosition.y - art.sizeDelta.y + 0.001f, "[2.5D 시점] 버튼과 겹치지 않는다");
            Assert.AreEqual(art.sizeDelta.x, rt.sizeDelta.x, 0.001f);
        }

        [Test]
        public void Toggle_FlipsTheSetting_Saves_AndUpdatesLabel()
        {
            var button = View3dButton.Attach(root.transform);
            Assert.AreEqual("3D 화면: 켬", LabelOf(button));

            button.Toggle();
            Assert.IsFalse(Hd2dSettings.View3d);
            Assert.AreEqual("3D 화면: 끔", LabelOf(button));

            // 저장됐는지: 캐시를 버리고 다시 읽는다
            SaveSystem.ResetCache();
            Assert.IsFalse(SaveSystem.Current.view3d);

            button.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(Hd2dSettings.View3d);
            Assert.AreEqual("3D 화면: 켬", LabelOf(button));
        }

        [Test]
        public void Override_WinsOverTheSave()
        {
            Hd2dSettings.View3dOverride = false;
            Assert.IsFalse(Hd2dSettings.View3d);
            Assert.IsTrue(SaveSystem.Current.view3d, "저장은 건드리지 않는다");
        }
    }
}
