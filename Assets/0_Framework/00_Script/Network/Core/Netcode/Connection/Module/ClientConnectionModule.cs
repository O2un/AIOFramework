using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    public sealed class ClientConnectionModule : INetcodeConnectionModule
    {
        private readonly NetcodeWorldProvider _worldProvider;
        private readonly NetcodeTransportConnector _connector;

        public MultiplayerRole Role => MultiplayerRole.Client;

        public ClientConnectionModule(NetcodeWorldProvider worldProvider, NetcodeTransportConnector connector)
        {
            _worldProvider = worldProvider;
            _connector = connector;
        }

        public UniTask ConnectAsync(MatchConnectionInfo connectionInfo, CancellationToken ct)
        {
            var clientWorld = _worldProvider.CreateClientWorld(NetcodeWorldProvider.CLIENT_WORLD_NAME);

            _connector.Connect(clientWorld, connectionInfo.Address, connectionInfo.Port);

            return UniTask.CompletedTask;
        }

        public UniTask DisconnectAsync(CancellationToken ct)
        {
            _worldProvider.DisposeClientWorld();
            return UniTask.CompletedTask;
        }
    }
}
