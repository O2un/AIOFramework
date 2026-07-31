using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network.Tests
{
    internal sealed class TestClientWorld : IDisposable
    {
        public World World { get; }
        public ClientSendPacketSystem SendSystem { get; }
        public ClientReceivePacketSystem ReceiveSystem { get; }
        public NetcodePacketDrainSystem DrainSystem { get; }

        public TestClientWorld(string name = "O2unPacketTestWorld")
        {
            World = new World(name);
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

        public List<RelayPacketRpc> DrainOutbound()
        {
            var drained = new List<RelayPacketRpc>();

            using EntityQuery query = World.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<RelayPacketRpc>(),
                ComponentType.ReadOnly<SendRpcCommandRequest>());
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            using NativeArray<RelayPacketRpc> packets = query.ToComponentDataArray<RelayPacketRpc>(Allocator.Temp);

            for (var i = 0; i < packets.Length; ++i)
            {
                drained.Add(packets[i]);
            }

            World.EntityManager.DestroyEntity(entities);

            return drained;
        }

        // Host 파이프라인은 03 책임이라, 여기서는 확정된 RPC 를 그대로 되돌려 Client 수신 경로만 검증한다.
        public void DeliverInbound(RelayPacketRpc packet, int senderNetworkId)
        {
            packet.SenderNetworkId = senderNetworkId;
            DeliverInbound(packet);
        }

        public void DeliverInbound(RelayPacketRpc packet)
        {
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

    // Client World 를 만들지 않는 것이 Dedicated Server 재사용의 검증 조건이다.
    internal sealed class TestServerWorld : IDisposable
    {
        public const int FORGED_NETWORK_ID = 99;

        private readonly Dictionary<int, Entity> _connections = new();

        public World World { get; }
        public ServerPacketImportSystem ImportSystem { get; }
        public ServerPacketProcessSystem ProcessSystem { get; }
        public ServerPacketExportSystem ExportSystem { get; }

        public TestServerWorld(string name = "O2unHostPipelineTestWorld")
        {
            World = new World(name);
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

            ReceiveFromClient(sourceNetworkId, new RelayPacketRpc
            {
                EventId = (int)eventId,
                PacketType = (int)packetType,
                SenderNetworkId = forgedSenderId,
                Sequence = sequence,
                Payload = wirePayload,
            });
        }

        public void ReceiveFromClient(int sourceNetworkId, RelayPacketRpc packet)
        {
            var entity = World.EntityManager.CreateEntity();
            World.EntityManager.AddComponentData(entity, packet);
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
}
