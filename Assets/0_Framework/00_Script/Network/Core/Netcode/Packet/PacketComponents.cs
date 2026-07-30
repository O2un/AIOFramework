using System;
using Unity.Collections;
using Unity.Entities;

namespace O2un.Core.Network
{
    internal struct ClientPacketSendRequest : IComponentData
    {
        public int EventId;
        public int PacketType;
        public int SenderNetworkId;
        public ulong Sequence;
        public FixedList512Bytes<byte> Payload;
    }

    [InternalBufferCapacity(0)]
    internal struct NetcodeInboundPacket : IBufferElementData
    {
        public int EventId;
        public int PacketType;
        public int SenderNetworkId;
        public ulong Sequence;
        public FixedList512Bytes<byte> Payload;
    }

    internal struct NetcodeInboundPacketQueue : IComponentData
    {
    }

    internal readonly struct NetcodeInboundPacketData
    {
        internal int EventId { get; }
        internal int PacketType { get; }
        internal int SenderNetworkId { get; }
        internal ulong Sequence { get; }
        internal byte[] Payload { get; }

        internal NetcodeInboundPacketData(int eventId, int packetType, int senderNetworkId, ulong sequence, byte[] payload)
        {
            EventId = eventId;
            PacketType = packetType;
            SenderNetworkId = senderNetworkId;
            Sequence = sequence;
            Payload = payload;
        }
    }

    internal static class NetcodePacketWire
    {
        // FixedList512Bytes 는 512 byte 중 길이 헤더를 쓰므로 실제 payload 용량은 그보다 작다.
        // 상수를 손으로 적으면 컨테이너 교체 시 조용히 어긋나므로 컨테이너에 직접 묻는다.
        internal static readonly int MAX_PAYLOAD_BYTES = default(FixedList512Bytes<byte>).Capacity;

        internal static bool TryCreatePayload(ReadOnlySpan<byte> source, out FixedList512Bytes<byte> payload)
        {
            payload = default;

            if (MAX_PAYLOAD_BYTES < source.Length)
            {
                return false;
            }

            // AddRange 는 포인터를 요구하고 이 어셈블리는 unsafe 를 끄고 있어 원소 단위로 채운다.
            for (var i = 0; i < source.Length; ++i)
            {
                payload.Add(source[i]);
            }

            return true;
        }

        internal static byte[] ToManagedPayload(in FixedList512Bytes<byte> payload)
        {
            var managed = new byte[payload.Length];

            for (var i = 0; i < payload.Length; ++i)
            {
                managed[i] = payload[i];
            }

            return managed;
        }
    }
}
