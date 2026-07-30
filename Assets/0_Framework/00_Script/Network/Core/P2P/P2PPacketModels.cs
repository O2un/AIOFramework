using System;

namespace O2un.Core.Network
{
    public readonly struct P2PPeerId : IEquatable<P2PPeerId>
    {
        public static readonly P2PPeerId None = default;

        public ulong Value { get; }
        public bool IsValid => 0 != Value;

        public P2PPeerId(ulong value)
        {
            Value = value;
        }

        public bool Equals(P2PPeerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is P2PPeerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(P2PPeerId left, P2PPeerId right) => left.Equals(right);
        public static bool operator !=(P2PPeerId left, P2PPeerId right) => false == left.Equals(right);
    }

    public enum P2PPacketTargetType
    {
        None = 0,
        All = 1,
        ExcludeSender = 2,
        SenderOnly = 3,
        SpecificPeer = 4,
    }

    public readonly struct P2PPacketTarget
    {
        public static readonly P2PPacketTarget All = new(P2PPacketTargetType.All, P2PPeerId.None);
        public static readonly P2PPacketTarget ExcludeSender = new(P2PPacketTargetType.ExcludeSender, P2PPeerId.None);
        public static readonly P2PPacketTarget SenderOnly = new(P2PPacketTargetType.SenderOnly, P2PPeerId.None);

        public P2PPacketTargetType Type { get; }
        public P2PPeerId PeerId { get; }
        public bool IsValid => P2PPacketTargetType.None != Type && Enum.IsDefined(typeof(P2PPacketTargetType), Type) && (P2PPacketTargetType.SpecificPeer != Type || true == PeerId.IsValid);

        private P2PPacketTarget(P2PPacketTargetType type, P2PPeerId peerId)
        {
            Type = type;
            PeerId = peerId;
        }

        public static P2PPacketTarget SpecificPeer(P2PPeerId peerId)
        {
            if (false == peerId.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(peerId));
            }

            return new P2PPacketTarget(P2PPacketTargetType.SpecificPeer, peerId);
        }
    }

    public readonly struct P2PRequestPacket
    {
        public NetworkPacketId EventId { get; }
        public NetworkPacketType PacketType { get; }
        public ulong Sequence { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public P2PRequestPacket(NetworkPacketId eventId, NetworkPacketType packetType, ulong sequence, ReadOnlyMemory<byte> payload)
        {
            EventId = eventId;
            PacketType = packetType;
            Sequence = sequence;
            Payload = payload;
        }
    }

    public readonly struct P2PInboundPacket
    {
        public NetworkPacketId EventId { get; }
        public NetworkPacketType PacketType { get; }
        public P2PPeerId SenderId { get; }
        public ulong Sequence { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public P2PInboundPacket(NetworkPacketId eventId, NetworkPacketType packetType, P2PPeerId senderId, ulong sequence, ReadOnlyMemory<byte> payload)
        {
            EventId = eventId;
            PacketType = packetType;
            SenderId = senderId;
            Sequence = sequence;
            Payload = payload;
        }
    }

    public readonly struct P2POutboundPacket
    {
        public NetworkPacketId EventId { get; }
        public NetworkPacketType PacketType { get; }
        public P2PPeerId SenderId { get; }
        public ulong Sequence { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public P2PPacketTarget Target { get; }

        public P2POutboundPacket(NetworkPacketId eventId, NetworkPacketType packetType, P2PPeerId senderId, ulong sequence, ReadOnlyMemory<byte> payload, P2PPacketTarget target)
        {
            if (false == target.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            EventId = eventId;
            PacketType = packetType;
            SenderId = senderId;
            Sequence = sequence;
            Payload = payload;
            Target = target;
        }
    }
}
