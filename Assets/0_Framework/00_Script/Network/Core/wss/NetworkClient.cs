using System;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 연결 수명과 재접속을 담당한다. 봉투는 다루지 않고 raw 메시지를 그대로 위로 넘긴다.
    /// </summary>
    public sealed class NetworkClient : SafeDisposableClass
    {
        private readonly NetworkRuntimeData _config;
        private WebSocketClient _webSocket;

        private bool _isReconnecting;
        public bool IsConnected => _webSocket != null && _webSocket.IsConnected;

        private readonly Subject<Unit> _onConnected = new();
        private readonly Subject<Unit> _onDisconnected = new();
        private readonly Subject<ReadOnlyMemory<byte>> _onRawMessageReceived = new();
        private readonly CompositeDisposable _webSocketSubscriptions = new();
        public Observable<Unit> OnConnected => _onConnected;
        public Observable<Unit> OnDisconnected => _onDisconnected;
        public Observable<ReadOnlyMemory<byte>> OnRawMessageReceived => _onRawMessageReceived;

        public NetworkClient(NetworkRuntimeData config)
        {
            _config = config;
            _webSocketSubscriptions.AddTo(DisposableR3);
        }

        public async UniTask TryConnect()
        {
            if (IsConnected || _isReconnecting) return;

            var handle = this.StartExclusiveAsync("TryConnection", async ct =>
            {
                await ConnectInternalAsync(ct);
            });
            await handle;
        }

        private async UniTask ConnectInternalAsync(CancellationToken ct)
        {
            _webSocketSubscriptions.Clear();
            _webSocket?.Dispose();
            _webSocket = new();

            _webSocket.OnConnected
                .ObserveOnMainThread()
                .Subscribe(_ => HandleConnected())
                .AddTo(_webSocketSubscriptions);
            _webSocket.OnDisconnected
                .ObserveOnMainThread()
                .Subscribe(HandleDisconnected)
                .AddTo(_webSocketSubscriptions);
            _webSocket.OnError
                .ObserveOnMainThread()
                .Subscribe(HandleError)
                .AddTo(_webSocketSubscriptions);
            _webSocket.OnRawMessageReceived
                .ObserveOnMainThread()
                .Subscribe(HandleRawMessage)
                .AddTo(_webSocketSubscriptions);

            await _webSocket.ConnectAsync(_config.ServerUrl, ct);
        }

        private void HandleRawMessage(ReadOnlyMemory<byte> rawData)
        {
            if (IsDisposed)
            {
                return;
            }

            _onRawMessageReceived.OnNext(rawData);
        }

        private void HandleConnected()
        {
            if (IsDisposed)
            {
                return;
            }

            _isReconnecting = false;
            _onConnected.OnNext(Unit.Default);
        }

        private void HandleDisconnected(string reason) => TriggerReconnect();
        private void HandleError(string error) => TriggerReconnect();

        private void TriggerReconnect()
        {
            if (IsDisposed || _isReconnecting)
            {
                return;
            }

            _onDisconnected.OnNext(Unit.Default);
            this.StartExclusiveAsync("ReconnectLoop", async ct =>
            {
                await ReconnectWithBackoffAsync(ct);
            });
        }

        private async UniTask ReconnectWithBackoffAsync(CancellationToken ct)
        {
            if (IsDisposed || _isReconnecting) return;
            _isReconnecting = true;

            try
            {
                int attempt = 0;
                int maxDelay = 15000;

                while (false == IsDisposed && false == IsConnected)
                {
                    attempt++;
                    int delay = Math.Min((int)Math.Pow(2, attempt) * 1000, maxDelay);

                    await UniTask.Delay(delay, cancellationToken: ct);
                    await ConnectInternalAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // NULL
            }
        }

        public async UniTask<bool> SendAsync(ReadOnlyMemory<byte> payload, WebSocketMessageType messageType = WebSocketMessageType.Text, CancellationToken ct = default)
        {
            if (false == IsConnected)
            {
                return false;
            }

            return await _webSocket.SendAsync(payload, messageType, ct);
        }

        protected override void SafeDispose()
        {
            _webSocket?.Dispose();
            _onConnected.Dispose();
            _onDisconnected.Dispose();
            _onRawMessageReceived.Dispose();
        }
    }
}
