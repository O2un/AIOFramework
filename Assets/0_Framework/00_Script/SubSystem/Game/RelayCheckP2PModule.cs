using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Network;
using R3;

namespace O2un
{
    /// <summary>
    /// RelayCheck 를 쓰는 쪽이 보는 통로다. Event ID 와 Packet Type 선택을 여기서 끝내고 밖에는 payload 만 보인다.
    /// </summary>
    public sealed class RelayCheckP2PModule
    {
        private const NetworkPacketId EVENT_ID = NetworkPacketId.DebugRelayCheckPing;

        private readonly IMultiplayerPacketContract _packets;

        public RelayCheckP2PModule(IMultiplayerPacketContract packets)
        {
            _packets = packets ?? throw new ArgumentNullException(nameof(packets));
        }

        // 세션이 없는 동안은 빈 스트림이다. 방에 들어간 뒤에 구독해야 실제 수신이 붙는다.
        public Observable<RelayCheckPayload> Received => _packets.Observe<RelayCheckPayload>(EVENT_ID);

        public UniTask<bool> BroadcastAsync(string message, CancellationToken ct = default)
        {
            return _packets.SendDataAsync(EVENT_ID, NetworkPacketType.Broadcast, new RelayCheckPayload { Message = message }, ct);
        }

        public UniTask<P2PPacketResult<RelayCheckPayload>> EchoAsync(string message, TimeSpan timeout, CancellationToken ct = default)
        {
            return _packets.SendDataAndWaitAsync<RelayCheckPayload, RelayCheckPayload>(
                EVENT_ID,
                NetworkPacketType.Echo,
                new RelayCheckPayload { Message = message },
                timeout,
                ct);
        }
    }
}
