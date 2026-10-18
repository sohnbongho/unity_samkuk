using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Messages;
using NUnit.Framework;
using Samkuk.Meta;
using Samkuk.Net;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Samkuk.Tests
{
    /// <summary>
    /// 서버 로그인(Step 15-1): 프레임 파서, 비밀번호 해시, 로그인 상태 기계(LoginFlow), NetClient, 타이틀 로그인 창.
    /// 실제 소켓은 쓰지 않고 가짜 연결(FakeLink)로 서버의 응답을 흉내 낸다.
    /// </summary>
    public class NetLoginTests
    {
        // 가짜 연결(FakeLink)/연결 공장(FakeNet)은 NetFakes.cs (CoopRoomTests 와 공유)

        static readonly byte[] Hash = PasswordHash.Client("Test1234!");

        LoginFlow StartFlow(FakeNet net)
        {
            var flow = new LoginFlow(net.Connect);
            flow.Begin("user_00001", Hash, "127.0.0.1", 9100, 9101);
            return flow;
        }

        /// <summary>로그인 서버 인사 → 로그인 → 게임 서버 인사까지 진행시켜 게임 연결을 돌려준다.</summary>
        FakeLink ReachAuthenticating(FakeNet net, LoginFlow flow, string token = "tok")
        {
            net.Links[0].Hello(); flow.Tick(0.01f);
            net.Links[0].LoginOk(token); flow.Tick(0.01f);
            net.Links[1].Hello(); flow.Tick(0.01f);
            return net.Links[1];
        }

        string savePath;

        [SetUp]
        public void SetUp()
        {
            savePath = Path.Combine(Application.temporaryCachePath, $"test_net_save_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = savePath;
            SaveSystem.ResetCache();
            NetClient.OfflineChosen = false;
        }

        [TearDown]
        public void TearDown()
        {
            NetClient.DestroyInstance();
            NetClient.LinkFactory = (h, p) => new TcpLink(h, p);
            NetClient.OfflineChosen = false;
            LoginPanel.AutoShow = true;
            Audio.AudioManager.DestroyInstance();
            SaveSystem.Delete();
            SaveSystem.PathOverride = null;
            SaveSystem.ResetCache();
            if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
        }

        // ───────────────────────── 프레임 ─────────────────────────

        [Test]
        public void Frames_RoundTrip_HeaderIsBodyLengthLittleEndian()
        {
            var m = new MessageWrapper { LoginRequest = new LoginRequest { UserId = "user_00001", PasswordHash = ByteString.CopyFrom(Hash) } };
            var frame = NetFrames.Encode(m);
            int body = frame.Length - NetSettings.HeaderSize;
            Assert.AreEqual(body & 0xFF, frame[0]);
            Assert.AreEqual(body >> 8, frame[1]);

            var parser = new FrameParser();
            parser.Push(frame, 0, frame.Length);
            Assert.IsTrue(parser.TryPop(out var seg));
            var back = NetFrames.Decode(seg);
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.LoginRequest, back.PayloadCase);
            Assert.AreEqual("user_00001", back.LoginRequest.UserId);
            CollectionAssert.AreEqual(Hash, back.LoginRequest.PasswordHash.ToByteArray());
            Assert.IsFalse(parser.TryPop(out _));
            Assert.AreEqual(0, parser.Pending);
        }

        [Test]
        public void FrameParser_SplitsStuckTogetherFrames_AndJoinsSplitOnes()
        {
            var a = NetFrames.Encode(new MessageWrapper { ConnectedResponse = new ConnectedResponse { Index = 7 } });
            var b = NetFrames.Encode(new MessageWrapper { LoginResponse = new LoginResponse { Success = true, AuthToken = "abc" } });
            var c = NetFrames.Encode(new MessageWrapper { KeepAliveRequest = new KeepAliveRequest() });
            var all = new byte[a.Length + b.Length + c.Length];
            Buffer.BlockCopy(a, 0, all, 0, a.Length);
            Buffer.BlockCopy(b, 0, all, a.Length, b.Length);
            Buffer.BlockCopy(c, 0, all, a.Length + b.Length, c.Length);

            // 붙어서 한 번에
            var parser = new FrameParser();
            parser.Push(all, 0, all.Length);
            Assert.IsTrue(parser.TryPop(out var s1)); Assert.AreEqual(7, NetFrames.Decode(s1).ConnectedResponse.Index);
            Assert.IsTrue(parser.TryPop(out var s2)); Assert.AreEqual("abc", NetFrames.Decode(s2).LoginResponse.AuthToken);
            Assert.IsTrue(parser.TryPop(out var s3)); Assert.AreEqual(MessageWrapper.PayloadOneofCase.KeepAliveRequest, NetFrames.Decode(s3).PayloadCase);
            Assert.IsFalse(parser.TryPop(out _));

            // 한 바이트씩 쪼개서
            parser = new FrameParser();
            int popped = 0;
            for (int i = 0; i < all.Length; i++)
            {
                parser.Push(all, i, 1);
                while (parser.TryPop(out _)) popped++;
            }
            Assert.AreEqual(3, popped);
            Assert.AreEqual(0, parser.Pending);
        }

        [Test]
        public void FrameParser_RejectsZeroOrOversizedLength()
        {
            var parser = new FrameParser();
            parser.Push(new byte[] { 0, 0, 1 }, 0, 3);
            Assert.Throws<InvalidDataException>(() => parser.TryPop(out _), "길이 0");

            parser = new FrameParser();
            parser.Push(new byte[] { 0xFF, 0xFF }, 0, 2);
            Assert.Throws<InvalidDataException>(() => parser.TryPop(out _), "상한 초과");

            Assert.Throws<InvalidDataException>(() => NetFrames.Encode(new MessageWrapper()), "빈 메시지는 길이 0이라 보낼 수 없다");
        }

        [Test]
        public void PasswordHash_IsSha256OfUtf8_NeverPlaintext()
        {
            Assert.AreEqual(32, Hash.Length);
            Assert.AreEqual("0fadf52a4580cfebb99e61162139af3d3a6403c1d36b83e4962b721d1c8cbd0b", PasswordHash.ToHex(Hash));
            CollectionAssert.AreNotEqual(Hash, PasswordHash.Client("test1234!"));
        }

        // ───────────────────────── 로그인 흐름 ─────────────────────────

        [Test]
        public void LoginFlow_HappyPath_LoginServer_ThenGameServer_ThenKeepAlive()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            Assert.AreEqual(NetPhase.ConnectingLogin, flow.Phase);
            Assert.IsTrue(flow.IsBusy);
            Assert.AreEqual(9100, net.Links[0].Port);
            Assert.AreEqual("127.0.0.1", net.Links[0].Host);

            flow.Tick(0.1f);
            Assert.AreEqual(0, net.Links[0].Sent.Count, "서버의 첫 인사(ConnectedResponse)를 받기 전에는 보내지 않는다");

            net.Links[0].Hello();
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.LoggingIn, flow.Phase);
            Assert.AreEqual(1, net.Links[0].Sent.Count);
            var req = net.Links[0].Sent[0];
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.LoginRequest, req.PayloadCase);
            Assert.AreEqual("user_00001", req.LoginRequest.UserId);
            CollectionAssert.AreEqual(Hash, req.LoginRequest.PasswordHash.ToByteArray(), "SHA256 해시를 보낸다");

            net.Links[0].LoginOk("token-1");
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.ConnectingGame, flow.Phase);
            Assert.AreEqual("token-1", flow.Token);
            Assert.IsTrue(net.Links[0].Disposed, "로그인 소켓은 닫는다");
            Assert.AreEqual(2, net.Links.Count);
            Assert.AreEqual(9101, net.Links[1].Port);

            net.Links[1].Hello();
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.Authenticating, flow.Phase);
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.GameConnectRequest, net.Links[1].Sent[0].PayloadCase);
            Assert.AreEqual("token-1", net.Links[1].Sent[0].GameConnectRequest.AuthToken);

            net.Links[1].GameOk();
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.Online, flow.Phase);
            Assert.IsTrue(flow.IsOnline);
            Assert.IsFalse(flow.IsBusy);

            flow.Tick(2.9f);
            Assert.AreEqual(0, flow.KeepAlivesSent, "3초 전에는 아직");
            flow.Tick(0.2f);
            Assert.AreEqual(1, flow.KeepAlivesSent);
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.KeepAliveRequest, net.Links[1].Sent[1].PayloadCase);
            flow.Tick(3.0f);
            Assert.AreEqual(2, flow.KeepAlivesSent, "3초마다");
        }

        [Test]
        public void LoginFlow_StillReadsResponse_WhenServerClosesRightAfterIt()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            net.Links[0].Hello(); flow.Tick(0.1f);
            net.Links[0].LoginOk("t");
            net.Links[0].IsClosed = true;           // 응답 보내고 바로 끊은 서버
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.ConnectingGame, flow.Phase, "끊김보다 응답을 먼저 처리한다");
        }

        [Test]
        public void LoginFlow_AuthFailure_ReportsReason_AndDoesNotOpenGameServer()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            net.Links[0].Hello(); flow.Tick(0.1f);
            net.Links[0].LoginFail(1); flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.Failed, flow.Phase);
            Assert.AreEqual(1, flow.ErrorCode);
            Assert.AreEqual(LoginFlow.LoginErrorText(1), flow.Message);
            StringAssert.Contains("비밀번호", flow.Message);
            Assert.AreEqual(1, net.Links.Count, "게임 서버에는 가지 않는다");
            Assert.IsTrue(net.Links[0].Disposed);

            // 다른 코드도 사람이 읽을 문구가 있다
            foreach (var code in new[] { 2, 3, 4, 9 }) Assert.IsFalse(string.IsNullOrEmpty(LoginFlow.LoginErrorText(code)));
        }

        [Test]
        public void LoginFlow_InvalidToken_Fails_WithRelogInHint()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            var game = ReachAuthenticating(net, flow);
            game.GameFail(1); flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.Failed, flow.Phase);
            Assert.AreEqual(LoginFlow.GameErrorText(1), flow.Message);
            Assert.IsTrue(game.Disposed);
        }

        [Test]
        public void LoginFlow_ConnectError_And_Timeout_Fail()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            net.Links[0].Error = "연결 실패: 거부됨";
            net.Links[0].IsClosed = true;
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.Failed, flow.Phase);
            Assert.AreEqual("연결 실패: 거부됨", flow.Message);
            Assert.AreEqual(-1, flow.ErrorCode);

            net = new FakeNet { AutoConnect = false };
            flow = StartFlow(net);
            flow.Timeout = 2f;
            flow.Tick(1.9f);
            Assert.AreEqual(NetPhase.ConnectingLogin, flow.Phase);
            flow.Tick(0.2f);
            Assert.AreEqual(NetPhase.Failed, flow.Phase);
            StringAssert.Contains("로그인 서버", flow.Message);
        }

        [Test]
        public void LoginFlow_OnlineThenDropped_Fails_AndDisconnectGoesOffline()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            var game = ReachAuthenticating(net, flow);
            game.GameOk(); flow.Tick(0.1f);
            Assert.IsTrue(flow.IsOnline);

            game.IsClosed = true;
            flow.Tick(0.1f);
            Assert.AreEqual(NetPhase.Failed, flow.Phase);
            StringAssert.Contains("끊겼", flow.Message);

            flow.Disconnect();
            Assert.AreEqual(NetPhase.Offline, flow.Phase);
            Assert.AreEqual("", flow.Message);
        }

        [Test]
        public void LoginFlow_Begin_WhileOnline_DisconnectsOldLink()
        {
            var net = new FakeNet();
            var flow = StartFlow(net);
            var game = ReachAuthenticating(net, flow);
            game.GameOk(); flow.Tick(0.1f);

            flow.Begin("user_00002", Hash, "127.0.0.1", 9100, 9101);
            Assert.IsTrue(game.Disposed);
            Assert.AreEqual(NetPhase.ConnectingLogin, flow.Phase);
            Assert.AreEqual(3, net.Links.Count);
            Assert.AreEqual("", flow.Token);
        }

        // ───────────────────────── NetClient ─────────────────────────

        [UnityTest]
        public IEnumerator NetClient_DrivesFlowEveryFrame_AndSurvivesAsSingleton()
        {
            var net = new FakeNet();
            NetClient.LinkFactory = net.Connect;
            var phases = new List<NetPhase>();
            var client = NetClient.GetOrCreate();
            Assert.AreSame(client, NetClient.GetOrCreate());
            client.PhaseChanged += phases.Add;
            Assert.AreEqual(NetPhase.Offline, client.Phase);

            client.Login("user_00001", "Test1234!");
            Assert.AreEqual(NetPhase.ConnectingLogin, client.Phase);
            CollectionAssert.AreEqual(new[] { NetPhase.ConnectingLogin }, phases);

            net.Links[0].Hello(); yield return null;
            Assert.AreEqual(NetPhase.LoggingIn, client.Phase);
            net.Links[0].LoginOk("t"); yield return null;
            net.Links[1].Hello(); yield return null;
            net.Links[1].GameOk(); yield return null;
            Assert.IsTrue(client.IsOnline);
            Assert.AreEqual("user_00001", client.UserId);
            CollectionAssert.AreEqual(new[] { NetPhase.ConnectingLogin, NetPhase.LoggingIn, NetPhase.ConnectingGame, NetPhase.Authenticating, NetPhase.Online }, phases);

            client.Logout();
            Assert.AreEqual(NetPhase.Offline, client.Phase);
            Assert.IsTrue(net.Links[1].Disposed);
        }

        // ───────────────────────── 로그인 창 ─────────────────────────

        LoginPanel MakePanel()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return LoginPanel.Create(canvasGo.transform);
        }

        [UnityTest]
        public IEnumerator LoginPanel_RequiresIdAndPassword_BeforeConnecting()
        {
            var net = new FakeNet();
            NetClient.LinkFactory = net.Connect;
            var panel = MakePanel();
            yield return null;

            panel.Submit();
            StringAssert.Contains("아이디", panel.Status);
            Assert.IsNull(NetClient.Instance, "입력이 없으면 접속하지 않는다");

            panel.UserId = "user_00001";
            panel.Submit();
            StringAssert.Contains("비밀번호", panel.Status);
            Assert.IsNull(NetClient.Instance);

            panel.Password = "Test1234!";
            panel.Submit();
            Assert.IsNotNull(NetClient.Instance);
            Assert.IsTrue(panel.Busy);
            Assert.AreEqual(1, net.Links.Count);

            UnityEngine.Object.Destroy(panel.transform.parent.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoginPanel_ClosesOnSuccess_SavesUserId_AndShowsFailure()
        {
            var net = new FakeNet();
            NetClient.LinkFactory = net.Connect;
            var panel = MakePanel();
            var canvas = panel.transform.parent.gameObject;
            panel.UserId = "user_00001";
            panel.Password = "Test1234!";
            panel.Submit();
            yield return null;

            net.Links[0].Hello(); yield return null;
            net.Links[0].LoginFail(1); yield return null;
            Assert.IsFalse(panel.Busy, "실패하면 다시 입력할 수 있다");
            Assert.AreEqual(LoginFlow.LoginErrorText(1), panel.Status);

            panel.Submit();
            yield return null;
            net.Links[1].Hello(); yield return null;
            net.Links[1].LoginOk("t"); yield return null;
            net.Links[2].Hello(); yield return null;
            net.Links[2].GameOk(); yield return null;
            yield return null;
            Assert.IsTrue(NetClient.Instance.IsOnline);
            Assert.IsTrue(panel == null, "접속되면 창이 사라진다");
            Assert.AreEqual("user_00001", SaveSystem.Current.lastUserId, "아이디는 다음 실행을 위해 저장");

            UnityEngine.Object.Destroy(canvas);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoginPanel_Offline_SkipsLogin_ForThisRun()
        {
            var panel = MakePanel();
            var canvas = panel.transform.parent.gameObject;
            yield return null;
            panel.ContinueOffline();
            yield return null;
            Assert.IsTrue(panel == null);
            Assert.IsTrue(NetClient.OfflineChosen);
            Assert.IsFalse(LoginPanel.ShouldShow(), "오프라인을 골랐으면 타이틀로 돌아와도 다시 뜨지 않는다");

            UnityEngine.Object.Destroy(canvas);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoginPanel_ShouldShow_UnlessOnlineOrOffline()
        {
            Assert.IsTrue(LoginPanel.ShouldShow(), "처음에는 뜬다");

            var net = new FakeNet();
            NetClient.LinkFactory = net.Connect;
            var client = NetClient.GetOrCreate();
            client.Login("user_00001", "Test1234!");
            Assert.IsTrue(LoginPanel.ShouldShow(), "접속 중(아직 아님)이면 뜬다 (진행 상태를 이어서 보여 줌)");
            net.Links[0].Hello(); yield return null;
            net.Links[0].LoginOk("t"); yield return null;
            net.Links[1].Hello(); yield return null;
            net.Links[1].GameOk(); yield return null;
            Assert.IsTrue(client.IsOnline);
            Assert.IsFalse(LoginPanel.ShouldShow(), "접속돼 있으면 안 뜬다 (전투에서 타이틀로 돌아올 때)");

            net.Links[1].IsClosed = true; yield return null;
            Assert.AreEqual(NetPhase.Failed, client.Phase);
            Assert.IsTrue(LoginPanel.ShouldShow(), "끊기면 다시 뜬다");

            LoginPanel.AutoShow = false;
            Assert.IsFalse(LoginPanel.ShouldShow());
        }
    }
}
