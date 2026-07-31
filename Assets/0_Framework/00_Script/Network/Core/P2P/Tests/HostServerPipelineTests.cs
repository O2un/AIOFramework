using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.TestTools;

namespace O2un.Core.Network.Tests
{
    public sealed class HostServerPipelineTests
    {
        private const int HOST_NETWORK_ID = 1;
        private const int GUEST_NETWORK_ID = 2;
        private const int OTHER_NETWORK_ID = 3;
        private const int FORGED_NETWORK_ID = 99;

        private const NetworkPacketId TEST_EVENT_ID = NetworkPacketId.DebugRelayCheckPing;

        private sealed class TestPayload
        {
            public int SampleValue { get; set; }
        }

        private sealed class StubHandler : IHostPacketHandler
        {
            private readonly Func<PacketProcessContext, PacketProcessResult> _handle;

            public StubHandler(Func<PacketProcessContext, PacketProcessResult> handle, NetworkPacketType allowed = NetworkPacketType.Broadcast)
            {
                _handle = handle;
                AllowedPacketType = allowed;
            }

            public NetworkPacketId EventId => TEST_EVENT_ID;
            public NetworkPacketType AllowedPacketType { get; }
            public List<PacketProcessContext> Received { get; } = new();

            public bool IsAllowedPacketType(NetworkPacketType packetType) => AllowedPacketType == packetType;

            public PacketProcessResult Handle(in PacketProcessContext context)
            {
                Received.Add(context);
                return _handle(context);
            }
        }

        // Client World 를 만들지 않는 것이 Dedicated Server 재사용의 검증 조건이다.
        private sealed class TestServerWorld : IDisposable
        {
            private readonly Dictionary<int, Entity> _connections = new();

            public World World { get; }
            public ServerPacketImportSystem ImportSystem { get; }
            public ServerPacketProcessSystem ProcessSystem { get; }
            public ServerPacketExportSystem ExportSystem { get; }

            public TestServerWorld()
            {
                World = new World("O2unHostPipelineTestWorld");
                ImportSystem = World.CreateSystemManaged<ServerPacketImportSystem>();
                ProcessSystem = World.CreateSystemManaged<ServerPacketProcessSystem>();
                ExportSystem = World.CreateSystemManaged<ServerPacketExportSystem>();
            }

            public ServerPacketProcessor Processor => ProcessSystem.Processor;

            public void Dispose()
            {
                World.Dispose();
            }

            public Entity AddConnection(int networkId)
            {
                var entity = World.EntityManager.CreateEntity();
                World.EntityManager.AddComponentData(entity, new NetworkId { Value = networkId });
                _connections[networkId] = entity;

                return entity;
            }

            public void RemoveConnection(int networkId)
            {
                World.EntityManager.DestroyEntity(_connections[networkId]);
                _connections.Remove(networkId);
            }

            public void ReceiveFromClient(int sourceNetworkId, NetworkPacketId eventId, NetworkPacketType packetType, ulong sequence, byte[] payload, int forgedSenderId = FORGED_NETWORK_ID)
            {
                Assert.That(NetcodePacketWire.TryCreatePayload(payload, out FixedList512Bytes<byte> wirePayload), Is.True);

                var entity = World.EntityManager.CreateEntity();
                World.EntityManager.AddComponentData(entity, new RelayPacketRpc
                {
                    EventId = (int)eventId,
                    PacketType = (int)packetType,
                    SenderNetworkId = forgedSenderId,
                    Sequence = sequence,
                    Payload = wirePayload,
                });
                World.EntityManager.AddComponentData(entity, new ReceiveRpcCommandRequest
                {
                    SourceConnection = _connections[sourceNetworkId],
                });
            }

            public void Tick()
            {
                ImportSystem.Update();
                ProcessSystem.Update();
                ExportSystem.Update();
            }

            public List<(RelayPacketRpc Packet, int TargetNetworkId)> DrainSent()
            {
                var sent = new List<(RelayPacketRpc, int)>();

                using EntityQuery query = World.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<RelayPacketRpc>(),
                    ComponentType.ReadOnly<SendRpcCommandRequest>());
                using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
                using NativeArray<RelayPacketRpc> packets = query.ToComponentDataArray<RelayPacketRpc>(Allocator.Temp);
                using NativeArray<SendRpcCommandRequest> requests = query.ToComponentDataArray<SendRpcCommandRequest>(Allocator.Temp);

                for (var i = 0; i < packets.Length; ++i)
                {
                    Entity target = requests[i].TargetConnection;
                    sent.Add((packets[i], World.EntityManager.GetComponentData<NetworkId>(target).Value));
                }

                World.EntityManager.DestroyEntity(entities);

                return sent;
            }
        }

        private static TestServerWorld CreateWorldWithHandler(StubHandler handler, params int[] networkIds)
        {
            var world = new TestServerWorld();

            foreach (var networkId in networkIds)
            {
                world.AddConnection(networkId);
            }

            if (null != handler)
            {
                world.Processor.Register(handler);
            }

            return world;
        }

        private static byte[] Payload(int value)
        {
            return NetworkJson.SerializeToUtf8Bytes(new TestPayload { SampleValue = value });
        }

        [Test]
        public void Import_Overrides_Forged_Sender_With_Source_Connection()
        {
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.All)));
            using TestServerWorld world = CreateWorldWithHandler(handler, HOST_NETWORK_ID, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1), FORGED_NETWORK_ID);
            world.Tick();

            Assert.That(handler.Received.Count, Is.EqualTo(1));
            Assert.That(handler.Received[0].SenderId, Is.EqualTo(new P2PPeerId(GUEST_NETWORK_ID)));
            Assert.That(handler.Received[0].SenderId, Is.Not.EqualTo(new P2PPeerId(FORGED_NETWORK_ID)));
        }

        [Test]
        public void Import_Consumes_Rpc_Entities_Once()
        {
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.SenderOnly)));
            using TestServerWorld world = CreateWorldWithHandler(handler, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();
            Assert.That(world.DrainSent().Count, Is.EqualTo(1));

            world.Tick();
            Assert.That(handler.Received.Count, Is.EqualTo(1));
            Assert.That(world.DrainSent(), Is.Empty);
        }

        [Test]
        public void Unregistered_Handler_Is_Blocked_By_Default()
        {
            using TestServerWorld world = CreateWorldWithHandler(null, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();

            Assert.That(world.DrainSent(), Is.Empty);
        }

        [Test]
        public void Undefined_Event_And_Unknown_Packet_Type_Are_Blocked()
        {
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.All)));
            using TestServerWorld world = CreateWorldWithHandler(handler, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, NetworkPacketId.None, NetworkPacketType.Broadcast, 0, Payload(1));
            world.ReceiveFromClient(GUEST_NETWORK_ID, (NetworkPacketId)123456, NetworkPacketType.Broadcast, 0, Payload(1));
            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.None, 0, Payload(1));
            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, (NetworkPacketType)77, 0, Payload(1));
            world.Tick();

            Assert.That(handler.Received, Is.Empty);
            Assert.That(world.DrainSent(), Is.Empty);
        }

        [Test]
        public void Packet_Type_Not_Allowed_By_Handler_Is_Blocked()
        {
            var handler = new StubHandler(
                context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.All)),
                NetworkPacketType.Echo);
            using TestServerWorld world = CreateWorldWithHandler(handler, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();

            Assert.That(handler.Received, Is.Empty);
            Assert.That(world.DrainSent(), Is.Empty);
        }

        [Test]
        public void Invalid_Json_And_Handler_Rejection_Produce_No_Send()
        {
            var handler = new StubHandler(context =>
            {
                TestPayload payload = context.Deserialize<TestPayload>();
                return PacketProcessResult.Single(PacketSendDirective.Transform(context, payload, P2PPacketTarget.All));
            });
            using TestServerWorld world = CreateWorldWithHandler(handler, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, new byte[] { 0x7B, 0x7B, 0x7B });
            world.Tick();

            Assert.That(world.DrainSent(), Is.Empty);

            using TestServerWorld rejecting = CreateWorldWithHandler(new StubHandler(_ => PacketProcessResult.Block), GUEST_NETWORK_ID);
            rejecting.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            rejecting.Tick();

            Assert.That(rejecting.DrainSent(), Is.Empty);
        }

        [Test]
        public void Oversized_Transformed_Payload_Is_Blocked_Instead_Of_Truncated()
        {
            // 서버 결함은 Error 로그가 남는 것까지가 계약이라 러너에 미리 알린다.
            LogAssert.Expect(LogType.Error, new Regex("잘못된 발신 지시"));

            using var processor = new ServerPacketProcessor(NetcodePacketWire.MAX_PAYLOAD_BYTES);
            processor.Register(new StubHandler(context => PacketProcessResult.Single(
                new PacketSendDirective(context.EventId, context.PacketType, context.Sequence, new byte[NetcodePacketWire.MAX_PAYLOAD_BYTES + 1], P2PPacketTarget.All))));

            var context = new PacketProcessContext(TEST_EVENT_ID, NetworkPacketType.Broadcast, new P2PPeerId(GUEST_NETWORK_ID), 0, Payload(1));
            PacketProcessResult result = processor.Process(context, out PacketBlockReason reason);

            Assert.That(result.Count, Is.EqualTo(0));
            Assert.That(reason, Is.EqualTo(PacketBlockReason.InvalidDirective));
        }

        [Test]
        public void Invalid_Sender_Is_Blocked()
        {
            using var processor = new ServerPacketProcessor(NetcodePacketWire.MAX_PAYLOAD_BYTES);
            processor.Register(new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.All))));

            var context = new PacketProcessContext(TEST_EVENT_ID, NetworkPacketType.Broadcast, P2PPeerId.None, 0, Payload(1));
            processor.Process(context, out PacketBlockReason reason);

            Assert.That(reason, Is.EqualTo(PacketBlockReason.InvalidSender));
        }

        [Test]
        public void Handler_Controls_All_Target_Kinds()
        {
            var target = P2PPacketTarget.All;
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, target)));
            using TestServerWorld world = CreateWorldWithHandler(handler, HOST_NETWORK_ID, GUEST_NETWORK_ID, OTHER_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();
            Assert.That(world.DrainSent().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { HOST_NETWORK_ID, GUEST_NETWORK_ID, OTHER_NETWORK_ID }));

            target = P2PPacketTarget.ExcludeSender;
            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();
            Assert.That(world.DrainSent().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { HOST_NETWORK_ID, OTHER_NETWORK_ID }));

            target = P2PPacketTarget.SenderOnly;
            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();
            Assert.That(world.DrainSent().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { GUEST_NETWORK_ID }));

            target = P2PPacketTarget.SpecificPeer(new P2PPeerId(OTHER_NETWORK_ID));
            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();
            Assert.That(world.DrainSent().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { OTHER_NETWORK_ID }));
        }

        [Test]
        public void Handler_Can_Emit_Multiple_Directives()
        {
            var handler = new StubHandler(context => PacketProcessResult.Multiple(
                PacketSendDirective.Relay(context, P2PPacketTarget.ExcludeSender),
                PacketSendDirective.Response(context, TEST_EVENT_ID, new TestPayload { SampleValue = 7 })));
            using TestServerWorld world = CreateWorldWithHandler(handler, HOST_NETWORK_ID, GUEST_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 42, Payload(1));
            world.Tick();

            List<(RelayPacketRpc Packet, int TargetNetworkId)> sent = world.DrainSent();
            Assert.That(sent.Count, Is.EqualTo(2));
            Assert.That(sent.Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { HOST_NETWORK_ID, GUEST_NETWORK_ID }));

            Assert.That(sent.All(x => 42UL == x.Packet.Sequence), Is.True);
        }

        [Test]
        public void Sequence_And_Packet_Type_Do_Not_Auto_Select_Target()
        {
            var handler = new StubHandler(_ => PacketProcessResult.Block);
            using TestServerWorld world = CreateWorldWithHandler(handler, HOST_NETWORK_ID, GUEST_NETWORK_ID, OTHER_NETWORK_ID);

            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 99, Payload(1));
            world.Tick();

            Assert.That(world.DrainSent(), Is.Empty);
        }

        [Test]
        public void Missing_Target_Connection_Does_Not_Block_Remaining_Targets()
        {
            var handler = new StubHandler(context => PacketProcessResult.Multiple(
                PacketSendDirective.Relay(context, P2PPacketTarget.SpecificPeer(new P2PPeerId(OTHER_NETWORK_ID))),
                PacketSendDirective.Relay(context, P2PPacketTarget.SenderOnly)));
            using TestServerWorld world = CreateWorldWithHandler(handler, GUEST_NETWORK_ID, OTHER_NETWORK_ID);

            world.RemoveConnection(OTHER_NETWORK_ID);
            world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();

            Assert.That(world.DrainSent().Select(x => x.TargetNetworkId), Is.EquivalentTo(new[] { GUEST_NETWORK_ID }));
        }

        [Test]
        public void Installer_Registered_Before_World_Creation_Is_Applied()
        {
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.SenderOnly)));

            void Install(IHostPacketHandlerRegistry registry) => registry.Register(handler);

            ServerPacketHandlers.AddInstaller(Install);

            try
            {
                using TestServerWorld world = CreateWorldWithHandler(null, GUEST_NETWORK_ID);

                world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
                world.Tick();

                Assert.That(world.DrainSent().Count, Is.EqualTo(1));
            }
            finally
            {
                ServerPacketHandlers.RemoveInstaller(Install);
            }
        }

        [Test]
        public void Installer_Registered_After_World_Creation_Reaches_Live_Registry()
        {
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.SenderOnly)));

            void Install(IHostPacketHandlerRegistry registry) => registry.Register(handler);

            using TestServerWorld world = CreateWorldWithHandler(null, GUEST_NETWORK_ID);
            Assert.That(world.Processor.HandlerCount, Is.EqualTo(0));

            ServerPacketHandlers.AddInstaller(Install);

            try
            {
                world.ReceiveFromClient(GUEST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
                world.Tick();

                Assert.That(world.DrainSent().Count, Is.EqualTo(1));
            }
            finally
            {
                ServerPacketHandlers.RemoveInstaller(Install);
            }
        }

        [Test]
        public void Installer_Cannot_Harvest_Registered_Handlers()
        {
            IHostPacketHandlerRegistry captured = null;

            void Install(IHostPacketHandlerRegistry registry) => captured = registry;

            ServerPacketHandlers.AddInstaller(Install);

            try
            {
                using TestServerWorld world = CreateWorldWithHandler(null, GUEST_NETWORK_ID);

                Assert.That(captured, Is.Not.Null);

                // 넣기만 가능한 표면이어야 하고, 구현체로 캐스팅해 Process 에 닿을 수도 없어야 한다.
                Assert.That(
                    typeof(IHostPacketHandlerRegistry).GetMethods().Any(x => typeof(IHostPacketHandler).IsAssignableFrom(x.ReturnType)),
                    Is.False);

                Assert.That(captured.GetType().IsPublic, Is.False);
            }
            finally
            {
                ServerPacketHandlers.RemoveInstaller(Install);
            }
        }

        [Test]
        public void Specific_Peer_Above_Int_Range_Does_Not_Misroute()
        {
            // 하위 32bit 가 GUEST 와 같다. int 로 줄이면 GUEST 에게 payload 가 노출된다.
            var forgedPeer = new P2PPeerId(0x0000000100000000UL | GUEST_NETWORK_ID);
            var handler = new StubHandler(context => PacketProcessResult.Single(PacketSendDirective.Relay(context, P2PPacketTarget.SpecificPeer(forgedPeer))));
            using TestServerWorld world = CreateWorldWithHandler(handler, HOST_NETWORK_ID, GUEST_NETWORK_ID);

            world.ReceiveFromClient(HOST_NETWORK_ID, TEST_EVENT_ID, NetworkPacketType.Broadcast, 0, Payload(1));
            world.Tick();

            Assert.That(world.DrainSent(), Is.Empty);
        }

        [Test]
        public void Default_Directive_Is_Blocked()
        {
            LogAssert.Expect(LogType.Error, new Regex("잘못된 발신 지시"));

            using var processor = new ServerPacketProcessor(NetcodePacketWire.MAX_PAYLOAD_BYTES);
            processor.Register(new StubHandler(_ => PacketProcessResult.Single(default)));

            var context = new PacketProcessContext(TEST_EVENT_ID, NetworkPacketType.Broadcast, new P2PPeerId(GUEST_NETWORK_ID), 0, Payload(1));
            PacketProcessResult result = processor.Process(context, out PacketBlockReason reason);

            Assert.That(result.Count, Is.EqualTo(0));
            Assert.That(reason, Is.EqualTo(PacketBlockReason.InvalidDirective));
        }

        [Test]
        public void Handler_Defect_Is_Not_Recorded_As_Client_Input_Error()
        {
            LogAssert.Expect(LogType.Error, new Regex("Handler 가 예외로 실패했다"));

            using var processor = new ServerPacketProcessor(NetcodePacketWire.MAX_PAYLOAD_BYTES);
            processor.Register(new StubHandler(_ => throw new NullReferenceException()));

            var context = new PacketProcessContext(TEST_EVENT_ID, NetworkPacketType.Broadcast, new P2PPeerId(GUEST_NETWORK_ID), 0, Payload(1));
            processor.Process(context, out PacketBlockReason reason);

            Assert.That(reason, Is.EqualTo(PacketBlockReason.HandlerFailed));
            Assert.That(processor.BlockedCount(PacketBlockReason.HandlerFailed), Is.EqualTo(1));
            Assert.That(processor.BlockedCount(PacketBlockReason.DeserializeFailed), Is.EqualTo(0));
        }

        [Test]
        public void Malformed_Json_Is_Recorded_As_Deserialize_Failure()
        {
            using var processor = new ServerPacketProcessor(NetcodePacketWire.MAX_PAYLOAD_BYTES);
            processor.Register(new StubHandler(context => PacketProcessResult.Single(
                PacketSendDirective.Transform(context, context.Deserialize<TestPayload>(), P2PPacketTarget.All))));

            var context = new PacketProcessContext(TEST_EVENT_ID, NetworkPacketType.Broadcast, new P2PPeerId(GUEST_NETWORK_ID), 0, new byte[] { 0x7B, 0x7B, 0x7B });
            processor.Process(context, out PacketBlockReason reason);

            Assert.That(reason, Is.EqualTo(PacketBlockReason.DeserializeFailed));
        }

        [Test]
        public void Processor_Registry_Is_Cleared_On_Dispose()
        {
            var world = new TestServerWorld();
            world.Processor.Register(new StubHandler(_ => PacketProcessResult.Block));
            ServerPacketProcessor processor = world.Processor;

            world.Dispose();

            Assert.That(processor.IsDisposed, Is.True);
            Assert.That(processor.HandlerCount, Is.EqualTo(0));
        }

        [Test]
        public void Common_Processing_Surface_Has_No_Netcode_Types()
        {
            var surfaces = new[]
            {
                typeof(PacketProcessContext),
                typeof(PacketSendDirective),
                typeof(PacketProcessResult),
                typeof(IHostPacketHandler),
                typeof(IHostPacketHandlerRegistry),
                typeof(ServerPacketProcessor),
            };

            foreach (Type surface in surfaces)
            {
                foreach (Type exposed in PublicTypesOf(surface))
                {
                    string assemblyName = exposed.Assembly.GetName().Name;

                    Assert.That(
                        assemblyName.StartsWith("Unity.Entities") || assemblyName.StartsWith("Unity.NetCode") || assemblyName.StartsWith("Unity.Networking.Transport"),
                        Is.False,
                        $"{surface.Name} 이 {exposed.FullName} 을 노출한다.");
                }
            }
        }

        [Test]
        public void Host_Client_Has_No_Direct_Handler_Invocation_Api()
        {
            MethodInfo[] publicStatics = typeof(ServerPacketHandlers)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.That(publicStatics.Any(x => typeof(PacketProcessResult) == x.ReturnType), Is.False);

            Assert.That(publicStatics.Any(x => x.GetParameters().Any(p => typeof(IHostPacketHandlerRegistry) == p.ParameterType)), Is.False);

            Assert.That(typeof(ServerPacketProcessor).IsPublic, Is.False);
            Assert.That(typeof(ServerPacketProcessSystem).IsPublic, Is.False);
            Assert.That(typeof(ServerPacketProcessSystem).GetProperty("Processor", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        }

        private static IEnumerable<Type> PublicTypesOf(Type surface)
        {
            foreach (PropertyInfo property in surface.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                yield return property.PropertyType;
            }

            foreach (MethodInfo method in surface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                yield return method.ReturnType;

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }
        }
    }
}
