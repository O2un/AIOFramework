using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace O2un.Core.Network
{
    /// <summary>
    /// 방을 만들고 참가해 접속 정보를 얻는 데까지가 이 계층이다. 이 위로는 WebSocket 의 존재가 보이지 않는다.
    /// </summary>
    public interface IMatchmakingService
    {
        ReadOnlyReactiveProperty<bool> IsConnected { get; }
        MatchConnectionInfo CurrentConnection { get; }

        Observable<MatchConnectionInfo> MatchAssigned { get; }
        Observable<RoomState> RoomUpdated { get; }
        Observable<SessionClosedNotice> SessionClosed { get; }

        UniTask<bool> ConnectAsync(CancellationToken ct = default);

        UniTask<MatchmakingResult<MatchConnectionInfo>> CreateRoomAsync(string playerId, int maxPlayers, CancellationToken ct = default);
        UniTask<MatchmakingResult<MatchConnectionInfo>> JoinRoomAsync(string playerId, string roomCode, CancellationToken ct = default);
        UniTask<MatchmakingResult> LeaveRoomAsync(CancellationToken ct = default);
        UniTask<MatchmakingResult<RoomSummary[]>> GetRoomListAsync(CancellationToken ct = default);
    }
}
