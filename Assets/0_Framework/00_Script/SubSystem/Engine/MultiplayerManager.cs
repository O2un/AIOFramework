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
    /// 로비가 쓰는 방 통로. WSS·매치메이킹 프로토콜은 이 아래로 감춘다.
    /// </summary>
    public interface IMultiplayerRoomContract
    {
        UniTask<bool> CreateRoomAsync(string playerId, int maxPlayers, CancellationToken ct = default);
        UniTask<bool> JoinRoomAsync(string playerId, string roomCode, CancellationToken ct = default);
        UniTask LeaveAsync(CancellationToken ct = default);
        UniTask<RoomSummary[]> GetRoomListAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// 접속 진행 상태를 읽는 통로. 상태를 바꾸는 경로는 열지 않는다.
    /// </summary>
    public interface IMultiplayerSessionContract
    {
        INetcodeSessionSource Session { get; }
    }

    /// <summary>
    /// 게임이 쓰는 패킷 통로. 만들어 보내고 받아서 처리하는 것 외의 전송 계층은 보이지 않는다.
    /// </summary>
    public interface IMultiplayerPacketContract
    {
        Observable<T> Observe<T>(NetworkPacketId eventId);
        UniTask<bool> SendDataAsync<T>(NetworkPacketId eventId, NetworkPacketType packetType, T data, CancellationToken ct = default);
        UniTask<P2PPacketResult<TResponse>> SendDataAndWaitAsync<TRequest, TResponse>(NetworkPacketId eventId, NetworkPacketType packetType, TRequest data, TimeSpan timeout, CancellationToken ct = default);
    }

    public interface IMultiplayerManager : IMultiplayerRoomContract, IMultiplayerSessionContract, IMultiplayerPacketContract
    {
    }

    public sealed class MultiplayerManager : EngineSubsystemBase, IMultiplayerManager
    {
        private readonly INetworkMessenger _messenger;
        private readonly IRuntimeDataProvider _dataProvider;

        public MultiplayerManager(INetworkMessenger messenger, IRuntimeDataProvider dataProvider)
        {
            _messenger = messenger;
            _dataProvider = dataProvider;
        }

        private readonly NetcodeWorldProvider _worldProvider = new();
        private readonly NetcodeSessionRuntime _session = new();

        private WebSocketMatchmakingService _matchmaking;
        private NetcodeConnectionCoordinator _coordinator;
        private P2PSessionCoordinator _sessionCoordinator;

        private NetworkRuntimeData _config;
        private bool _isConnectionProcessing;

        public INetcodeSessionSource Session => _session;

        protected override UniTask InitAsync()
        {
            _config = _dataProvider.Get<NetworkRuntimeData>();

            _matchmaking = new WebSocketMatchmakingService(_messenger, _dataProvider);
            _matchmaking.Initialize();

            _coordinator = new NetcodeConnectionCoordinator(_worldProvider, new NetcodeTransportConnector());
            _coordinator.Initialize();

            _sessionCoordinator = new P2PSessionCoordinator(
                () => NetcodeP2PTransportFactory.TryCreate(_worldProvider, _coordinator.NetworkId));

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
            CloseGameSession();
            _sessionCoordinator?.Dispose();
            _coordinator?.Dispose();
            _matchmaking?.Dispose();
            _session.Dispose();

            base.SafeDispose();
        }

        public Observable<T> Observe<T>(NetworkPacketId eventId)
        {
            IP2PMessenger messenger = _sessionCoordinator?.Messenger;

            if (null == messenger)
            {
                return Observable.Empty<T>();
            }

            return messenger.Observe<T>(eventId);
        }

        public UniTask<bool> SendDataAsync<T>(NetworkPacketId eventId, NetworkPacketType packetType, T data, CancellationToken ct = default)
        {
            IP2PMessenger messenger = _sessionCoordinator?.Messenger;

            if (null == messenger)
            {
                return UniTask.FromResult(false);
            }

            return messenger.SendDataAsync(eventId, packetType, data, ct);
        }

        public UniTask<P2PPacketResult<TResponse>> SendDataAndWaitAsync<TRequest, TResponse>(NetworkPacketId eventId, NetworkPacketType packetType, TRequest data, TimeSpan timeout, CancellationToken ct = default)
        {
            IP2PMessenger messenger = _sessionCoordinator?.Messenger;

            return messenger.SendDataAndWaitResultAsync<TRequest, TResponse>(eventId, packetType, data, timeout, ct);
        }

        public UniTask<bool> CreateRoomAsync(string playerId, int maxPlayers, CancellationToken ct = default)
        {
            return RunAsync(NetcodeSessionState.CreatingRoom, token => _matchmaking.CreateRoomAsync(playerId, maxPlayers, token), ct);
        }

        public UniTask<bool> JoinRoomAsync(string playerId, string roomCode, CancellationToken ct = default)
        {
            return RunAsync(NetcodeSessionState.JoiningRoom, token => _matchmaking.JoinRoomAsync(playerId, roomCode, token), ct);
        }

        public async UniTask<RoomSummary[]> GetRoomListAsync(CancellationToken ct = default)
        {
            if (false == await _matchmaking.ConnectAsync(ct))
            {
                return Array.Empty<RoomSummary>();
            }

            var result = await _matchmaking.GetRoomListAsync(ct);

            return true == result.IsSuccess ? result.Value : Array.Empty<RoomSummary>();
        }

        public async UniTask LeaveAsync(CancellationToken ct = default)
        {
            _session.Set(NetcodeSessionState.Disconnecting);

            CloseGameSession();

            try
            {
                await _matchmaking.LeaveRoomAsync(ct);
            }
            finally
            {
                // 원격 통지는 취소돼도 되지만 로컬 World·연결은 남기면 다음 매치가 활성 연결에 막힌다.
                await _coordinator.DisconnectAsync(CancellationToken.None);
                _session.Reset();
            }
        }

        private async UniTask<bool> RunAsync(NetcodeSessionState matchmakingState, Func<CancellationToken, UniTask<MatchmakingResult<MatchConnectionInfo>>> request, CancellationToken ct)
        {
            if (true == _isConnectionProcessing || true == _coordinator.HasActiveConnection)
            {
                return false;
            }

            _isConnectionProcessing = true;
            var isConnected = false;

            CloseGameSession();

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
                    return false;
                }

                _session.Set(NetcodeSessionState.Connected);

                if (false == _sessionCoordinator.OpenSession())
                {
                    Log.Print(Log.LogLevel.Error, "[MultiplayerManager] 게임 세션을 열지 못했다. Client World 가 없다.", Log.LogFilter.Server);
                    _session.Set(NetcodeSessionState.Failed);
                    return false;
                }

                _session.Set(NetcodeSessionState.InLobby);
                isConnected = true;

                return true;
            }
            catch (OperationCanceledException)
            {
                _session.Set(NetcodeSessionState.Failed);
                return false;
            }
            catch (Exception e)
            {
                Log.Print(Log.LogLevel.Error, $"[MultiplayerManager] 연결 실패. error={e.Message}", Log.LogFilter.Server);
                _session.Set(NetcodeSessionState.Failed);
                return false;
            }
            finally
            {
                try
                {
                    if (false == isConnected)
                    {
                        CloseGameSession();

                        if (true == _coordinator.HasActiveConnection)
                        {
                            await _coordinator.DisconnectAsync(CancellationToken.None);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Print(Log.LogLevel.Error, $"[MultiplayerManager] 연결 실패 정리 중 오류가 발생했다. error={e.Message}", Log.LogFilter.Server);
                }

                _isConnectionProcessing = false;
            }
        }

        private void CloseGameSession()
        {
            _sessionCoordinator?.CloseSession();
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
            CloseGameSession();
            _coordinator.DisconnectAsync().Forget();
            _session.Reset();
        }
    }
}
