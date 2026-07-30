using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 게임 기능에 전송 구현을 노출하지 않고 패킷 관찰과 요청·응답만 제공한다.
    /// </summary>
    public interface IP2PMessenger : ISafeDisposable
    {
        ReadOnlyReactiveProperty<bool> IsConnected { get; }

        Observable<T> Observe<T>(NetworkPacketId eventId);
        UniTask<bool> SendDataAsync<T>(NetworkPacketId eventId, NetworkPacketType packetType, T data, CancellationToken ct = default);
        UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(NetworkPacketId eventId, NetworkPacketType packetType, TRequest data, TimeSpan timeout, CancellationToken ct = default);
        UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(NetworkPacketId eventId, NetworkPacketId responseEventId, NetworkPacketType packetType, TRequest data, TimeSpan timeout, CancellationToken ct = default);
    }
}
