using System;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 봉투 송수신 표면만 추린 것. 구현체 NetworkManager 는 O2un.SubSystem 에 있고 O2un.Network 는 그 어셈블리를 볼 수 없으므로,
    /// 매치메이킹 코드를 한 어셈블리 안에 온전히 두려면 이 방향의 인터페이스가 있어야 한다.
    /// </summary>
    public interface INetworkMessenger
    {
        ReadOnlyReactiveProperty<bool> IsConnected { get; }

        Observable<T> Observe<T>(string eventName);
        Observable<T> Observe<T>(string eventName, Func<JsonElement, T> parser);

        UniTask<bool> SendDataAsync<T>(string eventName, T data, CancellationToken ct = default);

        UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(string eventName, TRequest data, CancellationToken ct = default) where TResponse : class;
    }
}
