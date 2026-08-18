using O2un.Roslyn.Generator;

namespace O2un.Core.Network
{
    [ServerHandler]
    public sealed class GameStartServerHandler : IHostPacketHandler
    {
        public NetworkPacketId EventId => NetworkPacketId.GameStart;

        public bool IsAllowedPacketType(NetworkPacketType packetType)
        {
            return NetworkPacketType.Broadcast == packetType;
        }

        public PacketProcessResult Handle(in PacketProcessContext context)
        {
            if (false == HostAuthority.IsHost(context.SenderId))
            {
                return PacketProcessResult.Block;
            }

            var stamped = new GameStartPayload
            {
                HostId = context.SenderId.Value,
            };

            return PacketProcessResult.Single(PacketSendDirective.Transform(context, stamped, P2PPacketTarget.ExcludeSender));
        }
    }
}
