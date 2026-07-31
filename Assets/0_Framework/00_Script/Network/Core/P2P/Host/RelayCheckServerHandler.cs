using System;
using O2un.Roslyn.Generator;

namespace O2un.Core.Network
{
    /// <summary>
    /// 첫 수직 검증 Event 의 Host 판정이다. Broadcast 의도를 <see cref="P2PPacketTargetType.ExcludeSender"/> 발신으로 바꾸는 결정은 이 Handler 만 한다.
    /// 권한 정책을 주지 않으면 연결이 확정된 Sender 는 모두 Broadcast 할 수 있다.
    /// </summary>
    [ServerHandler]
    public sealed class RelayCheckServerHandler : IHostPacketHandler
    {
        private readonly Func<P2PPeerId, bool> _canBroadcast;

        public RelayCheckServerHandler(Func<P2PPeerId, bool> canBroadcast = null)
        {
            _canBroadcast = canBroadcast;
        }

        public NetworkPacketId EventId => NetworkPacketId.DebugRelayCheckPing;

        public bool IsAllowedPacketType(NetworkPacketType packetType)
        {
            return NetworkPacketType.Broadcast == packetType || NetworkPacketType.Echo == packetType;
        }

        public PacketProcessResult Handle(in PacketProcessContext context)
        {
            RelayCheckPayload payload = context.Deserialize<RelayCheckPayload>();

            if (null == payload || true == string.IsNullOrEmpty(payload.Message))
            {
                return PacketProcessResult.Block;
            }

            var stamped = new RelayCheckPayload
            {
                SenderId = context.SenderId.Value,
                Message = payload.Message,
            };

            if (NetworkPacketType.Echo == context.PacketType)
            {
                // Sequence 0 은 상관관계 없음이라 응답을 돌려줄 대기 요청 자체가 없다.
                if (0 == context.Sequence)
                {
                    return PacketProcessResult.Block;
                }

                return PacketProcessResult.Single(PacketSendDirective.Response(context, EventId, stamped));
            }

            if (null != _canBroadcast && false == _canBroadcast(context.SenderId))
            {
                return PacketProcessResult.Block;
            }

            return PacketProcessResult.Single(PacketSendDirective.Transform(context, stamped, P2PPacketTarget.ExcludeSender));
        }
    }
}
