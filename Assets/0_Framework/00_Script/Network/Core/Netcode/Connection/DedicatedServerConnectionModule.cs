using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    public sealed class DedicatedServerConnectionModule : INetcodeConnectionModule
    {
        private readonly NetcodeWorldProvider _worldProvider;
        private readonly NetcodeTransportConnector _connector;

        public MultiplayerRole Role => MultiplayerRole.DedicatedServer;

        public DedicatedServerConnectionModule(NetcodeWorldProvider worldProvider, NetcodeTransportConnector connector)
        {
            _worldProvider = worldProvider;
            _connector = connector;
        }

        public UniTask ConnectAsync(MatchConnectionInfo connectionInfo, CancellationToken ct)
        {
            // Bootstrap 이 이미 만들었으므로 검색만 한다. 여기서 또 만들면 World 생성 책임이 양쪽으로 갈린다.
            var serverWorld = _worldProvider.GetServerWorld();
            var arguments = GameClientServerBootstrap.DedicatedArguments;
            var bindAddress = null == arguments ? connectionInfo.Address : arguments.BindAddress;

            _connector.Listen(serverWorld, bindAddress, connectionInfo.Port);

            return UniTask.CompletedTask;
        }

        public UniTask DisconnectAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }
    }
}
