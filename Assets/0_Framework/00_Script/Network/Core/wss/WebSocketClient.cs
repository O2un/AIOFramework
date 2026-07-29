using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 전송 계층. 봉투를 해석하지 않고 완성된 메시지만 위로 올린다 — 파서를 통로에서 떼어 두기 위한 경계다.
    /// </summary>
    public sealed class WebSocketClient : SafeDisposableClass
    {
        private ClientWebSocket _webSocket;
        private readonly byte[] _receiveBuffer;
        private readonly Memory<byte> _receiveMemory;

        private AsyncHandle _receiveHandle;

        private readonly Subject<Unit> _onConnected = new();
        private readonly Subject<string> _onDisconnected = new();
        private readonly Subject<string> _onError = new();
        private readonly Subject<ReadOnlyMemory<byte>> _onRawMessageReceived = new();
        public Observable<Unit> OnConnected => _onConnected;
        public Observable<string> OnDisconnected => _onDisconnected;
        public Observable<string> OnError => _onError;
        public Observable<ReadOnlyMemory<byte>> OnRawMessageReceived => _onRawMessageReceived;

        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        public WebSocketClient(int bufferSize = 8192)
        {
            _receiveBuffer = new byte[bufferSize];
            _receiveMemory = new(_receiveBuffer);
        }

        public async UniTask ConnectAsync(string uri, CancellationToken ct)
        {
            if (IsConnected) return;

            _webSocket = new ClientWebSocket();

            try
            {
                await _webSocket.ConnectAsync(new Uri(uri), ct);
                _onConnected.OnNext(Unit.Default);

                _receiveHandle = this.StartAsync(async innerCt =>
                {
                    await ReceiveLoopAsync(innerCt);
                });
            }
            catch (Exception ex)
            {
                _onError.OnNext(ex.Message);
            }
        }

        public async UniTask DisconnectAsync()
        {
            if (_webSocket == null) return;

            try
            {
                _receiveHandle.Dispose();
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _onError.OnNext(ex.Message);
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
                _onDisconnected.OnNext(string.Empty);
            }
        }

        public async UniTask<bool> SendAsync(ReadOnlyMemory<byte> data, WebSocketMessageType messageType = WebSocketMessageType.Text, CancellationToken ct = default)
        {
            if (false == IsConnected) return false;
            await _webSocket.SendAsync(data, messageType, true, ct);
            return true;
        }

        private async UniTask ReceiveLoopAsync(CancellationToken ct)
        {
            var messageBuffer = new ArrayBufferWriter<byte>();

            try
            {
                while (false == ct.IsCancellationRequested && IsConnected)
                {
                    ValueWebSocketReceiveResult result;

                    // 버퍼보다 큰 메시지는 여러 번에 걸쳐 온다. 조각을 모으지 않으면 앞부분이 조용히 사라진다.
                    do
                    {
                        result = await _webSocket.ReceiveAsync(_receiveMemory, ct);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await DisconnectAsync();
                            return;
                        }

                        messageBuffer.Write(_receiveMemory.Span.Slice(0, result.Count));
                    }
                    while (false == result.EndOfMessage);

                    ProcessMessage(messageBuffer.WrittenMemory);
                    messageBuffer.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // NULL
            }
            catch (Exception ex)
            {
                _onError.OnNext(ex.Message);
                await DisconnectAsync();
            }
        }

        private void ProcessMessage(ReadOnlyMemory<byte> message)
        {
            // 수신 버퍼는 다음 루프에서 덮어쓰이는데 구독자는 메인 스레드로 미뤄 실행되므로 복사해서 넘긴다.
            byte[] copy = message.ToArray();
            _onRawMessageReceived.OnNext(copy);
        }

        protected override void SafeDispose()
        {
            _webSocket?.Dispose();
            _onConnected.Dispose();
            _onDisconnected.Dispose();
            _onError.Dispose();
            _onRawMessageReceived.Dispose();
        }
    }
}
