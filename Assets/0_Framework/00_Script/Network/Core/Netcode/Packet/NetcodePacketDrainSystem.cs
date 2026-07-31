using System;
using O2un.Core.Utils;
using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(ClientReceivePacketSystem))]
    internal partial class NetcodePacketDrainSystem : SystemBase
    {
        private EntityQuery _queueQuery;

        protected override void OnCreate()
        {
            _queueQuery = GetEntityQuery(ComponentType.ReadOnly<NetcodeInboundPacketQueue>());
            RequireForUpdate(_queueQuery);
        }

        protected override void OnUpdate()
        {
            var queueEntity = _queueQuery.GetSingletonEntity();
            DynamicBuffer<NetcodeInboundPacket> buffer = EntityManager.GetBuffer<NetcodeInboundPacket>(queueEntity);

            if (0 == buffer.Length)
            {
                return;
            }

            // Bridge 구독자는 ECS 수명 밖에서 payload 를 붙잡을 수 있어 관리형 메모리로 복사한다.
            var drained = new NetcodeInboundPacketData[buffer.Length];

            for (var i = 0; i < buffer.Length; ++i)
            {
                NetcodeInboundPacket packet = buffer[i];
                drained[i] = new NetcodeInboundPacketData(
                    packet.EventId,
                    packet.PacketType,
                    packet.SenderNetworkId,
                    packet.Sequence,
                    NetcodePacketWire.ToManagedPayload(packet.Payload));
            }

            // 구독자 호출 전에 비워야 재진입 발행이 같은 패킷을 다시 흘리지 않는다.
            buffer.Clear();

            foreach (NetcodeInboundPacketData packet in drained)
            {
                try
                {
                    NetcodePacketBridge.RaisePacketDrained(World, packet);
                }
                catch (Exception e)
                {
                    // 한 패킷의 처리 실패가 남은 패킷 전달까지 막지 않도록 여기서 삼킨다.
                    Log.Print(Log.LogLevel.Error, $"[NetcodePacketDrainSystem] 수신 패킷 전달에 실패했다. eventId={packet.EventId}, error={e.Message}", Log.LogFilter.Server);
                }
            }
        }
    }
}
