using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    internal partial class ClientSendPacketSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<ClientPacketSendRequest>());
            RequireForUpdate(_requestQuery);
        }

        protected override void OnUpdate()
        {
            // 엔티티 생성이 쿼리를 무효화하므로 구조 변경 전에 요청을 전부 복사해 둔다.
            using NativeArray<Entity> entities = _requestQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<ClientPacketSendRequest> requests = _requestQuery.ToComponentDataArray<ClientPacketSendRequest>(Allocator.Temp);

            for (var i = 0; i < requests.Length; ++i)
            {
                ClientPacketSendRequest request = requests[i];
                var rpcEntity = EntityManager.CreateEntity();

                EntityManager.AddComponentData(rpcEntity, new RelayPacketRpc
                {
                    EventId = request.EventId,
                    PacketType = request.PacketType,
                    SenderNetworkId = request.SenderNetworkId,
                    Sequence = request.Sequence,
                    Payload = request.Payload,
                });
                EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest());
            }

            EntityManager.DestroyEntity(entities);
        }
    }
}
