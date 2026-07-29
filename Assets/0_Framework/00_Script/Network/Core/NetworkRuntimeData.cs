using O2un.Core.Data;
using UnityEngine;

namespace O2un.Core.Network
{
    public sealed class NetworkRuntimeData : RuntimeData<NetworkRuntimeData>
    {
        public override string Key => nameof(NetworkRuntimeData);

        [SerializeField] private string _serverUrl = "ws://localhost:8080";
        [SerializeField] private int _timeoutSeconds = 5;
        [SerializeField] private int _reconnectDelayMs = 3000;
        [SerializeField] private ushort _multiplayerPort = 7777;

        public string ServerUrl => _serverUrl;
        public int TimeoutSeconds => _timeoutSeconds;
        public int ReconnectDelayMs => _reconnectDelayMs;
        public ushort MultiplayerPort => _multiplayerPort;
    }
}
