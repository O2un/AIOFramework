using System;

namespace O2un.Core.Network
{
    /// <summary>
    /// Host Handler가 해석할 처리 의도이며 송신 대상을 직접 결정하지 않는다.
    /// </summary>
    public enum NetworkPacketType
    {
        None = 0,
        Broadcast = 1,
        Echo = 2,
    }

    public static class NetworkPacketTypeExtensions
    {
        public static bool IsDefined(this NetworkPacketType packetType)
        {
            return NetworkPacketType.None != packetType && Enum.IsDefined(typeof(NetworkPacketType), packetType);
        }
    }
}
