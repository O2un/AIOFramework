using R3;
using Unity.Entities;

namespace O2un.Core.Network
{
    public static class NetcodeP2PTransportFactory
    {
        /// <summary>
        /// Client World 가 없는 Dedicated Server 는 세션 Transport 를 갖지 않으므로 null 을 돌려준다.
        /// </summary>
        public static IP2PTransport TryCreate(NetcodeWorldProvider worldProvider, ReadOnlyReactiveProperty<int> networkId)
        {
            if (null == worldProvider)
            {
                return null;
            }

            World clientWorld = worldProvider.GetClientWorld();

            if (null == clientWorld || false == clientWorld.IsCreated)
            {
                return null;
            }

            return new NetcodeP2PTransport(clientWorld, networkId);
        }
    }
}
