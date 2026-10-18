using System.Collections;
using System.Collections.Generic;
using Messages;
using NUnit.Framework;
using Samkuk.Coop;
using Samkuk.Net;
using Samkuk.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Samkuk.Tests
{
    /// <summary>
    /// 협동 방(Step 15-3): 방 상태 모델, 방 흐름 상태 기계(CoopFlow), 로그인 흐름의 메시지 전달, NetClient/CoopSession 연결, 방 창.
    /// 서버 없이 가짜 전송(Recorder)과 가짜 연결(FakeLink)로 서버 응답을 흉내 낸다.
    /// </summary>
    public class CoopRoomTests
    {
        // ───────────────────────── 도우미 ─────────────────────────

        class Recorder
        {
            public readonly List<MessageWrapper> Sent = new List<MessageWrapper>();
            public bool Online = true;
            public bool Send(MessageWrapper m)
            {
                if (!Online) return false;
                Sent.Add(m);
                return true;
            }
            public MessageWrapper Last => Sent[Sent.Count - 1];
        }

        static RoomInfo Room(string code, int host, string stage, params (string user, int slot, string hero, bool ready)[] members)
        {
            var r = new RoomInfo { RoomCode = code, HostSlot = host, StageId = stage ?? "" };
            foreach (var m in members)
                r.Members.Add(new RoomMember { UserId = m.user, Slot = m.slot, HeroId = m.hero ?? "", Ready = m.ready, DisplayName = m.user + "님" });
            return r;
        }

        static MessageWrapper CreateOk(RoomInfo r) => new MessageWrapper { RoomCreateResponse = new RoomCreateResponse { Success = true, Room = r } };
        static MessageWrapper CreateFail(int code) => new MessageWrapper { RoomCreateResponse = new RoomCreateResponse { Success = false, ErrorCode = code } };
        static MessageWrapper JoinOk(RoomInfo r) => new MessageWrapper { RoomJoinResponse = new RoomJoinResponse { Success = true, Room = r } };
        static MessageWrapper JoinFail(int code) => new MessageWrapper { RoomJoinResponse = new RoomJoinResponse { Success = false, ErrorCode = code } };
        static MessageWrapper State(RoomInfo r) => new MessageWrapper { RoomStateNoti = new RoomStateNoti { Room = r } };
        static MessageWrapper StartFail(int code) => new MessageWrapper { RoomStartResponse = new RoomStartResponse { Success = false, ErrorCode = code } };
        static MessageWrapper BattleStart(int mySlot) => new MessageWrapper { BattleStartNoti = new BattleStartNoti { StageId = "Stage_YellowTurban", MySlot = mySlot, TickRate = 20, SnapshotRate = 10, CountdownMs = 3000 } };
        static MessageWrapper BattleEnd(bool cleared) => new MessageWrapper { BattleEndNoti = new BattleEndNoti { Cleared = cleared } };

        // ───────────────────────── RoomModel ─────────────────────────

        [Test]
        public void RoomModel_Apply_IdentifiesMeHostAndStartRule()
        {
            var room = new RoomModel("me");
            Assert.IsFalse(room.InRoom);
            Assert.AreEqual("방에 있지 않습니다", room.StartBlockReason);

            room.Apply(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false), ("friend", 1, "Hero_GuanYu", false)));
            Assert.IsTrue(room.InRoom);
            Assert.AreEqual(0, room.MySlot);
            Assert.IsTrue(room.IsHost);
            Assert.AreEqual("Hero_LiuBei", room.MyHeroId);
            Assert.IsFalse(room.CanStart);
            Assert.AreEqual("아직 준비하지 않은 사람이 있습니다", room.StartBlockReason);

            room.Apply(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false), ("friend", 1, "Hero_GuanYu", true)));
            Assert.IsTrue(room.CanStart, "방장은 준비 없이, 나머지가 준비하면 시작할 수 있다");

            room.Apply(Room("ABC123", 1, "", ("me", 0, "Hero_LiuBei", true), ("friend", 1, "Hero_GuanYu", false)));
            Assert.IsFalse(room.IsHost);
            Assert.IsTrue(room.MyReady);
            Assert.AreEqual("방장만 시작할 수 있습니다", room.StartBlockReason);

            room.Apply(Room("ABC123", 0, "", ("me", 0, "Hero_LiuBei", false)));
            Assert.AreEqual("스테이지가 정해지지 않았습니다", room.StartBlockReason, "혼자여도 스테이지가 없으면 못 시작");

            room.Apply(null);
            Assert.IsFalse(room.InRoom);
            Assert.AreEqual(-1, room.MySlot);
        }

        [Test]
        public void RoomModel_CodeNormalizeAndValidate()
        {
            Assert.AreEqual("ABC123", RoomModel.NormalizeCode("  abc123 "));
            Assert.IsTrue(RoomModel.IsValidCode("ABC123"));
            Assert.IsFalse(RoomModel.IsValidCode("ABC12"));
            Assert.IsFalse(RoomModel.IsValidCode("ABC-12"));
            Assert.IsFalse(RoomModel.IsValidCode(null));
            Assert.AreEqual("방이 가득 찼습니다 (최대 4명)", RoomModel.JoinErrorText(2));
            Assert.AreEqual("방장만 시작할 수 있습니다", RoomModel.StartErrorText(1));
            Assert.AreEqual("이미 방에 들어가 있습니다", RoomModel.CreateErrorText(1));
        }

        // ───────────────────────── CoopFlow ─────────────────────────

        [Test]
        public void CoopFlow_Create_SendsRequest_ThenInRoom_AndHostSetsDefaultStage()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me");
            int changed = 0;
            flow.Changed += () => changed++;

            Assert.IsFalse(flow.Create(""), "장수 없이 못 만든다");
            Assert.AreEqual("장수를 고르세요", flow.Message);
            Assert.AreEqual(0, rec.Sent.Count);

            Assert.IsTrue(flow.Create("Hero_LiuBei"));
            Assert.AreEqual(CoopPhase.Creating, flow.Phase);
            Assert.IsTrue(flow.IsBusy);
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomCreateRequest, rec.Last.PayloadCase);
            Assert.AreEqual("Hero_LiuBei", rec.Last.RoomCreateRequest.HeroId);
            Assert.IsFalse(flow.Join("ABC123", "Hero_LiuBei"), "응답 대기 중엔 다른 요청을 막는다");
            Assert.AreEqual("응답을 기다리는 중입니다", flow.Message);

            Assert.IsTrue(flow.Handle(CreateOk(Room("QWE789", 0, "", ("me", 0, "Hero_LiuBei", false)))));
            Assert.AreEqual(CoopPhase.InRoom, flow.Phase);
            Assert.AreEqual("QWE789", flow.Room.Code);
            Assert.IsTrue(flow.Room.IsHost);
            StringAssert.Contains("QWE789", flow.Message);
            // 서버의 스테이지가 비어 있으면 방장이 기본 스테이지를 정한다
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomSetStageRequest, rec.Last.PayloadCase);
            Assert.AreEqual(CoopSettings.DefaultStageId, rec.Last.RoomSetStageRequest.StageId);
            Assert.Greater(changed, 0);

            Assert.IsFalse(flow.Create("Hero_LiuBei"));
            Assert.AreEqual("이미 방에 있습니다", flow.Message);
        }

        [Test]
        public void CoopFlow_Create_Failure_ShowsReason_AndReturnsToNone()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me");
            flow.Create("Hero_LiuBei");
            flow.Handle(CreateFail(3));
            Assert.AreEqual(CoopPhase.None, flow.Phase);
            Assert.AreEqual(RoomModel.CreateErrorText(3), flow.Message);
            Assert.IsFalse(flow.InRoom);
        }

        [Test]
        public void CoopFlow_Join_ValidatesCode_AndShowsServerReason()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me");
            Assert.IsFalse(flow.Join("abc", "Hero_LiuBei"));
            StringAssert.Contains("6자리", flow.Message);
            Assert.AreEqual(0, rec.Sent.Count, "틀린 코드는 보내지 않는다");

            Assert.IsTrue(flow.Join(" abc123 ", "Hero_LiuBei"));
            Assert.AreEqual("ABC123", rec.Last.RoomJoinRequest.RoomCode, "대문자로 정리해서 보낸다");
            Assert.AreEqual(CoopPhase.Joining, flow.Phase);

            flow.Handle(JoinFail(2));
            Assert.AreEqual(CoopPhase.None, flow.Phase);
            Assert.AreEqual("방이 가득 찼습니다 (최대 4명)", flow.Message);
        }

        [Test]
        public void CoopFlow_Member_ReadyThenBattleStartThenEnd()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me");
            flow.Join("ABC123", "Hero_LiuBei");
            flow.Handle(JoinOk(Room("ABC123", 0, "Stage_YellowTurban", ("host", 0, "Hero_GuanYu", false), ("me", 1, "Hero_LiuBei", false))));
            Assert.AreEqual(CoopPhase.InRoom, flow.Phase);
            Assert.AreEqual(1, flow.Room.MySlot);
            Assert.IsFalse(flow.Room.IsHost);

            Assert.IsFalse(flow.Start());
            Assert.AreEqual("방장만 시작할 수 있습니다", flow.Message);
            Assert.AreNotEqual(MessageWrapper.PayloadOneofCase.RoomStartRequest, rec.Last.PayloadCase);

            Assert.IsTrue(flow.SetReady(true));
            Assert.IsTrue(rec.Last.RoomReadyRequest.Ready);
            Assert.IsFalse(flow.Room.MyReady, "준비 상태의 원본은 서버: 알림이 와야 바뀐다");
            flow.Handle(State(Room("ABC123", 0, "Stage_YellowTurban", ("host", 0, "Hero_GuanYu", false), ("me", 1, "Hero_LiuBei", true))));
            Assert.IsTrue(flow.Room.MyReady);

            Assert.IsTrue(flow.SetHero("Hero_ZhangFei"));
            Assert.AreEqual("Hero_ZhangFei", rec.Last.RoomSetHeroRequest.HeroId);
            Assert.IsFalse(flow.SetStage("Stage_X"), "방장이 아니면 스테이지를 못 바꾼다");

            Assert.IsTrue(flow.Handle(BattleStart(1)));
            Assert.AreEqual(CoopPhase.Battle, flow.Phase);
            Assert.AreEqual(1, flow.BattleStart.MySlot);
            Assert.AreEqual(3000, flow.BattleStart.CountdownMs);

            Assert.IsTrue(flow.Handle(BattleEnd(true)));
            Assert.AreEqual(CoopPhase.InRoom, flow.Phase, "전투가 끝나면 같은 방의 대기 상태로");
            Assert.AreEqual("전투 승리", flow.Message);
            Assert.IsTrue(flow.BattleEnd.Cleared);

            Assert.IsFalse(flow.Handle(new MessageWrapper { KeepAliveNoti = new KeepAliveNoti() }), "방 메시지가 아니면 false");
        }

        [Test]
        public void CoopFlow_Host_StartBlockedUntilReady_AndServerRejectKeepsRoom()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me");
            flow.Create("Hero_LiuBei");
            flow.Handle(CreateOk(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false), ("friend", 1, "Hero_GuanYu", false))));
            Assert.IsFalse(flow.Start());
            Assert.AreEqual("아직 준비하지 않은 사람이 있습니다", flow.Message);

            flow.Handle(State(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false), ("friend", 1, "Hero_GuanYu", true))));
            Assert.IsTrue(flow.Start());
            Assert.AreEqual(CoopPhase.Starting, flow.Phase);
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomStartRequest, rec.Last.PayloadCase);

            flow.Handle(StartFail(2));
            Assert.AreEqual(CoopPhase.InRoom, flow.Phase, "거절돼도 방에는 남는다");
            Assert.AreEqual(RoomModel.StartErrorText(2), flow.Message);

            flow.Start();
            flow.Handle(BattleStart(0));
            Assert.AreEqual(CoopPhase.Battle, flow.Phase);
        }

        [Test]
        public void CoopFlow_Timeout_RevertsPhase_AndLeaveClearsImmediately()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me") { Timeout = 2f };
            flow.Create("Hero_LiuBei");
            flow.Tick(1.5f);
            Assert.AreEqual(CoopPhase.Creating, flow.Phase);
            flow.Tick(1f);
            Assert.AreEqual(CoopPhase.None, flow.Phase);
            Assert.AreEqual("서버가 응답하지 않습니다", flow.Message);

            flow.Create("Hero_LiuBei");
            flow.Handle(CreateOk(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false))));
            flow.Start();
            flow.Tick(3f);
            Assert.AreEqual(CoopPhase.InRoom, flow.Phase, "시작 응답이 없으면 방으로 되돌아간다");

            Assert.IsTrue(flow.Leave());
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomLeaveRequest, rec.Last.PayloadCase);
            Assert.IsFalse(flow.InRoom, "응답을 기다리지 않고 바로 나간 것으로 본다");
            Assert.AreEqual(CoopPhase.None, flow.Phase);
            Assert.IsFalse(flow.Leave());

            flow.Join("ABC123", "Hero_LiuBei");
            flow.Handle(JoinOk(Room("ABC123", 0, "Stage_YellowTurban", ("host", 0, "Hero_GuanYu", false), ("me", 1, "Hero_LiuBei", false))));
            flow.Handle(State(null));
            Assert.AreEqual(CoopPhase.None, flow.Phase);
            Assert.AreEqual("방이 닫혔습니다", flow.Message);
        }

        [Test]
        public void CoopFlow_WhenNotOnline_RequestFailsWithMessage()
        {
            var rec = new Recorder { Online = false };
            var flow = new CoopFlow(rec.Send, "me");
            Assert.IsFalse(flow.Create("Hero_LiuBei"));
            Assert.AreEqual(CoopPhase.None, flow.Phase);
            Assert.AreEqual("서버에 접속돼 있지 않습니다", flow.Message);
        }

        // ───────────────────────── LoginFlow 전달 ─────────────────────────

        [Test]
        public void LoginFlow_RoutesUnhandledMessages_OnlyWhenOnline_AndSends()
        {
            var net = new FakeNet();
            var flow = new LoginFlow(net.Connect);
            var passed = new List<MessageWrapper>();
            flow.Unhandled += passed.Add;
            flow.Begin("me", new byte[32], "h", 1, 2);
            Assert.IsFalse(flow.Send(new MessageWrapper { RoomLeaveRequest = new RoomLeaveRequest() }), "접속 완료 전엔 못 보낸다");

            net.Links[0].Hello();
            net.Links[0].Inbox.Enqueue(State(Room("ABC123", 0, "", ("me", 0, "", false)))); // 로그인 중에 온 방 메시지는 버린다
            net.Links[0].LoginOk("t");
            flow.Tick(0.1f);
            net.Links[1].Hello();
            net.Links[1].GameOk();
            flow.Tick(0.1f);
            Assert.IsTrue(flow.IsOnline);
            Assert.AreEqual(0, passed.Count);

            net.Links[1].Inbox.Enqueue(State(Room("ABC123", 0, "", ("me", 0, "", false))));
            net.Links[1].Inbox.Enqueue(new MessageWrapper { KeepAliveNoti = new KeepAliveNoti() });
            flow.Tick(0.1f);
            Assert.AreEqual(2, passed.Count);
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomStateNoti, passed[0].PayloadCase);
            Assert.IsTrue(flow.IsOnline, "모르는 메시지로 흐름이 흔들리지 않는다");

            Assert.IsTrue(flow.Send(new MessageWrapper { RoomLeaveRequest = new RoomLeaveRequest() }));
            Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomLeaveRequest, net.Links[1].Sent[net.Links[1].Sent.Count - 1].PayloadCase);
        }

        // ───────────────────────── NetClient + CoopSession ─────────────────────────

        [UnityTest]
        public IEnumerator NetClient_CoopSession_RoutesMessages_AndResetsOnLogout()
        {
            NetClient.DestroyInstance();
            CoopSession.Detach();
            var net = new FakeNet();
            NetClient.LinkFactory = net.Connect;
            var client = NetClient.GetOrCreate();
            try
            {
                client.Login("me", "pw");
                net.Links[0].Hello(); yield return null;
                net.Links[0].LoginOk("t"); yield return null;
                net.Links[1].Hello(); yield return null;
                net.Links[1].GameOk(); yield return null;
                Assert.IsTrue(client.IsOnline);

                var flow = CoopSession.GetOrCreate(client);
                Assert.AreSame(flow, CoopSession.GetOrCreate(client), "같은 연결이면 같은 흐름");
                Assert.AreEqual("me", flow.UserId);
                Assert.IsTrue(flow.Create("Hero_LiuBei"));
                Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomCreateRequest, net.Links[1].Sent[net.Links[1].Sent.Count - 1].PayloadCase);

                net.Links[1].Inbox.Enqueue(CreateOk(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false))));
                yield return null;
                Assert.AreEqual(CoopPhase.InRoom, flow.Phase);
                Assert.IsTrue(flow.Room.IsHost);

                client.Logout();
                yield return null;
                Assert.AreEqual(CoopPhase.None, flow.Phase, "접속을 끊으면 방도 없다");
                StringAssert.Contains("방에서 나왔습니다", flow.Message);
            }
            finally
            {
                CoopSession.Detach();
                NetClient.DestroyInstance();
                NetClient.LinkFactory = (h, p) => new TcpLink(h, p);
            }
        }

        // ───────────────────────── RoomPanel ─────────────────────────

        [UnityTest]
        public IEnumerator RoomPanel_SwitchesLobbyAndRoomViews_AndValidatesInput()
        {
            var rec = new Recorder();
            var flow = new CoopFlow(rec.Send, "me");
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var panel = RoomPanel.Create(canvas.transform, flow);
                Assert.IsFalse(panel.IsRoomViewVisible);

                panel.SelectHero("Hero_LiuBei"); // 테스트에는 카탈로그가 없을 수 있어 id 로 직접 고른다
                panel.Code = "ab";
                panel.JoinRoom();
                StringAssert.Contains("6자리", panel.Status);
                Assert.AreEqual(0, rec.Sent.Count);

                panel.CreateRoom();
                Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomCreateRequest, rec.Last.PayloadCase);
                Assert.AreEqual("Hero_LiuBei", rec.Last.RoomCreateRequest.HeroId);
                flow.Handle(CreateOk(Room("ABC123", 0, "Stage_YellowTurban", ("me", 0, "Hero_LiuBei", false), ("friend", 1, "Hero_GuanYu", true))));
                yield return null;
                Assert.IsTrue(panel.IsRoomViewVisible);
                StringAssert.Contains("ABC123", panel.Status);

                panel.SelectHero("Hero_ZhangFei"); // 방 안에서 고르면 서버에 바꿔 달라고 한다
                Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomSetHeroRequest, rec.Last.PayloadCase);

                panel.StartBattle();
                Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomStartRequest, rec.Last.PayloadCase);
                flow.Handle(BattleStart(0));
                yield return null;
                Assert.AreEqual(CoopPhase.Battle, flow.Phase);
                panel.Close();
                yield return null;
                Assert.IsNotNull(canvas.transform.Find(RoomPanel.RootName), "전투 시작 뒤에는 닫지 않는다");

                flow.Handle(BattleEnd(false));
                panel.Close();
                yield return null;
                Assert.IsNull(canvas.transform.Find(RoomPanel.RootName));
                Assert.AreEqual(MessageWrapper.PayloadOneofCase.RoomLeaveRequest, rec.Last.PayloadCase, "닫으면 방을 나간다");
            }
            finally
            {
                Object.Destroy(canvas);
            }
        }
    }
}
