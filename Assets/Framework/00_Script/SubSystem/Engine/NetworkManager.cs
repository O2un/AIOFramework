using System;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;

namespace O2un.Core.Network
{
    public struct NetworkPacket<T>
    {
        public string Event { get; set; }
        public T Data { get; set; }
    }

    public partial class NetworkManager : ServiceSubsystemBase
    {
        private NetworkSystemConfig _config;
        private NetworkClient _client;
        private NetworkRouter _router;
        private NetworkRequestTracker _requestTracker;

        public bool IsConnected => _client != null && _client.IsConnected;

        protected override void Init()
        {
            _config = NetworkSystemConfig.LoadRuntime();
        
            _router = new NetworkRouter();
            _requestTracker = new NetworkRequestTracker();
            
            _client = new(_config);
            _client.OnRawMessageReceived += ProcessIncomingRawData;
            _=_client.ConnectAsync();
        }

        public override void ClearAll()
        {
            _client?.Dispose();
            _router?.Clear();
            _requestTracker?.Clear();
        }

        public void Subscribe<T>(string eventName, Action<T> handler)
        {
            _router.Subscribe(eventName, handler);
        }

        public void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            _router.Unsubscribe(eventName, handler);
        }

        public void SendData<T>(string eventName, T data)
        {
            if (!IsConnected) return;

            var packet = new NetworkPacket<T> { Event = eventName, Data = data };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(packet);
            _=_client.SendAsync(bytes);
        }

        public async UniTask<(bool isSuccess, TResponse response)> SendDataAndWaitAsync<TRequest, TResponse>(string eventName, TRequest data)
        {
            string waitEventName = $"{eventName}Result";
            
            if (!IsConnected) return (false, default);

            var waitTask = _requestTracker.CreateWaitTask<TResponse>(waitEventName);
            SendData(eventName, data);

            var (isResponseReceived, response) = await UniTask.WhenAny(
                waitTask, 
                UniTask.Delay(TimeSpan.FromSeconds(_config.TimeoutSeconds))
            );

            if (isResponseReceived)
            {
                return (true, response);
            }
            
            _requestTracker.RemoveTask(waitEventName);
            return (false, default);
        }

        private void ProcessIncomingRawData(ReadOnlyMemory<byte> rawData)
        {
            try
            {
                var reader = new Utf8JsonReader(rawData.Span);
                string eventName = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("event"))
                    {
                        reader.Read();
                        eventName = reader.GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(eventName)) return;

                using var document = JsonDocument.Parse(rawData);
                if (document.RootElement.TryGetProperty("data", out var dataElement))
                {
                    bool isResponse = _requestTracker.TryCompleteTask(eventName, dataElement);

                    if (!isResponse)
                    {
                        _router.Route(eventName, dataElement);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error,$"Message Processing Error: {ex.Message}");
            }
        }
    }
}
