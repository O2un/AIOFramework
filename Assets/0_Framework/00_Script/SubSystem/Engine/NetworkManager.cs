using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Cysharp.Threading.Tasks;
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

    public sealed class NetworkManager : EngineSubsystemBase
    {
        private const string ACK_POSTFIX = "Ack";
        private const string CONNECTION_READY_EVENT = "connectionReady";

        private sealed class ConnectionReadyData
        {
            public bool IsReady { get; set; }
        }

        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly object _uniqueKeyLock = new();

        private NetworkSystemConfig _config;
        private NetworkClient _client;
        private NetworkRouter _router;
        private NetworkRequestTracker _requestTracker;
        private ulong _nextUniqueKey;

        private readonly ReactiveProperty<bool> _isConnected = new(false);
        public ReadOnlyReactiveProperty<bool> IsConnected => _isConnected;

        protected override async UniTask InitAsync()
        {
            _config = NetworkSystemConfig.LoadRuntime();

            _router = new NetworkRouter();
            _requestTracker = new NetworkRequestTracker();
            _router.Observe<ConnectionReadyData>(CONNECTION_READY_EVENT)
                .Subscribe(data => _isConnected.Value = data.IsReady)
                .AddTo(DisposableR3);

            _client = new(_config);
            _client.OnDisconnected
                .Subscribe(_ => _isConnected.Value = false)
                .AddTo(DisposableR3);
            _client.OnRawMessageReceived
                .Subscribe(ProcessIncomingRawData)
                .AddTo(DisposableR3);
            await _client.TryConnect();
        }

        protected override void SafeDispose()
        {
            _client?.Dispose();
            _router?.Dispose();
            _requestTracker?.Clear();
            _sendLock.Dispose();
            _isConnected.Dispose();
        }

        public Observable<T> Observe<T>(string eventName)
        {
            return _router.Observe<T>(eventName);
        }

        public Observable<T> Observe<T>(string eventName, Func<JsonElement, T> parser)
        {
            return _router.Observe(eventName, parser);
        }

        public async UniTask<bool> SendDataAsync<T>(string eventName, T data, CancellationToken ct = default)
        {
            ulong uniqueKey = CreateUniqueKey();
            return await SendDataAsync(eventName, uniqueKey, data, ct);
        }

        private async UniTask<bool> SendDataAsync<T>(string eventName, ulong uniqueKey, T data, CancellationToken ct)
        {
            if (false == _isConnected.Value) return false;

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
                if (false == _isConnected.Value) return false;

                return await _client.SendAsync(bytes, ct: ct);
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
        public async UniTask<TResponse> SendDataAndWaitAsync<TRequest, TResponse>(string eventName, TRequest data, CancellationToken ct = default)
            where TResponse : class
        {
            if (false == _isConnected.Value) return null;

            string waitEventName = string.Concat(eventName, ACK_POSTFIX);
            ulong uniqueKey = CreateUniqueKey();

            // 송신보다 먼저 등록해야 즉시 도착한 응답을 놓치지 않는다.
            var waitTask = _requestTracker.CreateWaitTask<TResponse>(uniqueKey, waitEventName);

            try
            {
                bool isSent = await SendDataAsync(eventName, uniqueKey, data, ct);
                if (false == isSent) return null;

                var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(_config.TimeoutSeconds), cancellationToken: ct);
                var (isResponseReceived, response) = await UniTask.WhenAny(waitTask, timeoutTask);

                return isResponseReceived ? response : null;
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error, $"요청 실패. event={eventName}, error={ex.Message}", exception: ex);
                return null;
            }
            finally
            {
                _requestTracker.RemoveTask(uniqueKey);
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

                if (true == uniqueKey.HasValue)
                {
                    bool isCompleted = _requestTracker.TryCompleteTask(uniqueKey.Value, eventName, payload, out bool hasWaiter);
                    if (true == isCompleted)
                    {
                        return;
                    }

                    if (true == hasWaiter)
                    {
                        Log.Print(Log.LogLevel.Warning, $"예상하지 않은 응답 이벤트를 버렸다. uniqueKey={uniqueKey.Value}, event={eventName}");
                        return;
                    }
                }

                _router.Route(eventName, payload);
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

            if (JsonValueKind.Object != root.ValueKind) return false;

            if (false == root.TryGetProperty("event", out JsonElement eventElement)) return false;
            if (JsonValueKind.String != eventElement.ValueKind) return false;

            eventName = eventElement.GetString();
            if (string.IsNullOrEmpty(eventName)) return false;

            if (root.TryGetProperty("uniqueKey", out JsonElement uniqueKeyElement))
            {
                if (JsonValueKind.String != uniqueKeyElement.ValueKind) return false;
                if (false == ulong.TryParse(uniqueKeyElement.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedUniqueKey)) return false;
                uniqueKey = parsedUniqueKey;
            }

            if (false == root.TryGetProperty("data", out JsonElement dataElement)) return false;

            // JsonDocument 를 여기서 버리므로 Clone 없이 넘기면 해제된 메모리를 읽는다.
            payload = dataElement.Clone();
            return true;
        }

        private ulong CreateUniqueKey()
        {
            lock (_uniqueKeyLock)
            {
                _nextUniqueKey++;
                return _nextUniqueKey;
            }
        }
    }
}
