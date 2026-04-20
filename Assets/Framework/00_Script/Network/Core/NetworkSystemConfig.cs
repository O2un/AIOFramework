using O2un.Core;

public class NetworkSystemConfig : GlobalConfig<NetworkSystemConfig>
{
    public string ServerUrl = "https://ws.test.shotrack.ai";
    public int TimeoutSeconds = 5;
    public int ReconnectDelayMs = 3000;
}