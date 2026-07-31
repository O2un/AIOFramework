using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    internal partial class ClientReceivePacketSystem : SystemBase
    {
        private EntityQuery _rpcQuery;
        private Entity _queueEntity;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(ComponentType.ReadOnly<RelayPacketRpc>(), ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate(_rpcQuery);

            _queueEntity = EntityManager.CreateEntity(ComponentType.ReadWrite<NetcodeInboundPacketQueue>());
            EntityManager.AddBuffer<NetcodeInboundPacket>(_queueEntity);
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<RelayPacketRpc> packets = _rpcQuery.ToComponentDataArray<RelayPacketRpc>(Allocator.Temp);

            DynamicBuffer<NetcodeInboundPacket> buffer = EntityManager.GetBuffer<NetcodeInboundPacket>(_queueEntity);

            for (var i = 0; i < packets.Length; ++i)
            {
                RelayPacketRpc packet = packets[i];

                buffer.Add(new NetcodeInboundPacket
                {
                    EventId = packet.EventId,
                    PacketType = packet.PacketType,
                    SenderNetworkId = packet.SenderNetworkId,
                    Sequence = packet.Sequence,
                    Payload = packet.Payload,
                });
            }

            // 소비한 RPC 엔티티를 남기면 다음 프레임에 같은 패킷을 다시 적재한다.
            EntityManager.DestroyEntity(entities);
        }
    }
}
