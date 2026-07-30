using Unity.Collections;
using Unity.NetCode;

namespace O2un.Core.Network
{
    // Netcode RPC 코드 생성기는 필드가 public 이어야만 직렬화하므로 프로퍼티로 감싸지 않는다.
    // 타입 자체는 internal 로 묶어 wire 구조가 어셈블리 밖으로 새지 않게 한다.
    internal struct RelayPacketRpc : IRpcCommand
    {
        public int EventId;
        public int PacketType;
        public int SenderNetworkId;
        public ulong Sequence;
        public FixedList512Bytes<byte> Payload;
    }
}
