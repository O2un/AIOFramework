using System;

namespace O2un.Core.Network
{
    // ECS World 에는 DI 가 없어 Drain System 이 Transport 를 주입받을 수 없다. 정적 브리지는 그 제약 때문에 생긴 것이므로
    // internal 로 묶어 어셈블리 밖에서는 보이지 않게 한다.
    internal static class NetcodePacketBridge
    {
        internal static event Action<NetcodeInboundPacketData> PacketDrained;

        internal static void RaisePacketDrained(in NetcodeInboundPacketData packet)
        {
            PacketDrained?.Invoke(packet);
        }
    }
}
