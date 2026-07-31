using O2un.Core.Utils;
using Unity.Collections;
using Unity.Entities;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateAfter(typeof(ServerPacketImportSystem))]
    internal partial class ServerPacketProcessSystem : SystemBase
    {
        private ServerPacketProcessor _processor;
        private Entity _queueEntity;

        internal ServerPacketProcessor Processor => _processor;

        protected override void OnCreate()
        {
            _queueEntity = ServerPacketQueueUtility.GetOrCreate(EntityManager);
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<ServerPacketQueue>()));

            _processor = new ServerPacketProcessor(NetcodePacketWire.MAX_PAYLOAD_BYTES);

            ServerPacketHandlers.AttachRegistry(_processor);
        }

        protected override void OnDestroy()
        {
            ServerPacketHandlers.DetachRegistry(_processor);

            _processor?.Dispose();
            _processor = null;
        }

        protected override void OnUpdate()
        {
            DynamicBuffer<InboundPacketElement> inbound = EntityManager.GetBuffer<InboundPacketElement>(_queueEntity);

            if (0 == inbound.Length)
            {
                return;
            }

            // Handler 가 같은 프레임에 다시 발신을 유발해도 이미 처리한 패킷을 다시 돌리지 않도록 먼저 걷어낸다.
            var drained = new InboundPacketElement[inbound.Length];

            for (var i = 0; i < inbound.Length; ++i)
            {
                drained[i] = inbound[i];
            }

            inbound.Clear();

            foreach (InboundPacketElement packet in drained)
            {
                var context = new PacketProcessContext(
                    (NetworkPacketId)packet.EventId,
                    (NetworkPacketType)packet.PacketType,
                    new P2PPeerId((ulong)packet.SenderNetworkId),
                    packet.Sequence,
                    NetcodePacketWire.ToManagedPayload(packet.Payload));

                PacketProcessResult result = _processor.Process(context);

                for (var i = 0; i < result.Count; ++i)
                {
                    Enqueue(packet.SenderNetworkId, result[i]);
                }
            }
        }

        private void Enqueue(int senderNetworkId, in PacketSendDirective directive)
        {
            if (false == NetcodePacketWire.TryCreatePayload(directive.Payload.Span, out FixedList512Bytes<byte> payload))
            {
                // Processor 가 이미 검사했지만 전송 컨테이너가 바뀌면 여기가 마지막 방어선이다. 조용히 자르지 않는다.
                Log.Print(
                    Log.LogLevel.Error,
                    $"[ServerPacketProcessSystem] 발신 payload 가 상한을 넘어 차단했다. eventId={(int)directive.EventId}, size={directive.Payload.Length}",
                    Log.LogFilter.Server);
                return;
            }

            DynamicBuffer<OutboundPacketElement> outbound = EntityManager.GetBuffer<OutboundPacketElement>(_queueEntity);

            outbound.Add(new OutboundPacketElement
            {
                EventId = (int)directive.EventId,
                PacketType = (int)directive.PacketType,
                SenderNetworkId = senderNetworkId,
                Sequence = directive.Sequence,
                TargetType = (int)directive.Target.Type,
                TargetPeerId = directive.Target.PeerId.Value,
                Payload = payload,
            });
        }
    }
}
