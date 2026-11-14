using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Meta;
using Samkuk.Compat;
using Samkuk.Compat.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 타이틀 왼쪽 설정 줄 맨 아래의 [3D 화면] 버튼 (Godot 판 3D 전환, G15). 켜면 전투를 로우폴리 3D 모델 + 원근(쿼터뷰) 카메라로,
    /// 끄면 예전 도트 그림 + 직교 카메라로 그린다. 저장된다(<see cref="SaveData.view3d"/>), 다음 전투부터 적용.
    /// 타이틀 씬은 변환된 고정 씬이라 씬 파일을 고치지 않도록 로드될 때 코드로 붙인다(CoopButton 과 같은 방식).
    /// 규칙은 이 값을 읽지 않는다 — 화면 층이 <see cref="Hd2dSettings.View3d"/> 를 읽어 그리는 방법만 바꾼다.
    /// </summary>
    public class View3dButton : MonoBehaviour
    {
        public const string ButtonName = "View3dButton";
        const string AnchorButtonName = "TiltViewButton";   // 이 버튼 바로 아래에 같은 크기로 놓는다
        const float Spacing = 80f;

        Text label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            if (SceneManager.GetActiveScene().name != GameManager.TitleSceneName) return;
            var title = Object.FindAnyObjectByType<TitleController>();
            if (title == null) return;
            Attach(title.transform);
        }

        /// <summary>[2.5D 시점] 버튼 아래(없으면 타이틀 루트의 같은 자리)에 버튼을 만든다. 이미 있으면 그것.</summary>
        public static View3dButton Attach(Transform titleRoot)
        {
            foreach (var old in titleRoot.GetComponentsInChildren<View3dButton>(true)) return old;

            // 설정 버튼들과 같은 부모·같은 크기로
            Transform parent = titleRoot;
            var position = new Vector2(40f, -750f - Spacing);
            var size = new Vector2(360f, 70f);
            foreach (var b in titleRoot.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name != AnchorButtonName) continue;
                var anchor = (RectTransform)b.transform;
                parent = anchor.parent;
                position = anchor.anchoredPosition + new Vector2(0f, -Spacing);
                size = anchor.sizeDelta;
                break;
            }

            var go = new GameObject(ButtonName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.28f, 0.4f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            UiSkin.StyleButton(button);

            var self = go.AddComponent<View3dButton>();
            self.label = UiBuild.Text("Label", go.transform, UiFont.Get(), 30, TextAnchor.MiddleCenter, "");
            UiBuild.Stretch(self.label.rectTransform, 0f);
            self.Refresh();
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(SfxId.Click);
                self.Toggle();
            });
            return self;
        }

        /// <summary>3D 화면을 켜고 끈다. 저장된다.</summary>
        public void Toggle()
        {
            var save = SaveSystem.Current;
            save.view3d = !save.view3d;
            SaveSystem.SaveCurrent();
            Refresh();
        }

        void Refresh()
        {
            if (label != null) label.text = $"3D 화면: {(SaveSystem.Current.view3d ? "켬" : "끔")}";
        }
    }
}
