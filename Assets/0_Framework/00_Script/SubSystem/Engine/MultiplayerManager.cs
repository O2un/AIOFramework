using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Data;
using O2un.Core.Network;
using O2un.Core.Utils;
using R3;

namespace O2un
{
    /// <summary>
    /// 로비와 게임이 쓰는 유일한 진입점. World·Endpoint·역할은 이 아래로 감춘다.
    /// </summary>
    public interface IMultiplayerManager
    {
        INetcodeSessionSource Session { get; }

        UniTask<bool> CreateRoomAsync(string playerId, int maxPlayers, CancellationToken ct = default);
        UniTask<bool> JoinRoomAsync(string playerId, string roomCode, CancellationToken ct = default);
        UniTask LeaveAsync(CancellationToken ct = default);
    }

    // 연결 대기를 여기서 소유하는 이유는 SafeMono.OnDisable 이 DisposableR3.Clear() 를 호출해
    // UI 쪽에 둔 장기 대기가 비활성화 순간 조용히 취소되기 때문이다.
    public sealed class MultiplayerManager : EngineSubsystemBase, IMultiplayerManager
    {
        private readonly IMatchmakingService _matchmaking;
        private readonly IRuntimeDataProvider _dataProvider;

        public MultiplayerManager(IMatchmakingService matchmaking, IRuntimeDataProvider dataProvider)
        {
            _matchmaking = matchmaking;
            _dataProvider = dataProvider;
        }

        private NetcodeConnectionCoordinator _coordinator;

        private readonly NetcodeSessionRuntime _session = new();
        private NetworkRuntimeData _config;

        public INetcodeSessionSource Session => _session;

        protected override UniTask InitAsync()
        {
            _config = _dataProvider.Get<NetworkRuntimeData>();

            // 연결 계층은 이 매니저 밖에서 쓰는 곳이 없어 DI 에 올리지 않는다. 대신 수명을 여기서 통째로 쥔다.
            _coordinator = new NetcodeConnectionCoordinator(new NetcodeWorldProvider(), new NetcodeTransportConnector());
            _coordinator.Initialize();

            _matchmaking.RoomUpdated
                .Subscribe(HandleRoomUpdated)
                .AddTo(DisposableR3);
            _matchmaking.SessionClosed
                .Subscribe(_ => HandleSessionClosed())
                .AddTo(DisposableR3);
            _coordinator.NetworkId
                .Subscribe(_session.SetNetworkId)
                .AddTo(DisposableR3);

            return UniTask.CompletedTask;
        }

        protected override void SafeDispose()
        {
            // InitAsync 전에 폐기되면 아직 만들어지지 않았다.
            _coordinator?.Dispose();
            _session.Dispose();

            base.SafeDispose();
        }

        public UniTask<bool> CreateRoomAsync(string playerId, int maxPlayers, CancellationToken ct = default)
        {
            return RunAsync(NetcodeSessionState.CreatingRoom, token => _matchmaking.CreateRoomAsync(playerId, maxPlayers, token), ct);
        }

        public UniTask<bool> JoinRoomAsync(string playerId, string roomCode, CancellationToken ct = default)
        {
            return RunAsync(NetcodeSessionState.JoiningRoom, token => _matchmaking.JoinRoomAsync(playerId, roomCode, token), ct);
        }

        public async UniTask LeaveAsync(CancellationToken ct = default)
        {
            _session.Set(NetcodeSessionState.Disconnecting);

            await _matchmaking.LeaveRoomAsync(ct);
            await _coordinator.DisconnectAsync(ct);

            _session.Reset();
        }

        private async UniTask<bool> RunAsync(NetcodeSessionState matchmakingState, Func<CancellationToken, UniTask<MatchmakingResult<MatchConnectionInfo>>> request, CancellationToken ct)
        {
            if (true == _coordinator.HasActiveConnection)
            {
                return false;
            }

            try
            {
                _session.Set(NetcodeSessionState.ConnectingMatchmaking);

                if (false == await _matchmaking.ConnectAsync(ct))
                {
                    _session.Set(NetcodeSessionState.Failed);
                    return false;
                }

                _session.Set(matchmakingState);

                var result = await request(ct);

                if (false == result.IsSuccess)
                {
                    _session.Set(NetcodeSessionState.Failed);
                    return false;
                }

                _session.SetRoomCode(result.Value.RoomCode);
                _session.Set(NetcodeSessionState.StartingNetcode);

                await _coordinator.ConnectAsync(result.Value, ct);

                _session.Set(NetcodeSessionState.ConnectingNetcode);

                if (false == await WaitForNetworkIdAsync(ct))
                {
                    _session.Set(NetcodeSessionState.Failed);
                    await _coordinator.DisconnectAsync(ct);
                    return false;
                }

                _session.Set(NetcodeSessionState.Connected);
                _session.Set(NetcodeSessionState.InLobby);

                return true;
            }
            catch (OperationCanceledException)
            {
                _session.Set(NetcodeSessionState.Failed);
                return false;
            }
            catch (Exception e)
            {
                // 여기서 삼키지 않으면 로비 버튼이 예외로 죽는다. 대신 원인을 남긴다.
                Log.Print(Log.LogLevel.Error, $"[MultiplayerManager] 연결 실패. error={e.Message}", Log.LogFilter.Server);
                _session.Set(NetcodeSessionState.Failed);
                return false;
            }
        }

        private async UniTask<bool> WaitForNetworkIdAsync(CancellationToken ct)
        {
            if (0 != _coordinator.NetworkId.CurrentValue)
            {
                return true;
            }

            var readySource = new UniTaskCompletionSource<bool>();

            using var subscription = _coordinator.NetworkId
                .Where(networkId => 0 != networkId)
                .Subscribe(_ => readySource.TrySetResult(true));

            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(_config.TimeoutSeconds), cancellationToken: ct);
            var (isReady, _) = await UniTask.WhenAny(readySource.Task, timeoutTask);

            return isReady;
        }

        private void HandleRoomUpdated(RoomState roomState)
        {
            _session.SetRoomCode(roomState.RoomCode);
            _session.SetPlayers(roomState.Players);
        }

        private void HandleSessionClosed()
        {
            _coordinator.DisconnectAsync().Forget();
            _session.Reset();
        }
    }
}
