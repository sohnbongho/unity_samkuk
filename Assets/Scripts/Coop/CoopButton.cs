using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Net;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samkuk.Coop
{
    /// <summary>
    /// 타이틀 화면 오른쪽 아래의 [협동] 버튼. 타이틀 씬은 셋업이 만든 고정 씬이라 씬을 다시 만들지 않도록 로드될 때 코드로 붙인다(MapEditorLauncher 와 같은 방식).
    /// 서버에 접속돼 있을 때만 누를 수 있고(아니면 "협동 (로그인 필요)"), 누르면 방 창(RoomPanel)을 연다.
    /// </summary>
    public class CoopButton : MonoBehaviour
    {
        public const string ButtonName = "CoopButton";

        Button button;
        bool lastOnline = true; // 처음 Update 에서 한 번 글자를 맞추도록

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            if (SceneManager.GetActiveScene().name != GameManager.TitleSceneName) return;
            var title = Object.FindAnyObjectByType<TitleController>();
            if (title == null) return;
            Attach(title.transform);
        }

        /// <summary>타이틀의 MainPanel(없으면 타이틀 루트)에 버튼을 만든다. 이미 있으면 그것.</summary>
        public static CoopButton Attach(Transform titleRoot)
        {
            var parent = titleRoot.Find("MainPanel");
            if (parent == null) parent = titleRoot;
            var existing = parent.Find(ButtonName);
            if (existing != null) return existing.GetComponent<CoopButton>();

            var go = new GameObject(ButtonName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-30f, 30f);
            rt.sizeDelta = new Vector2(300f, 70f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.35f, 0.5f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            UiSkin.StyleButton(button);
            var label = UiBuild.Text("Label", go.transform, UiFont.Get(), 30, TextAnchor.MiddleCenter, "협동");
            UiBuild.Stretch(label.rectTransform, 0f);

            var self = go.AddComponent<CoopButton>();
            self.button = button;
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                RoomPanel.Open(titleRoot);
            });
            return self;
        }

        void Update()
        {
            bool online = NetClient.Instance != null && NetClient.Instance.IsOnline;
            if (online == lastOnline) return;
            lastOnline = online;
            if (button == null) button = GetComponent<Button>();
            button.interactable = online;
            UiBuild.SetLabel(button, online ? "협동 (친구와 함께)" : "협동 (로그인 필요)");
        }
    }
}
