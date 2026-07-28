using O2un.Core;
using UnityEngine;

public class NetworkSystemConfig : GlobalConfig<NetworkSystemConfig>
{
    [SerializeField] private string _serverUrl = "ws://localhost:8080";
    [SerializeField] private int _timeoutSeconds = 5;
    [SerializeField] private int _reconnectDelayMs = 3000;
    [SerializeField] private int _netCode = 7777;

    public string ServerUrl => _serverUrl;
    public int TimeoutSeconds => _timeoutSeconds;
    public int ReconnectDelayMs => _reconnectDelayMs;
    public int NetCode => _netCode;
}