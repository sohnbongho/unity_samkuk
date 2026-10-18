using System;
using Samkuk.Audio;
using Samkuk.Meta;
using Samkuk.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>
    /// 타이틀에 들어오면 먼저 뜨는 로그인 창(아이디/비밀번호 → [로그인] / [오프라인으로 계속]). 타이틀 씬을 다시 만들지 않도록
    /// 코드로 만들어 캔버스 맨 위에 덮는다(뒤의 메인 버튼은 가려져 눌리지 않음). 접속이 끝나거나 오프라인을 고르면 사라진다.
    /// 이미 접속 중이거나 이번 실행에서 오프라인을 골랐으면 다시 뜨지 않는다(전투에서 타이틀로 돌아올 때).
    /// </summary>
    public class LoginPanel : MonoBehaviour
    {
        public const string RootName = "LoginPanel";
        /// <summary>테스트/도구에서 자동으로 뜨지 않게.</summary>
        public static bool AutoShow = true;

        InputField idField, pwField;
        Text status;
        Button loginButton, offlineButton;
        NetClient client;
        bool busy;

        public string UserId { get => idField.text; set => idField.text = value; }
        public string Password { set => pwField.text = value; }
        public string Status => status.text;
        public bool Busy => busy;

        /// <summary>타이틀에 들어올 때 창을 띄워야 하는가: 접속돼 있거나 이번 실행에서 오프라인을 골랐으면 아니다.</summary>
        public static bool ShouldShow()
        {
            if (!AutoShow) return false;
            if (NetClient.OfflineChosen) return false;
            if (NetClient.Instance != null && NetClient.Instance.IsOnline) return false;
            return true;
        }

        /// <summary>타이틀 컨트롤러가 Start 에서 부른다. 필요 없으면 null.</summary>
        public static LoginPanel Attach(TitleController title)
        {
            if (title == null || !ShouldShow()) return null;
            var existing = title.transform.Find(RootName);
            if (existing != null) return existing.GetComponent<LoginPanel>();
            return Create(title.transform);
        }

        public static LoginPanel Create(Transform parent)
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var blocker = go.AddComponent<Image>();                   // 뒤의 버튼을 막는 어두운 덮개
            blocker.color = new Color(0f, 0f, 0f, 0.6f);

            var panel = go.AddComponent<LoginPanel>();
            panel.Build();
            return panel;
        }

        void Build()
        {
            var font = UiFont.Get();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(transform, false);
            var brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(640f, 520f);
            var boxImg = box.GetComponent<Image>();
            boxImg.color = new Color(0.12f, 0.09f, 0.07f, 0.96f);
            UiSkin.StylePanel(boxImg);

            var title = UiBuild.Text("Title", box.transform, font, 44, TextAnchor.MiddleCenter, "로그인");
            UiBuild.Place(title.rectTransform, 0f, 200f, 560f, 60f);
            UiSkin.StyleTitle(title);

            idField = UiBuild.Input("IdField", box.transform, font, "아이디", 110f, false);
            pwField = UiBuild.Input("PasswordField", box.transform, font, "비밀번호", 30f, true);

            status = UiBuild.Text("Status", box.transform, font, 24, TextAnchor.MiddleCenter, "");
            UiBuild.Place(status.rectTransform, 0f, -40f, 580f, 60f);
            status.color = new Color(1f, 0.85f, 0.6f);

            loginButton = UiBuild.Button("LoginButton", box.transform, font, "로그인  [Enter]", -120f, new Vector2(400f, 76f), new Color(0.55f, 0.2f, 0.18f));
            offlineButton = UiBuild.Button("OfflineButton", box.transform, font, "오프라인으로 계속", -205f, new Vector2(400f, 64f), new Color(0.25f, 0.25f, 0.3f));
            loginButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); Submit(); });
            offlineButton.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); ContinueOffline(); });

            idField.text = SaveSystem.Current.lastUserId ?? "";
            SetStatus($"서버 {NetSettings.Host}:{NetSettings.LoginPort}");

            // 이미 시도 중인 접속(예: 실패 뒤 타이틀로 돌아옴)이 있으면 그 상태를 이어서 보여 준다
            if (NetClient.Instance != null && NetClient.Instance.IsBusy) Watch(NetClient.Instance);
        }

        void Update()
        {
            if (busy) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Submit();
        }

        /// <summary>입력을 검사하고 접속을 시작한다.</summary>
        public void Submit()
        {
            if (busy) return;
            var id = (idField.text ?? "").Trim();
            var pw = pwField.text ?? "";
            if (id.Length == 0) { SetStatus("아이디를 입력하세요"); Focus(idField); return; }
            if (pw.Length == 0) { SetStatus("비밀번호를 입력하세요"); Focus(pwField); return; }

            var net = NetClient.GetOrCreate();
            Watch(net);
            net.Login(id, pw);
        }

        /// <summary>서버 없이 플레이. 이번 실행 동안은 다시 묻지 않는다.</summary>
        public void ContinueOffline()
        {
            NetClient.OfflineChosen = true;
            NetClient.Instance?.Logout();
            Close();
        }

        void Watch(NetClient net)
        {
            if (client != null) client.PhaseChanged -= OnPhase;
            client = net;
            client.PhaseChanged += OnPhase;
            SetBusy(true);
            SetStatus(client.Message);
        }

        void OnPhase(NetPhase phase)
        {
            if (client == null) return;
            SetStatus(client.Message);
            if (phase == NetPhase.Online)
            {
                SaveSystem.Current.lastUserId = client.UserId;      // 다음 실행에 아이디만 채워 둔다 (비밀번호는 저장 안 함)
                SaveSystem.SaveCurrent();
                Close();
            }
            else if (phase == NetPhase.Failed || phase == NetPhase.Offline)
            {
                SetBusy(false);
            }
        }

        void SetBusy(bool value)
        {
            busy = value;
            if (loginButton != null) loginButton.interactable = !value;
            if (idField != null) idField.interactable = !value;
            if (pwField != null) pwField.interactable = !value;
        }

        void SetStatus(string text)
        {
            if (status != null) status.text = text ?? "";
        }

        static void Focus(InputField field)
        {
            if (field != null && field.isActiveAndEnabled) field.ActivateInputField();
        }

        void Close()
        {
            if (client != null) client.PhaseChanged -= OnPhase;
            client = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (client != null) client.PhaseChanged -= OnPhase;
            client = null;
        }
    }
}
