using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using R3;
using VContainer.Unity;

namespace O2un.Core.Network
{
    public sealed class NetcodeConnectionCoordinator : SafeDisposableClass, IInitializable
    {
        private readonly NetcodeWorldProvider _worldProvider;
        private readonly NetcodeTransportConnector _connector;

        public NetcodeConnectionCoordinator(NetcodeWorldProvider worldProvider, NetcodeTransportConnector connector)
        {
            _worldProvider = worldProvider;
            _connector = connector;
        }

        private readonly Dictionary<MultiplayerRole, INetcodeConnectionModule> _modules = new();
        private readonly ReactiveProperty<int> _networkId = new(0);
        private INetcodeConnectionModule _active;

        public ReadOnlyReactiveProperty<int> NetworkId => _networkId;
        public bool HasActiveConnection => null != _active;

        public void Initialize()
        {
            _modules.Add(MultiplayerRole.Host, new HostConnectionModule(_worldProvider, _connector));
            _modules.Add(MultiplayerRole.Client, new ClientConnectionModule(_worldProvider, _connector));
            _modules.Add(MultiplayerRole.DedicatedServer, new DedicatedServerConnectionModule(_worldProvider, _connector));

            NetcodeConnectionBridge.NetworkIdChanged += HandleNetworkIdChanged;
        }

        protected override void SafeDispose()
        {
            NetcodeConnectionBridge.NetworkIdChanged -= HandleNetworkIdChanged;
            HostAuthority.Clear();
            _networkId.Dispose();

            base.SafeDispose();
        }

        public async UniTask ConnectAsync(MatchConnectionInfo connectionInfo, CancellationToken ct = default)
        {
            if (null == connectionInfo)
            {
                throw new ArgumentNullException(nameof(connectionInfo));
            }

            // 로비에서 버튼을 두 번 누르는 경우가 흔하다. 두 번째 요청이 World 를 덮어쓰면 첫 연결이 어긋난다.
            if (true == HasActiveConnection)
            {
                throw new InvalidOperationException("[NetcodeConnectionCoordinator] 이미 활성 연결이 있다.");
            }

            if (false == _modules.TryGetValue(connectionInfo.Role, out var module))
            {
                throw new InvalidOperationException($"[NetcodeConnectionCoordinator] 역할에 맞는 모듈이 없다. role={connectionInfo.Role}");
            }

            _active = module;

            try
            {
                await module.ConnectAsync(connectionInfo, ct);
            }
            catch
            {
                try
                {
                    await module.DisconnectAsync(CancellationToken.None);
                }
                catch (Exception e)
                {
                    Log.Print(Log.LogLevel.Error, $"[NetcodeConnectionCoordinator] 연결 실패 정리 중 오류가 발생했다. error={e.Message}", Log.LogFilter.Server);
                }

                _active = null;
                _networkId.Value = 0;
                HostAuthority.Clear();
                throw;
            }
        }

        public async UniTask DisconnectAsync(CancellationToken ct = default)
        {
            if (false == HasActiveConnection)
            {
                return;
            }

            var module = _active;
            _active = null;

            await module.DisconnectAsync(ct);

            _networkId.Value = 0;
            HostAuthority.Clear();
        }

        private void HandleNetworkIdChanged(int networkId)
        {
            if (true == IsDisposed)
            {
                return;
            }

            _networkId.Value = networkId;

            // Server World 에는 Host 가 누구인지 알 수단이 없다. 같은 프로세스의 Host 만 자기 NetworkId 를 알려줄 수 있다.
            if (MultiplayerRole.Host != _active?.Role)
            {
                return;
            }

            HostAuthority.SetHostPeer(new P2PPeerId((ulong)networkId));
        }
    }
}
