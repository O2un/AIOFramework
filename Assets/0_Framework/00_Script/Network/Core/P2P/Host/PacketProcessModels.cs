using System;

namespace O2un.Core.Network
{
    /// <summary>
    /// Host Handler 가 보는 유일한 입력이다. Sender 는 전송 계층이 실제 연결에서 확정한 값이며 Client 가 채운 값이 아니다.
    /// </summary>
    public readonly struct PacketProcessContext
    {
        public NetworkPacketId EventId { get; }
        public NetworkPacketType PacketType { get; }
        public P2PPeerId SenderId { get; }
        public ulong Sequence { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public PacketProcessContext(NetworkPacketId eventId, NetworkPacketType packetType, P2PPeerId senderId, ulong sequence, ReadOnlyMemory<byte> payload)
        {
            EventId = eventId;
            PacketType = packetType;
            SenderId = senderId;
            Sequence = sequence;
            Payload = payload;
        }

        public T Deserialize<T>()
        {
            return NetworkJson.Deserialize<T>(Payload.Span);
        }
    }

    public readonly struct PacketSendDirective
    {
        public NetworkPacketId EventId { get; }
        public NetworkPacketType PacketType { get; }
        public ulong Sequence { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public P2PPacketTarget Target { get; }

        public bool IsValid => EventId.IsDefined() && PacketType.IsDefined() && Target.IsValid;

        public PacketSendDirective(NetworkPacketId eventId, NetworkPacketType packetType, ulong sequence, ReadOnlyMemory<byte> payload, P2PPacketTarget target)
        {
            if (false == eventId.IsDefined())
            {
                throw new ArgumentOutOfRangeException(nameof(eventId));
            }

            if (false == packetType.IsDefined())
            {
                throw new ArgumentOutOfRangeException(nameof(packetType));
            }

            if (false == target.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            EventId = eventId;
            PacketType = packetType;
            Sequence = sequence;
            Payload = payload;
            Target = target;
        }

        public static PacketSendDirective Relay(in PacketProcessContext context, P2PPacketTarget target)
        {
            return new PacketSendDirective(context.EventId, context.PacketType, context.Sequence, context.Payload, target);
        }

        public static PacketSendDirective Transform<T>(in PacketProcessContext context, T payload, P2PPacketTarget target)
        {
            return new PacketSendDirective(context.EventId, context.PacketType, context.Sequence, NetworkJson.SerializeToUtf8Bytes(payload), target);
        }

        // 응답은 요청과 같은 Sequence 를 써야 Client Tracker 가 대기 중인 요청과 이어붙일 수 있다.
        public static PacketSendDirective Response<T>(in PacketProcessContext context, NetworkPacketId responseEventId, T payload)
        {
            return new PacketSendDirective(responseEventId, context.PacketType, context.Sequence, NetworkJson.SerializeToUtf8Bytes(payload), P2PPacketTarget.SenderOnly);
        }
    }

    public readonly struct PacketProcessResult
    {
        private static readonly PacketSendDirective[] EMPTY = Array.Empty<PacketSendDirective>();

        private readonly PacketSendDirective[] _directives;

        private PacketProcessResult(PacketSendDirective[] directives)
        {
            _directives = directives;
        }

        public static PacketProcessResult Block => new(EMPTY);

        public int Count => null == _directives ? 0 : _directives.Length;

        public PacketSendDirective this[int index] => _directives[index];

        public static PacketProcessResult Single(PacketSendDirective directive)
        {
            return new PacketProcessResult(new[] { directive });
        }

        public static PacketProcessResult Multiple(params PacketSendDirective[] directives)
        {
            if (null == directives || 0 == directives.Length)
            {
                return Block;
            }

            return new PacketProcessResult(directives);
        }
    }

    public enum PacketBlockReason
    {
        None = 0,
        UndefinedEventId = 1,
        UnknownPacketType = 2,
        HandlerNotRegistered = 3,
        PacketTypeNotAllowed = 4,
        InvalidSender = 5,
        PayloadTooLarge = 6,
        DeserializeFailed = 7,
        HandlerRejected = 8,
        InvalidDirective = 9,
        HandlerFailed = 10,
    }
}
