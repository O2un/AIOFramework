using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Network;
using R3;

namespace O2un
{
    public sealed class GameStartP2PModule
    {
        private const NetworkPacketId EVENT_ID = NetworkPacketId.GameStart;

        private readonly IMultiplayerPacketContract _packets;

        public GameStartP2PModule(IMultiplayerPacketContract packets)
        {
            _packets = packets ?? throw new ArgumentNullException(nameof(packets));
        }

        // 세션이 없는 동안은 빈 스트림이다. 방에 들어간 뒤에 구독해야 실제 수신이 붙는다.
        public Observable<GameStartPayload> Received => _packets.Observe<GameStartPayload>(EVENT_ID);

        public UniTask<bool> BroadcastAsync(CancellationToken ct = default)
        {
            return _packets.SendDataAsync(EVENT_ID, NetworkPacketType.Broadcast, new GameStartPayload(), ct);
        }
    }
}
