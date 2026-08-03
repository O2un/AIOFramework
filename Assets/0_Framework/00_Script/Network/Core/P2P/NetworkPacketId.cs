using System;

namespace O2un.Core.Network
{
    /// <summary>
    /// 배포 뒤 값의 삭제·재정렬·중간 삽입을 금지하는 P2P wire Event ID다.
    /// </summary>
    public enum NetworkPacketId
    {
        None = 0,
        GameStart = 1,
        DebugRelayCheckPing = 1000001,
    }

    public static class NetworkPacketIdExtensions
    {
        public static bool IsDefined(this NetworkPacketId eventId)
        {
            return NetworkPacketId.None != eventId && Enum.IsDefined(typeof(NetworkPacketId), eventId);
        }
    }
}
