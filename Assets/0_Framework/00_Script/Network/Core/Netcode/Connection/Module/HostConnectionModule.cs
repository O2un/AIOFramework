using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    public sealed class HostConnectionModule : INetcodeConnectionModule
    {
        private readonly NetcodeWorldProvider _worldProvider;
        private readonly NetcodeTransportConnector _connector;

        public MultiplayerRole Role => MultiplayerRole.Host;

        public HostConnectionModule(NetcodeWorldProvider worldProvider, NetcodeTransportConnector connector)
        {
            _worldProvider = worldProvider;
            _connector = connector;
        }

        public UniTask ConnectAsync(MatchConnectionInfo connectionInfo, CancellationToken ct)
        {
            var serverWorld = _worldProvider.CreateServerWorld(NetcodeWorldProvider.SERVER_WORLD_NAME);
            var clientWorld = _worldProvider.CreateClientWorld(NetcodeWorldProvider.CLIENT_WORLD_NAME);

            _connector.Listen(serverWorld, NetcodeTransportConnector.ANY_ADDRESS, connectionInfo.Port);

            // 호스트도 원격 클라와 같은 경로를 밟게 한다. 서버 로직을 직접 호출하면 코드 경로가 갈려
            // 데디케이티드 전환이나 서버 권한 검증을 넣을 때 네트워크 인터페이스를 다시 갈아엎게 된다.
            _connector.Connect(clientWorld, NetcodeTransportConnector.LOOPBACK, connectionInfo.Port);

            return UniTask.CompletedTask;
        }

        public UniTask DisconnectAsync(CancellationToken ct)
        {
            _worldProvider.DisposeAll();
            return UniTask.CompletedTask;
        }
    }
}
