using Unity.Collections;
using Unity.Entities;

namespace O2un.Core.Network
{
    internal struct ServerPacketQueue : IComponentData
    {
    }

    [InternalBufferCapacity(0)]
    internal struct InboundPacketElement : IBufferElementData
    {
        public int EventId;
        public int PacketType;
        public int SenderNetworkId;
        public ulong Sequence;
        public FixedList512Bytes<byte> Payload;
    }

    [InternalBufferCapacity(0)]
    internal struct OutboundPacketElement : IBufferElementData
    {
        public int EventId;
        public int PacketType;
        public int SenderNetworkId;
        public ulong Sequence;
        public int TargetType;
        public ulong TargetPeerId;
        public FixedList512Bytes<byte> Payload;
    }

    internal static class ServerPacketQueueUtility
    {
        // Import/Process/Export 가 만들어지는 순서에 기대면 테스트나 World 재구성에서 조용히 깨진다.
        internal static Entity GetOrCreate(EntityManager entityManager)
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ServerPacketQueue>());

            if (false == query.IsEmpty)
            {
                return query.GetSingletonEntity();
            }

            var entity = entityManager.CreateEntity(ComponentType.ReadWrite<ServerPacketQueue>());
            entityManager.AddBuffer<InboundPacketElement>(entity);
            entityManager.AddBuffer<OutboundPacketElement>(entity);

            return entity;
        }
    }
}
