using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;

namespace O2un.Core.Network
{
    public sealed class NetworkClient : IDisposable
    {
        private readonly NetworkSystemConfig _config;
        private WebSocketClient _webSocket;
        private CancellationTokenSource _cts;
        
        private bool _isDisposed;
        private bool _isReconnecting;

        public bool IsConnected => _webSocket != null && _webSocket.IsConnected;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<ReadOnlyMemory<byte>> OnRawMessageReceived;

        public NetworkClient(NetworkSystemConfig config)
        {
            _config = config;
        }
        
        public async UniTask ConnectAsync()
        {
            if (IsConnected || _isReconnecting) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            await ConnectInternalAsync(_cts.Token);
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

        private void HandleDisconnected(string reason)
        {
            TriggerReconnect();
        }

        private void HandleError(string error)
        {
            TriggerReconnect();
        }

        private void TriggerReconnect()
        {
            OnDisconnected.RunOnMainThread();
            _=ReconnectWithBackoffAsync();
        }

        private async UniTask ReconnectWithBackoffAsync()
        {
            if (_isDisposed || _isReconnecting) return;
            _isReconnecting = true;

            int attempt = 0;
            int maxDelay = 15000;

            while (!_isDisposed && !IsConnected)
            {
                attempt++;
                int delay = Math.Min((int)Math.Pow(2, attempt) * 1000, maxDelay);
                
                try
                {
                    await UniTask.Delay(delay, cancellationToken: _cts.Token);
                    await ConnectInternalAsync(_cts.Token);
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

        public void Dispose()
        {
            _isDisposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _webSocket?.Dispose();
        }
    }
}
