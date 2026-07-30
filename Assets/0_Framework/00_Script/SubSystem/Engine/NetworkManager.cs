using System;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Data;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// WSS 접속 수명의 소유자. 봉투 처리는 내부 Messenger 모듈이 하고 외부에는 <see cref="INetworkMessenger"/> 표면만 나간다.
    /// Messenger 를 서브시스템으로 만들 수 없는 이유는 상속 슬롯이 공통 Messenger 기반에 필요하기 때문이다.
    /// </summary>
    public sealed class NetworkManager : EngineSubsystemBase, INetworkMessenger
    {
        // INetworkMessenger 주입은 부팅보다 먼저 일어나므로 생성만 생성자에서 끝내고 접속만 InitAsync 로 미룬다.
        private readonly WebSocketMessenger _messenger;

        public NetworkManager(IRuntimeDataProvider dataProvider)
        {
            _messenger = new WebSocketMessenger(dataProvider);
        }

        public ReadOnlyReactiveProperty<bool> IsConnected => _messenger.IsConnected;

        public Observable<T> Observe<T>(string eventName)
        {
            return _messenger.Observe<T>(eventName);
        }

        public Observable<T> Observe<T>(string eventName, Func<JsonElement, T> parser)
        {
            return _messenger.Observe(eventName, parser);
        }

        public UniTask<bool> SendDataAsync<T>(string eventName, T data, CancellationToken ct = default)
        {
            return _messenger.SendDataAsync(eventName, data, ct);
        }

        public UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(string eventName, TRequest data, CancellationToken ct = default) where TResponse : class
        {
            return _messenger.SendDataAndWaitAsync<TRequest, TResponse>(eventName, data, ct);
        }

        protected override UniTask InitAsync()
        {
            return _messenger.ConnectAsync();
        }

        protected override void SafeDispose()
        {
            _messenger.Dispose();
        }
    }
}
