using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samkuk.World
{
    /// <summary>
    /// 타이틀 화면 왼쪽 아래에 [맵 편집기] 버튼을 더한다 (에디터와 개발용 빌드에서만, 편집기 씬이 빌드에 있을 때).
    /// 타이틀 씬은 셋업이 만든 고정 씬이라, 씬을 다시 만들지 않아도 되도록 로드될 때 코드로 붙인다.
    /// </summary>
    public static class MapEditorLauncher
    {
        public const string ButtonName = "MapEditorButton";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            if (SceneManager.GetActiveScene().name != GameManager.TitleSceneName) return;
            if (!Application.isEditor && !Debug.isDebugBuild) return;   // 릴리스 빌드(친구에게 보내는 것)에는 개발 도구를 숨긴다
            if (!Application.CanStreamedLevelBeLoaded(GameManager.MapEditorSceneName)) return;

            var title = Object.FindAnyObjectByType<TitleController>();
            if (title == null) return;
            var parent = title.transform.Find("MainPanel");
            if (parent == null) parent = title.transform;
            if (parent.Find(ButtonName) != null) return;
            CreateButton(parent);
        }

        static void CreateButton(Transform parent)
        {
            var go = new GameObject(ButtonName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(30f, 30f);
            rt.sizeDelta = new Vector2(250f, 70f);

            var img = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            UiSkin.StyleButton(button);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = UiFont.Get();
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "맵 편집기";
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                SceneManager.LoadScene(GameManager.MapEditorSceneName);
            });
        }
    }
}
