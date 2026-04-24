using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;

namespace O2un.Core.Network
{
    public sealed class NetworkClient : SafeDisposableClass
    {
        private readonly NetworkSystemConfig _config;
        private WebSocketClient _webSocket;
        
        private bool _isReconnecting;
        public bool IsConnected => _webSocket != null && _webSocket.IsConnected;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<ReadOnlyMemory<byte>> OnRawMessageReceived;

        public NetworkClient(NetworkSystemConfig config)
        {
            _config = config;
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
            _webSocket?.Dispose();
            _webSocket = new();

            _webSocket.OnConnected += HandleConnected;
            _webSocket.OnDisconnected += HandleDisconnected;
            _webSocket.OnError += HandleError;

            _webSocket.Subscribe("message", rawData => 
            {
                OnRawMessageReceived?.RunOnMainThread(rawData);
            });

            string endpoint = $"{_config.ServerUrl}?clientId={Guid.NewGuid()}";
            await _webSocket.ConnectAsync(endpoint, ct);
        }

        private void HandleConnected()
        {
            _isReconnecting = false;
            OnConnected?.RunOnMainThread();
        }

        private void HandleDisconnected(string reason) => TriggerReconnect();
        private void HandleError(string error) => TriggerReconnect();

        private void TriggerReconnect()
        {
            OnDisconnected.RunOnMainThread();
            this.StartExclusiveAsync("ReconnectLoop", async ct => 
            {
                await ReconnectWithBackoffAsync(ct);
            });
        }

        private async UniTask ReconnectWithBackoffAsync(CancellationToken ct)
        {
            if (IsDisposed || _isReconnecting) return;
            _isReconnecting = true;

            int attempt = 0;
            int maxDelay = 15000;

            while (!IsDisposed && !IsConnected)
            {
                attempt++;
                int delay = Math.Min((int)Math.Pow(2, attempt) * 1000, maxDelay);
                
                try
                {
                    await UniTask.Delay(delay, cancellationToken: ct);
                    if (ct.IsCancellationRequested) break;
                    await ConnectInternalAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        public async UniTask SendAsync(ReadOnlyMemory<byte> payload)
        {
            if (IsConnected)
            {
                await _webSocket.SendAsync(payload);
            }
        }

        protected override void SafeDispose()
        {
            _webSocket?.Dispose();
        }
    }
}
