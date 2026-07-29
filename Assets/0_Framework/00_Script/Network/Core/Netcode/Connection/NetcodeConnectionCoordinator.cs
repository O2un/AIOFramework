using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
                _active = null;
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
        }

        private void HandleNetworkIdChanged(int networkId)
        {
            if (true == IsDisposed)
            {
                return;
            }

            _networkId.Value = networkId;
        }
    }
}
