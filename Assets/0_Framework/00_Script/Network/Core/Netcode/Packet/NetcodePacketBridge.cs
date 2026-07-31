using System;
using Unity.Entities;

namespace O2un.Core.Network
{
    // ECS World 에는 DI 가 없어 Drain System 이 Transport 를 주입받을 수 없다. 정적 브리지는 그 제약 때문에 생긴 것이므로
    // internal 로 묶어 어셈블리 밖에서는 보이지 않게 한다.
    internal static class NetcodePacketBridge
    {
        // 이 정적 통로는 살아 있는 모든 Client World 의 수신을 한곳에 모은다. 구독자가 자기 World 를 가려낼 수 있도록 출처를 같이 넘긴다.
        internal static event Action<World, NetcodeInboundPacketData> PacketDrained;

        internal static void RaisePacketDrained(World world, in NetcodeInboundPacketData packet)
        {
            PacketDrained?.Invoke(world, packet);
        }
    }
}
