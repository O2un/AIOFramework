using System;
using O2un.Utils;

namespace O2un.Core.Network
{
    public sealed class DedicatedServerArguments
    {
        public string MatchmakingAddress { get; private set; }
        public string ServerId { get; private set; }
        public string BindAddress { get; private set; }
        public ushort Port { get; private set; }
        public string AllocationId { get; private set; }

        public static DedicatedServerArguments Parse(string[] arguments)
        {
            return new DedicatedServerArguments
            {
                MatchmakingAddress = CommandLine.GetArgument(arguments, "-matchmaking", "ws://127.0.0.1:8080"),
                ServerId = CommandLine.GetArgument(arguments, "-serverId", Guid.NewGuid().ToString("N")),
                BindAddress = CommandLine.GetArgument(arguments, "-bindAddress", NetcodeTransportConnector.ANY_ADDRESS),
                Port = CommandLine.GetUShortArgument(arguments, "-port", 7979),
                AllocationId = CommandLine.GetArgument(arguments, "-allocationId", string.Empty),
            };
        }
    }
}
