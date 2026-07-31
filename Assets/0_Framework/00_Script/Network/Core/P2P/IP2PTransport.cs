using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 전송 구현의 연결·wire framing·peer 매핑을 공통 패킷 경계 뒤로 숨긴다.
    /// </summary>
    public interface IP2PTransport : ISafeDisposable
    {
        ReadOnlyReactiveProperty<bool> IsConnected { get; }
        Observable<P2PInboundPacket> PacketReceived { get; }

        UniTask<bool> SendAsync(P2PRequestPacket packet, CancellationToken ct = default);
    }
}
