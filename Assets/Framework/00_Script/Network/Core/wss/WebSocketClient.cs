using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;

namespace O2un.Core.Network
{
    public sealed class WebSocketClient : SafeDisposableClass
    {
        private ClientWebSocket _webSocket;
        private readonly byte[] _receiveBuffer;
        private readonly Memory<byte> _receiveMemory;
        
        private AsyncHandle _receiveHandle;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        private readonly Dictionary<string, Action<ReadOnlyMemory<byte>>> _eventHandlers;

        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        public WebSocketClient(int bufferSize = 8192)
        {
            _receiveBuffer = new byte[bufferSize];
            _receiveMemory = new(_receiveBuffer);
            _eventHandlers = new(StringComparer.Ordinal);
        }
        
        public async UniTask ConnectAsync(string uri, CancellationToken ct)
        {
            if (IsConnected) return;

            _webSocket = new ClientWebSocket();

            try
            {
                await _webSocket.ConnectAsync(new Uri(uri), ct);
                OnConnected?.Invoke();
                
                _receiveHandle = this.StartAsync(async innerCt => 
                {
                    await ReceiveLoopAsync(innerCt);
                });
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
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
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
                OnDisconnected?.Invoke(string.Empty);
            }
        }

        public void Subscribe(string eventName, Action<ReadOnlyMemory<byte>> handler)
        {
            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName] = handler;
            }
            else
            {
                _eventHandlers[eventName] += handler;
            }
        }

        public void Unsubscribe(string eventName, Action<ReadOnlyMemory<byte>> handler = null)
        {
            if (_eventHandlers.TryGetValue(eventName, out var existingHandler))
            {
                if (handler == null)
                {
                    _eventHandlers.Remove(eventName);
                }
                else
                {
                    existingHandler -= handler;
                    if (existingHandler == null)
                    {
                        _eventHandlers.Remove(eventName);
                    }
                    else
                    {
                        _eventHandlers[eventName] = existingHandler;
                    }
                }
            }
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> data, WebSocketMessageType messageType = WebSocketMessageType.Text)
        {
            if (!IsConnected) return;
            await _webSocket.SendAsync(data, messageType, true, CancellationToken.None);
        }

        private async UniTask ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    ValueWebSocketReceiveResult result = await _webSocket.ReceiveAsync(_receiveMemory, ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync();
                        break;
                    }

                    if (result.EndOfMessage)
                    {
                        ProcessMessage(_receiveMemory.Slice(0, result.Count));
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                await DisconnectAsync();
            }
        }

        private void ProcessMessage(ReadOnlyMemory<byte> message)
        {
            ParseProtocolMessage(message, out string eventName, out ReadOnlyMemory<byte> payload);

            if (!string.IsNullOrEmpty(eventName) && _eventHandlers.TryGetValue(eventName, out var handler))
            {
                handler?.Invoke(payload);
            }
        }

        private void ParseProtocolMessage(ReadOnlyMemory<byte> message, out string eventName, out ReadOnlyMemory<byte> payload)
        {
            eventName = string.Empty;
            payload = default;

            try
            {
                var reader = new Utf8JsonReader(message.Span);

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("event"))
                        {
                            reader.Read();
                            eventName = reader.GetString();
                        }
                        else if (reader.ValueTextEquals("data"))
                        {
                            reader.Read();
                            int startIndex = (int)reader.TokenStartIndex;
                            reader.Skip();
                            int length = (int)reader.BytesConsumed - startIndex;
                            
                            payload = message.Slice(startIndex, length);
                        }
                    }
                    if (!string.IsNullOrEmpty(eventName) && !payload.IsEmpty)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log.Print(Log.LogLevel.Error, $"파싱에러 : {ex.Message}");
            }
        }

        protected override void SafeDispose()
        {
            _webSocket?.Dispose();
            _eventHandlers.Clear();
        }
    }
}
