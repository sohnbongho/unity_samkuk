using System;
using UnityEngine;

namespace Samkuk.Net
{
    /// <summary>
    /// 게임 전체에서 하나뿐인 서버 연결(DontDestroyOnLoad). 타이틀에서 로그인하면 전투/내정 씬으로 가도 연결 유지(KeepAlive)가 이어진다.
    /// 흐름은 LoginFlow 가, 소켓은 TcpLink 가 맡고 이 컴포넌트는 매 프레임 Tick 을 돌려 단계 변화를 알린다.
    /// 일시정지(timeScale 0) 중에도 연결은 유지해야 하므로 unscaledDeltaTime.
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        static NetClient instance;
        public static NetClient Instance => instance;

        /// <summary>이번 실행에서 [오프라인으로 계속] 을 골랐다 (저장하지 않음: 다음 실행에서 다시 묻는다).</summary>
        public static bool OfflineChosen { get; set; }
        /// <summary>연결을 만드는 방법. 테스트가 가짜 연결로 바꾼다.</summary>
        public static Func<string, int, INetLink> LinkFactory = (host, port) => new TcpLink(host, port);

        LoginFlow flow;
        NetPhase lastPhase = NetPhase.Offline;

        /// <summary>단계가 바뀔 때(메인 스레드).</summary>
        public event Action<NetPhase> PhaseChanged;

        public LoginFlow Flow => flow;
        public NetPhase Phase => flow != null ? flow.Phase : NetPhase.Offline;
        public string Message => flow != null ? flow.Message : "";
        public string UserId => flow != null ? flow.UserId : "";
        public bool IsOnline => Phase == NetPhase.Online;
        public bool IsBusy => flow != null && flow.IsBusy;

        public static NetClient GetOrCreate()
        {
            if (instance == null)
            {
                var go = new GameObject("NetClient");
                instance = go.AddComponent<NetClient>();
            }
            return instance;
        }

        /// <summary>테스트 정리용.</summary>
        public static void DestroyInstance()
        {
            if (instance == null) return;
            var go = instance.gameObject;
            instance.flow?.Disconnect();
            instance.flow = null;
            instance = null;
            Destroy(go);
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            // 창이 뒤로 가도(알트탭, 서버 콘솔 보기) Update 가 멈추지 않게. 멈추면 연결 유지가 끊기고(서버 10초) 로그인 흐름도 그 자리에 선다.
            // 에디터도 이 값을 따른다 (Player 설정의 Run In Background 와 별개로 실행 중 켠다).
            Application.runInBackground = true;
        }

        /// <summary>로그인 서버 → 게임 서버 접속을 시작한다. 이미 연결돼 있으면 끊고 새로.</summary>
        public void Login(string userId, string password)
        {
            flow?.Disconnect();
            flow = new LoginFlow(LinkFactory);
            lastPhase = NetPhase.Offline;
            flow.Begin(userId, PasswordHash.Client(password), NetSettings.Host, NetSettings.LoginPort, NetSettings.GamePort);
            Notify();
        }

        public void Logout()
        {
            flow?.Disconnect();
            Notify();
        }

        void Update()
        {
            if (flow == null) return;
            flow.Tick(Time.unscaledDeltaTime);
            Notify();
        }

        void Notify()
        {
            var phase = Phase;
            if (phase == lastPhase) return;
            lastPhase = phase;
            Debug.Log($"[Net] {phase}: {Message}");   // 서버 로그와 시각을 맞춰 보기 위한 흔적 (단계가 바뀔 때만)
            PhaseChanged?.Invoke(phase);
        }

        void OnApplicationQuit() => flow?.Disconnect();

        void OnDestroy()
        {
            flow?.Disconnect();
            if (instance == this) instance = null;
        }
    }
}
