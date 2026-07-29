using Unity.Entities;
using Unity.NetCode;

namespace O2un.Core.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    internal partial class NetworkIdMonitorSystem : SystemBase
    {
        private EntityQuery _query;
        private int _lastNetworkId;

        protected override void OnCreate()
        {
            _query = GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
        }

        protected override void OnUpdate()
        {
            var networkId = 0;

            if (false == _query.IsEmpty)
            {
                using var ids = _query.ToComponentDataArray<NetworkId>(Unity.Collections.Allocator.Temp);
                networkId = ids[0].Value;
            }

            if (networkId == _lastNetworkId)
            {
                return;
            }

            _lastNetworkId = networkId;
            NetcodeConnectionBridge.RaiseNetworkIdChanged(networkId);
        }
    }
}
