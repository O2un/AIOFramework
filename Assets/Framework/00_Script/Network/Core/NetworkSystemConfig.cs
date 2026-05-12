using O2un.Core;

public class NetworkSystemConfig : GlobalConfig<NetworkSystemConfig>
{
    public string ServerUrl = "ws://localhost:8080";
    public int TimeoutSeconds = 5;
    public int ReconnectDelayMs = 3000;
}