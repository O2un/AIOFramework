using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using R3;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network.Tests
{
    public sealed class NetcodeClientTransportTests
    {
        private const int LOCAL_NETWORK_ID = 3;
        private const int REMOTE_NETWORK_ID = 7;

        private sealed class TestClientWorld : IDisposable
        {
            public World World { get; }
            public ClientSendPacketSystem SendSystem { get; }
            public ClientReceivePacketSystem ReceiveSystem { get; }
            public NetcodePacketDrainSystem DrainSystem { get; }

            public TestClientWorld()
            {
                World = new World("O2unPacketTestWorld");
                SendSystem = World.CreateSystemManaged<ClientSendPacketSystem>();
                ReceiveSystem = World.CreateSystemManaged<ClientReceivePacketSystem>();
                DrainSystem = World.CreateSystemManaged<NetcodePacketDrainSystem>();
            }

            public void Dispose()
            {
                World.Dispose();
            }

            public int CountOf<T>() where T : unmanaged, IComponentData
            {
                using EntityQuery query = World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
                return query.CalculateEntityCount();
            }

            public RelayPacketRpc SingleRpc()
            {
                using EntityQuery query = World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<RelayPacketRpc>());
                using NativeArray<RelayPacketRpc> packets = query.ToComponentDataArray<RelayPacketRpc>(Allocator.Temp);

                Assert.That(packets.Length, Is.EqualTo(1));
                return packets[0];
            }

            // Host 파이프라인은 03 책임이라, 여기서는 확정된 RPC 를 그대로 되돌려 Client 수신 경로만 검증한다.
            public void DeliverInbound(RelayPacketRpc packet, int senderNetworkId)
            {
                packet.SenderNetworkId = senderNetworkId;

                var entity = World.EntityManager.CreateEntity();
                World.EntityManager.AddComponentData(entity, packet);
                World.EntityManager.AddComponentData(entity, new ReceiveRpcCommandRequest());
            }

            public void PumpReceive()
            {
                ReceiveSystem.Update();
                DrainSystem.Update();
            }
        }

        private static byte[] CreatePayload(int size)
        {
            var payload = new byte[size];

            for (var i = 0; i < size; ++i)
            {
                payload[i] = (byte)(i % 251);
            }

            return payload;
        }

        private static NetcodeP2PTransport CreateTransport(TestClientWorld clientWorld)
        {
            return CreateTransport(clientWorld, new ReactiveProperty<int>(LOCAL_NETWORK_ID));
        }

        private static NetcodeP2PTransport CreateTransport(TestClientWorld clientWorld, ReactiveProperty<int> networkId)
        {
            return new NetcodeP2PTransport(clientWorld.World, networkId);
        }

        [Test]
        public void PayloadLimitMatchesFixedListCapacity()
        {
            Assert.That(NetcodePacketWire.MAX_PAYLOAD_BYTES, Is.EqualTo(510));
            Assert.That(NetcodePacketWire.MAX_PAYLOAD_BYTES, Is.EqualTo(default(FixedList512Bytes<byte>).Capacity));
        }

        [Test]
        public async Task MaxSizePacketRoundTripsThroughRpcAndDrain()
        {
            using var clientWorld = new TestClientWorld();
            using NetcodeP2PTransport transport = CreateTransport(clientWorld);
            var received = new List<P2PInboundPacket>();
            using IDisposable subscription = transport.PacketReceived.Subscribe(received.Add);
            byte[] payload = CreatePayload(NetcodePacketWire.MAX_PAYLOAD_BYTES);

            bool isSent = await transport.SendAsync(new P2PRequestPacket(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Broadcast,
                42,
                payload));
            clientWorld.SendSystem.Update();
            RelayPacketRpc rpc = clientWorld.SingleRpc();
            clientWorld.DeliverInbound(rpc, REMOTE_NETWORK_ID);
            clientWorld.PumpReceive();

            Assert.That(isSent, Is.True);
            Assert.That(rpc.EventId, Is.EqualTo((int)NetworkPacketId.DebugRelayCheckPing));
            Assert.That(rpc.PacketType, Is.EqualTo((int)NetworkPacketType.Broadcast));
            Assert.That(rpc.SenderNetworkId, Is.EqualTo(LOCAL_NETWORK_ID));
            Assert.That(rpc.Sequence, Is.EqualTo(42UL));
            Assert.That(rpc.Payload.Length, Is.EqualTo(payload.Length));
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].EventId, Is.EqualTo(NetworkPacketId.DebugRelayCheckPing));
            Assert.That(received[0].PacketType, Is.EqualTo(NetworkPacketType.Broadcast));
            Assert.That(received[0].Sequence, Is.EqualTo(42UL));
            Assert.That(received[0].SenderId, Is.EqualTo(new P2PPeerId(REMOTE_NETWORK_ID)));
            Assert.That(received[0].Payload.ToArray(), Is.EqualTo(payload));
            Assert.That(transport.IsConnected.CurrentValue, Is.True);
        }

        // 실제 순서는 연결로 Network ID 가 먼저 확정되고 그 뒤에 세션이 Transport 를 만드는 것이다.
        // 사건이 아니라 상태를 구독하므로 구독 시점에 이미 확정된 값이 그대로 들어와야 한다.
        [Test]
        public async Task TransportAdoptsNetworkIdAssignedBeforeItWasCreated()
        {
            using var clientWorld = new TestClientWorld();
            using var networkId = new ReactiveProperty<int>(LOCAL_NETWORK_ID);
            using NetcodeP2PTransport transport = CreateTransport(clientWorld, networkId);

            Assert.That(transport.IsConnected.CurrentValue, Is.True);
            Assert.That(transport.PeerCount, Is.EqualTo(1));

            await transport.SendAsync(new P2PRequestPacket(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Broadcast,
                1,
                CreatePayload(4)));
            clientWorld.SendSystem.Update();

            Assert.That(clientWorld.SingleRpc().SenderNetworkId, Is.EqualTo(LOCAL_NETWORK_ID));
        }

        [Test]
        public void OversizedPayloadIsRejectedWithoutCreatingRequestOrRpc()
        {
            using var clientWorld = new TestClientWorld();
            using NetcodeP2PTransport transport = CreateTransport(clientWorld);
            byte[] payload = CreatePayload(NetcodePacketWire.MAX_PAYLOAD_BYTES + 1);

            Assert.CatchAsync<ArgumentOutOfRangeException>(async () => await transport.SendAsync(new P2PRequestPacket(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                1,
                payload)));

            clientWorld.SendSystem.Update();

            Assert.That(clientWorld.CountOf<ClientPacketSendRequest>(), Is.Zero);
            Assert.That(clientWorld.CountOf<RelayPacketRpc>(), Is.Zero);
        }

        [Test]
        public async Task ProcessedRequestAndRpcAreNotReprocessed()
        {
            using var clientWorld = new TestClientWorld();
            using NetcodeP2PTransport transport = CreateTransport(clientWorld);
            var received = new List<P2PInboundPacket>();
            using IDisposable subscription = transport.PacketReceived.Subscribe(received.Add);

            await transport.SendAsync(new P2PRequestPacket(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                5,
                CreatePayload(8)));
            clientWorld.SendSystem.Update();
            RelayPacketRpc rpc = clientWorld.SingleRpc();
            clientWorld.SendSystem.Update();
            clientWorld.DeliverInbound(rpc, REMOTE_NETWORK_ID);
            clientWorld.PumpReceive();
            clientWorld.PumpReceive();

            Assert.That(clientWorld.CountOf<ClientPacketSendRequest>(), Is.Zero);
            Assert.That(clientWorld.CountOf<RelayPacketRpc>(), Is.EqualTo(1));
            Assert.That(received.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task DisposeClearsBridgeSubscriptionAndPeerMapping()
        {
            using var clientWorld = new TestClientWorld();
            using var networkId = new ReactiveProperty<int>(LOCAL_NETWORK_ID);
            NetcodeP2PTransport transport = CreateTransport(clientWorld, networkId);
            var received = new List<P2PInboundPacket>();
            using IDisposable subscription = transport.PacketReceived.Subscribe(received.Add);

            await transport.SendAsync(new P2PRequestPacket(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                9,
                CreatePayload(4)));
            clientWorld.SendSystem.Update();
            RelayPacketRpc rpc = clientWorld.SingleRpc();
            clientWorld.DeliverInbound(rpc, REMOTE_NETWORK_ID);
            clientWorld.PumpReceive();

            Assert.That(transport.PeerCount, Is.EqualTo(2));
            Assert.That(received.Count, Is.EqualTo(1));

            transport.Dispose();
            clientWorld.DeliverInbound(rpc, REMOTE_NETWORK_ID);

            Assert.DoesNotThrow(() => clientWorld.PumpReceive());
            Assert.DoesNotThrow(() => networkId.Value = 0);
            Assert.That(transport.PeerCount, Is.Zero);
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.CatchAsync<ObjectDisposedException>(async () => await transport.SendAsync(new P2PRequestPacket(
                NetworkPacketId.DebugRelayCheckPing,
                NetworkPacketType.Echo,
                10,
                CreatePayload(4))));
        }

        [Test]
        public void TransportPublicSurfaceDoesNotLeakNetcodeTypes()
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
            Type transportType = typeof(NetcodeP2PTransport);
            var leakedTypes = new List<Type>();

            Assert.That(transportType.GetConstructors(FLAGS), Is.Empty);

            foreach (PropertyInfo property in transportType.GetProperties(FLAGS))
            {
                CollectTransportSpecific(property.PropertyType, leakedTypes);
            }

            foreach (MethodInfo method in transportType.GetMethods(FLAGS))
            {
                CollectTransportSpecific(method.ReturnType, leakedTypes);

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    CollectTransportSpecific(parameter.ParameterType, leakedTypes);
                }
            }

            Assert.That(leakedTypes, Is.Empty, string.Join(", ", leakedTypes.ConvertAll(type => type.FullName)));
        }

        private static void CollectTransportSpecific(Type type, List<Type> leakedTypes)
        {
            if (null == type)
            {
                return;
            }

            if (true == type.HasElementType)
            {
                CollectTransportSpecific(type.GetElementType(), leakedTypes);
            }

            if (true == type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    CollectTransportSpecific(argument, leakedTypes);
                }
            }

            string typeNamespace = type.Namespace ?? string.Empty;

            if (true == typeNamespace.StartsWith("Unity.NetCode", StringComparison.Ordinal)
                || true == typeNamespace.StartsWith("Unity.Entities", StringComparison.Ordinal)
                || true == typeNamespace.StartsWith("Unity.Networking.Transport", StringComparison.Ordinal)
                || true == typeNamespace.StartsWith("Unity.Collections", StringComparison.Ordinal))
            {
                leakedTypes.Add(type);
            }
        }
    }
}
