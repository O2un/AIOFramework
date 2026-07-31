using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;

namespace O2un.Core.Network.Tests
{
    // RelayCheckP2PModule 은 SubSystem 계층에서 같은 Event ID·Packet Type 으로 Messenger 를 부르는 얇은 통로다.
    // 이 어셈블리는 그 계층을 참조하지 않으므로 Module 이 쓰는 계약을 그대로 두드려 검증한다.
    public sealed class RelayCheckVerticalSliceTests
    {
        private const NetworkPacketId EVENT_ID = NetworkPacketId.DebugRelayCheckPing;

        private static readonly TimeSpan ECHO_TIMEOUT = TimeSpan.FromSeconds(5);

        // A 는 Host 자신의 Client 다. 원격 Client 와 같은 RPC·Pipeline 경로를 밟는지가 이 검증의 핵심이다.
        private const int PEER_A = 1;
        private const int PEER_B = 2;
        private const int PEER_C = 3;

        private sealed class Participant : IDisposable
        {
            private readonly ReactiveProperty<int> _networkId;
            private readonly IDisposable _subscription;

            public Participant(int networkId)
            {
                NetworkId = networkId;
                ClientWorld = new TestClientWorld($"O2unRelayCheckClient{networkId}");
                _networkId = new ReactiveProperty<int>(networkId);
                Transport = new NetcodeP2PTransport(ClientWorld.World, _networkId);
                Messenger = new P2PMessenger(Transport);

                _subscription = Messenger.Observe<RelayCheckPayload>(EVENT_ID).Subscribe(Received.Add);
            }

            public int NetworkId { get; }
            public TestClientWorld ClientWorld { get; }
            public NetcodeP2PTransport Transport { get; }
            public P2PMessenger Messenger { get; }
            public List<RelayCheckPayload> Received { get; } = new();

            public UniTask<bool> BroadcastAsync(string message)
            {
                return Messenger.SendDataAsync(EVENT_ID, NetworkPacketType.Broadcast, new RelayCheckPayload { Message = message });
            }

            public UniTask<P2PPacketResult<RelayCheckPayload>> EchoAsync(string message)
            {
                return Messenger.SendDataAndWaitResultAsync<RelayCheckPayload, RelayCheckPayload>(
                    EVENT_ID,
                    NetworkPacketType.Echo,
                    new RelayCheckPayload { Message = message },
                    ECHO_TIMEOUT);
            }

            public void CloseSession()
            {
                Messenger.Dispose();
                Transport.Dispose();
            }

            public void Dispose()
            {
                _subscription.Dispose();
                CloseSession();
                _networkId.Dispose();
                ClientWorld.Dispose();
            }
        }

        private TestServerWorld _server;
        private Participant _a;
        private Participant _b;
        private Participant _c;

        private IEnumerable<Participant> Participants => new[] { _a, _b, _c };

        [TearDown]
        public void TearDown()
        {
            _a?.Dispose();
            _b?.Dispose();
            _c?.Dispose();
            _server?.Dispose();

            _a = null;
            _b = null;
            _c = null;
            _server = null;
        }

        private void CreateSession(Func<P2PPeerId, bool> canBroadcast = null)
        {
            _server = new TestServerWorld("O2unRelayCheckServerWorld");
            _server.AddConnection(PEER_A);
            _server.AddConnection(PEER_B);
            _server.AddConnection(PEER_C);

            // 런타임 설치자가 이미 붙여둔 Handler 가 있을 수 있다. 어떤 정책으로 판정하는지는 이 테스트가 정한다.
            _server.Processor.Unregister(EVENT_ID);
            _server.Processor.Register(new RelayCheckServerHandler(canBroadcast));

            _a = new Participant(PEER_A);
            _b = new Participant(PEER_B);
            _c = new Participant(PEER_C);
        }

        private List<RelayPacketRpc> PumpClientToServer()
        {
            var forwarded = new List<RelayPacketRpc>();

            foreach (Participant participant in Participants)
            {
                participant.ClientWorld.SendSystem.Update();

                foreach (RelayPacketRpc rpc in participant.ClientWorld.DrainOutbound())
                {
                    forwarded.Add(rpc);
                    _server.ReceiveFromClient(participant.NetworkId, rpc);
                }
            }

            return forwarded;
        }

        private List<(RelayPacketRpc Packet, int TargetNetworkId)> TickServerAndDeliver()
        {
            _server.Tick();

            List<(RelayPacketRpc Packet, int TargetNetworkId)> sent = _server.DrainSent();

            foreach ((RelayPacketRpc packet, int targetNetworkId) in sent)
            {
                Participants.Single(participant => targetNetworkId == participant.NetworkId).ClientWorld.DeliverInbound(packet);
            }

            foreach (Participant participant in Participants)
            {
                participant.ClientWorld.PumpReceive();
            }

            return sent;
        }

        private static T Complete<T>(UniTask<T> task)
        {
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return task.GetAwaiter().GetResult();
        }

        private static void SendRaw(Participant participant, NetworkPacketId eventId, NetworkPacketType packetType, ulong sequence, byte[] payload)
        {
            // Messenger 는 정의된 Event·Packet Type 만 통과시킨다. Host 의 fail-closed 를 보려면 그 아래에서 넣어야 한다.
            participant.Transport.SendAsync(new P2PRequestPacket(eventId, packetType, sequence, payload)).Forget();
        }

        [Test]
        public void Broadcast_From_Host_Client_Reaches_Only_Other_Peers()
        {
            CreateSession();

            Assert.That(Complete(_a.BroadcastAsync("relay-check")), Is.True);

            PumpClientToServer();

            // Server 가 돌기 전에는 아무도 받지 않는다. Host Client 도 Handler 를 직접 부르지 않는다는 뜻이다.
            Assert.That(Participants.Sum(participant => participant.Received.Count), Is.Zero);

            List<(RelayPacketRpc Packet, int TargetNetworkId)> sent = TickServerAndDeliver();

            Assert.That(sent.Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { PEER_B, PEER_C }));
            Assert.That(_a.Received, Is.Empty);
            Assert.That(_b.Received.Count, Is.EqualTo(1));
            Assert.That(_c.Received.Count, Is.EqualTo(1));
            Assert.That(_b.Received[0].Message, Is.EqualTo("relay-check"));
            Assert.That(_b.Received[0].SenderId, Is.EqualTo((ulong)PEER_A));
            Assert.That(_c.Received[0].SenderId, Is.EqualTo((ulong)PEER_A));
        }

        [Test]
        public void Echo_Completes_Sender_Request_Without_Routing_To_Subscribers()
        {
            CreateSession();

            UniTask<P2PPacketResult<RelayCheckPayload>> echo = _a.EchoAsync("echo-me");
            Assert.That(echo.Status, Is.EqualTo(UniTaskStatus.Pending));

            List<RelayPacketRpc> forwarded = PumpClientToServer();
            List<(RelayPacketRpc Packet, int TargetNetworkId)> sent = TickServerAndDeliver();

            Assert.That(forwarded.Count, Is.EqualTo(1));
            Assert.That(forwarded[0].Sequence, Is.Not.EqualTo(0UL));
            Assert.That(sent.Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { PEER_A }));
            Assert.That(sent[0].Packet.Sequence, Is.EqualTo(forwarded[0].Sequence));
            Assert.That(sent[0].Packet.EventId, Is.EqualTo((int)EVENT_ID));

            P2PPacketResult<RelayCheckPayload> result = Complete(echo);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Message, Is.EqualTo("echo-me"));
            Assert.That(result.Value.SenderId, Is.EqualTo((ulong)PEER_A));

            // 응답은 대기 요청을 완료할 뿐 알림 구독으로 다시 흘러가지 않는다.
            Assert.That(Participants.Sum(participant => participant.Received.Count), Is.Zero);
        }

        [Test]
        public void Sender_Is_Confirmed_By_Connection_Not_By_Payload()
        {
            CreateSession();

            Complete(_a.Messenger.SendDataAsync(
                EVENT_ID,
                NetworkPacketType.Broadcast,
                new RelayCheckPayload { SenderId = PEER_C, Message = "forged" }));

            PumpClientToServer();
            TickServerAndDeliver();

            Assert.That(_b.Received.Count, Is.EqualTo(1));
            Assert.That(_b.Received[0].SenderId, Is.EqualTo((ulong)PEER_A));
        }

        [Test]
        public void Unauthorized_Broadcast_Reaches_Nobody()
        {
            CreateSession(peerId => new P2PPeerId(PEER_A) != peerId);

            Complete(_a.BroadcastAsync("denied"));
            PumpClientToServer();

            Assert.That(TickServerAndDeliver(), Is.Empty);
            Assert.That(Participants.Sum(participant => participant.Received.Count), Is.Zero);

            // 권한 판정은 Sender 별이라 남은 Peer 의 Broadcast 는 그대로 지나가야 한다.
            Complete(_b.BroadcastAsync("allowed"));
            PumpClientToServer();

            Assert.That(TickServerAndDeliver().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { PEER_A, PEER_C }));
        }

        [Test]
        public void Invalid_Payload_Unknown_Type_And_Undefined_Event_Reach_Nobody()
        {
            CreateSession();

            SendRaw(_a, EVENT_ID, NetworkPacketType.Broadcast, 0, new byte[] { 0x7B, 0x7B, 0x7B });
            SendRaw(_a, EVENT_ID, NetworkPacketType.Broadcast, 0, NetworkJson.SerializeToUtf8Bytes(new RelayCheckPayload { Message = null }));
            SendRaw(_a, EVENT_ID, (NetworkPacketType)77, 0, NetworkJson.SerializeToUtf8Bytes(new RelayCheckPayload { Message = "unknown-type" }));
            SendRaw(_a, (NetworkPacketId)123456, NetworkPacketType.Broadcast, 0, NetworkJson.SerializeToUtf8Bytes(new RelayCheckPayload { Message = "undefined-event" }));
            SendRaw(_a, EVENT_ID, NetworkPacketType.Echo, 0, NetworkJson.SerializeToUtf8Bytes(new RelayCheckPayload { Message = "echo-without-sequence" }));

            PumpClientToServer();

            Assert.That(TickServerAndDeliver(), Is.Empty);
            Assert.That(Participants.Sum(participant => participant.Received.Count), Is.Zero);
        }

        [Test]
        public void Session_Shutdown_Clears_Pending_Request_And_Peer_State()
        {
            CreateSession();

            UniTask<P2PPacketResult<RelayCheckPayload>> pending = _a.EchoAsync("bye");
            PumpClientToServer();

            foreach (Participant participant in Participants)
            {
                participant.CloseSession();
            }

            P2PPacketResult<RelayCheckPayload> result = Complete(pending);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Reason, Is.EqualTo(P2PPacketReasons.SESSION_CLOSED));
            Assert.That(_a.Transport.PeerCount, Is.Zero);

            // 정적 Bridge 구독이 남아 있으면 종료된 세션의 Transport 로 패킷이 계속 들어온다.
            _server.Tick();

            foreach ((RelayPacketRpc packet, int _) in _server.DrainSent())
            {
                _a.ClientWorld.DeliverInbound(packet);
            }

            Assert.DoesNotThrow(() => _a.ClientWorld.PumpReceive());
            Assert.That(Participants.Sum(participant => participant.Received.Count), Is.Zero);
        }
    }
}
