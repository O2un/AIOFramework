using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace O2un.Core.Network.Tests
{
    // 게임 시작은 Host 만 낼 수 있어야 한다. Client 가 채운 값은 신뢰하지 않으므로 판정 근거는
    // 전송 계층이 확정한 SenderId 와 Host 가 등록한 NetworkId 뿐이다.
    public sealed class GameStartAuthorityTests
    {
        private const NetworkPacketId EVENT_ID = NetworkPacketId.GameStart;

        private const int HOST_PEER = 1;
        private const int GUEST_PEER = 2;

        private TestServerWorld _server;

        [SetUp]
        public void SetUp()
        {
            HostAuthority.Clear();

            _server = new TestServerWorld("O2unGameStartServerWorld");
            _server.AddConnection(HOST_PEER);
            _server.AddConnection(GUEST_PEER);

            // 런타임 설치자가 이미 붙여둔 Handler 가 있을 수 있다.
            _server.Processor.Unregister(EVENT_ID);
            _server.Processor.Register(new GameStartServerHandler());
        }

        [TearDown]
        public void TearDown()
        {
            HostAuthority.Clear();

            _server?.Dispose();
            _server = null;
        }

        [Test]
        public void Host_Broadcast_Reaches_Other_Peers()
        {
            HostAuthority.SetHostPeer(new P2PPeerId(HOST_PEER));

            Send(HOST_PEER);

            Assert.That(Sent().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { GUEST_PEER }));
        }

        [Test]
        public void Guest_Broadcast_Is_Blocked()
        {
            HostAuthority.SetHostPeer(new P2PPeerId(HOST_PEER));

            Send(GUEST_PEER);

            Assert.That(Sent(), Is.Empty);
            Assert.That(_server.Processor.BlockedCount(PacketBlockReason.HandlerRejected), Is.EqualTo(1));
        }

        [Test]
        public void Broadcast_Is_Blocked_When_No_Host_Is_Registered()
        {
            Send(HOST_PEER);

            Assert.That(Sent(), Is.Empty);
            Assert.That(_server.Processor.BlockedCount(PacketBlockReason.HandlerRejected), Is.EqualTo(1));
        }

        [Test]
        public void Host_Peer_Is_Stamped_On_Relayed_Payload()
        {
            HostAuthority.SetHostPeer(new P2PPeerId(HOST_PEER));

            Send(HOST_PEER);

            List<(RelayPacketRpc Packet, int TargetNetworkId)> sent = Sent();

            Assert.That(sent.Count, Is.EqualTo(1));

            GameStartPayload payload = NetworkJson.Deserialize<GameStartPayload>(
                NetcodePacketWire.ToManagedPayload(sent[0].Packet.Payload));

            Assert.That(payload.HostId, Is.EqualTo((ulong)HOST_PEER));
        }

        [Test]
        public void Echo_Packet_Type_Is_Not_Allowed()
        {
            HostAuthority.SetHostPeer(new P2PPeerId(HOST_PEER));

            _server.ReceiveFromClient(HOST_PEER, EVENT_ID, NetworkPacketType.Echo, 1, Payload());
            _server.Tick();

            Assert.That(Sent(), Is.Empty);
            Assert.That(_server.Processor.BlockedCount(PacketBlockReason.PacketTypeNotAllowed), Is.EqualTo(1));
        }

        private void Send(int senderNetworkId)
        {
            _server.ReceiveFromClient(senderNetworkId, EVENT_ID, NetworkPacketType.Broadcast, 0, Payload());
            _server.Tick();
        }

        private List<(RelayPacketRpc Packet, int TargetNetworkId)> Sent() => _server.DrainSent();

        private static byte[] Payload() => NetworkJson.SerializeToUtf8Bytes(new GameStartPayload());
    }
}
