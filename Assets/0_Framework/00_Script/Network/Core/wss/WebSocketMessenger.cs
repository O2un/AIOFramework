using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Data;
using O2un.Core.Utils;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 전송 통로·용도와 무관하게 모든 JSON 패킷이 쓰는 공통 봉투.
    /// </summary>
    public struct NetworkPacket<T>
    {
        // 기본 직렬화는 PascalCase 라, 명시하지 않으면 보내는 쪽만 조용히 어긋난다.
        [JsonPropertyName("event")] public string Event { get; set; }
        [JsonPropertyName("uniqueKey")] public string UniqueKey { get; set; }
        [JsonPropertyName("data")] public T Data { get; set; }
    }

    public sealed class WebSocketMessenger : NetworkMessengerBase<string, JsonElement>, INetworkMessenger
    {
        private const string ACK_POSTFIX = "Ack";
        private const string CONNECTION_READY_EVENT = "connectionReady";

        private sealed class ConnectionReadyData
        {
            public bool IsReady { get; set; }
        }

        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private readonly NetworkRuntimeData _config;
        private readonly NetworkClient _client;

        private readonly ReactiveProperty<bool> _isConnected = new(false);
        public ReadOnlyReactiveProperty<bool> IsConnected => _isConnected;

        public WebSocketMessenger(IRuntimeDataProvider dataProvider)
        {
            _config = dataProvider.Get<NetworkRuntimeData>();

            ObserveEvent<ConnectionReadyData>(CONNECTION_READY_EVENT)
                .Subscribe(data => _isConnected.Value = data.IsReady)
                .AddTo(DisposableR3);

            _client = new(_config);
            _client.OnDisconnected
                .Subscribe(_ => _isConnected.Value = false)
                .AddTo(DisposableR3);
            _client.OnRawMessageReceived
                .Subscribe(ProcessIncomingRawData)
                .AddTo(DisposableR3);
        }

        public UniTask ConnectAsync()
        {
            ThrowIfDisposed();
            return _client.TryConnect();
        }

        protected override bool IsEventIdValid(string eventId)
        {
            return false == string.IsNullOrEmpty(eventId);
        }

        protected override T Deserialize<T>(JsonElement payload)
        {
            return payload.CommonOptionDeserialize<T>();
        }

        protected override void SafeDispose()
        {
            base.SafeDispose();
            _client?.Dispose();
            _sendLock.Dispose();
            _isConnected.Dispose();
        }

        public Observable<T> Observe<T>(string eventName)
        {
            return ObserveEvent<T>(eventName);
        }

        public Observable<T> Observe<T>(string eventName, Func<JsonElement, T> parser)
        {
            return ObserveEvent(eventName, parser);
        }

        public async UniTask<bool> SendDataAsync<T>(string eventName, T data, CancellationToken ct = default)
        {
            ulong uniqueKey = CreateSequence();
            return await SendDataAsync(eventName, uniqueKey, data, ct);
        }

        private async UniTask<bool> SendDataAsync<T>(string eventName, ulong uniqueKey, T data, CancellationToken ct)
        {
            if (false == _isConnected.Value)
            {
                return false;
            }

            byte[] bytes;

            try
            {
                var packet = new NetworkPacket<T>
                {
                    Event = eventName,
                    UniqueKey = uniqueKey.ToString(CultureInfo.InvariantCulture),
                    Data = data,
                };

                // 서버는 payload 키를 camelCase 로 읽는다. 옵션 없이 직렬화하면 봉투만 맞고 안쪽이 어긋난다.
                bytes = JsonSerializer.SerializeToUtf8Bytes(packet, NetworkJson.Options);
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error, $"직렬화 실패. event={eventName}, error={ex.Message}");
                return false;
            }

            // ClientWebSocket 은 동시 송신을 하나로 제한한다. 겹치면 예외가 난다.
            await _sendLock.WaitAsync(ct);

            try
            {
                if (false == _isConnected.Value)
                {
                    return false;
                }

                return await _client.SendAsync(bytes, ct: ct);
            }
            catch (OperationCanceledException)
            {
                // 취소·시간 초과는 호출자가 판정한다. 여기서 송신 실패로 기록하면 같은 사건이 두 번 남는다.
                throw;
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error, $"송신 실패. event={eventName}, error={ex.Message}");
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// 응답을 받지 못하면 null 이다. "서버가 거절함"은 null 이 아니라 응답 본문의 실패 플래그로 온다.
        /// </summary>
        public async UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(string eventName, TRequest data, CancellationToken ct = default) where TResponse : class
        {
            if (false == _isConnected.Value)
            {
                return null;
            }

            string waitEventName = string.Concat(eventName, ACK_POSTFIX);

            try
            {
                return await RequestAsync<TResponse>(
                    waitEventName,
                    TimeSpan.FromSeconds(_config.TimeoutSeconds),
                    (uniqueKey, token) => SendDataAsync(eventName, uniqueKey, data, token),
                    ct);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is NetworkSendFailedException || ex is OperationCanceledException)
            {
                // 응답 없음·송신 실패·취소는 이 API가 null 로 표현하는 정상 결과다.
                return null;
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error, $"요청 실패. event={eventName}, error={ex.Message}", exception: ex);
                return null;
            }
        }

        private void ProcessIncomingRawData(ReadOnlyMemory<byte> rawData)
        {
            try
            {
                if (false == TryParsePacket(rawData, out string eventName, out ulong? uniqueKey, out JsonElement payload))
                {
                    Log.Print(Log.LogLevel.Warning, "봉투 형식이 아닌 패킷을 버렸다.");
                    return;
                }

                NetworkDispatchResult result = Dispatch(uniqueKey.GetValueOrDefault(), eventName, payload);
                if (NetworkDispatchResult.ResponseCompleted == result)
                {
                    return;
                }

                if (NetworkDispatchResult.ResponseEventMismatch == result)
                {
                    Log.Print(Log.LogLevel.Warning, $"예상하지 않은 응답 이벤트를 버렸다. uniqueKey={uniqueKey.Value}, event={eventName}");
                }
            }
            catch (JsonException ex)
            {
                Log.Print(Log.LogLevel.Error, $"JSON 파싱 실패. error={ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error, $"패킷 처리 실패. error={ex.Message}");
            }
        }

        private bool TryParsePacket(ReadOnlyMemory<byte> rawData, out string eventName, out ulong? uniqueKey, out JsonElement payload)
        {
            eventName = null;
            uniqueKey = null;
            payload = default;

            using var document = JsonDocument.Parse(rawData);
            JsonElement root = document.RootElement;

            if (JsonValueKind.Object != root.ValueKind)
            {
                return false;
            }

            if (false == root.TryGetProperty("event", out JsonElement eventElement))
            {
                return false;
            }
            if (JsonValueKind.String != eventElement.ValueKind)
            {
                return false;
            }

            eventName = eventElement.GetString();
            if (string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            if (root.TryGetProperty("uniqueKey", out JsonElement uniqueKeyElement))
            {
                if (JsonValueKind.String != uniqueKeyElement.ValueKind)
                {
                    return false;
                }
                if (false == ulong.TryParse(uniqueKeyElement.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedUniqueKey))
                {
                    return false;
                }
                uniqueKey = parsedUniqueKey;
            }

            if (false == root.TryGetProperty("data", out JsonElement dataElement))
            {
                return false;
            }

            // JsonDocument 를 여기서 버리므로 Clone 없이 넘기면 해제된 메모리를 읽는다.
            payload = dataElement.Clone();
            return true;
        }
    }
}
