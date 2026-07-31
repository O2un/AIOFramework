using O2un.Core.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    internal partial class ServerPacketImportSystem : SystemBase
    {
        private Entity _queueEntity;

        protected override void OnCreate()
        {
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<RelayPacketRpc>(), ComponentType.ReadOnly<ReceiveRpcCommandRequest>()));

            _queueEntity = ServerPacketQueueUtility.GetOrCreate(EntityManager);
        }

        protected override void OnUpdate()
        {
            DynamicBuffer<InboundPacketElement> buffer = EntityManager.GetBuffer<InboundPacketElement>(_queueEntity);

            // 순회 중 DestroyEntity 는 구조 변경이라 Enumerator 를 무효화한다.
            var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRO<RelayPacketRpc> rpc, RefRO<ReceiveRpcCommandRequest> request, Entity entity)
                in SystemAPI.Query<RefRO<RelayPacketRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                commands.DestroyEntity(entity);

                Entity sourceConnection = request.ValueRO.SourceConnection;

                if (false == EntityManager.Exists(sourceConnection) || false == EntityManager.HasComponent<NetworkId>(sourceConnection))
                {
                    Log.Dev($"[ServerPacketImportSystem] 패킷을 차단했다. reason={PacketBlockReason.InvalidSender}, eventId={rpc.ValueRO.EventId}", Log.LogLevel.Warning);
                    continue;
                }

                buffer.Add(new InboundPacketElement
                {
                    EventId = rpc.ValueRO.EventId,
                    PacketType = rpc.ValueRO.PacketType,
                    // Client 가 채운 SenderNetworkId 는 신뢰하지 않는다.
                    SenderNetworkId = EntityManager.GetComponentData<NetworkId>(sourceConnection).Value,
                    Sequence = rpc.ValueRO.Sequence,
                    Payload = rpc.ValueRO.Payload,
                });
            }

            commands.Playback(EntityManager);
            commands.Dispose();
        }
    }
}
