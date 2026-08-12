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
        private const int RECONNECT_BASE_DELAY_MS = 2000;
        private const int RECONNECT_MAX_DELAY_MS = 15000;
        private const int RECONNECT_MAX_SHIFT = 3;

        private readonly NetworkRuntimeData _config;
        private WebSocketClient _webSocket;

        private bool _isReconnecting;
        public bool IsConnected => _webSocket != null && _webSocket.IsConnected;

        private readonly Subject<Unit> _onConnected = new();
        private readonly Subject<Unit> _onDisconnected = new();
        private readonly Subject<ReadOnlyMemory<byte>> _onRawMessageReceived = new();
        private readonly SerialDisposable _webSocketSubscriptions = new();
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
            // 이전 소켓을 버리기 전에 구독을 먼저 끊는다. 남겨두면 Dispose가 흘리는 OnDisconnected를 받아 재접속이 다시 돈다.
            _webSocketSubscriptions.Disposable = null;
            _webSocket?.Dispose();
            _webSocket = new();
            _webSocketSubscriptions.Disposable = SubscribeSocket(_webSocket);

            await _webSocket.ConnectAsync(_config.ServerUrl, ct);
        }

        // DisposableBuilder는 ref struct라 async 메서드 안에 둘 수 없어 분리했다.
        private IDisposable SubscribeSocket(WebSocketClient socket)
        {
            var builder = Disposable.CreateBuilder();

            socket.OnConnected
                .ObserveOnMainThread()
                .Subscribe(_ => HandleConnected())
                .AddTo(ref builder);
            socket.OnDisconnected
                .ObserveOnMainThread()
                .Subscribe(HandleDisconnected)
                .AddTo(ref builder);
            socket.OnError
                .ObserveOnMainThread()
                .Subscribe(HandleError)
                .AddTo(ref builder);
            socket.OnRawMessageReceived
                .ObserveOnMainThread()
                .Subscribe(HandleRawMessage)
                .AddTo(ref builder);

            return builder.Build();
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
                int shift = 0;

                while (false == IsDisposed && false == IsConnected)
                {
                    int delay = Math.Min(RECONNECT_BASE_DELAY_MS << shift, RECONNECT_MAX_DELAY_MS);

                    // 상한에 닿으면 증가를 멈춘다. 계속 올리면 int를 넘겨 delay가 음수가 되고 재접속이 매 프레임 돈다.
                    if (shift < RECONNECT_MAX_SHIFT)
                    {
                        shift++;
                    }

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
