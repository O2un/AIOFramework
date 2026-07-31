using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Data;
using O2un.Core.Utils;
using R3;

namespace O2un.Core.Network
{
    public sealed class WebSocketMatchmakingService : SafeDisposableClass, IMatchmakingService
    {
        private readonly INetworkMessenger _messenger;
        private readonly IRuntimeDataProvider _dataProvider;
        private NetworkRuntimeData _config;

        private readonly Subject<MatchConnectionInfo> _matchAssigned = new();
        private readonly Subject<RoomState> _roomUpdated = new();
        private readonly Subject<SessionClosedNotice> _sessionClosed = new();

        public ReadOnlyReactiveProperty<bool> IsConnected => _messenger.IsConnected;
        public MatchConnectionInfo CurrentConnection { get; private set; }

        public Observable<MatchConnectionInfo> MatchAssigned => _matchAssigned;
        public Observable<RoomState> RoomUpdated => _roomUpdated;
        public Observable<SessionClosedNotice> SessionClosed => _sessionClosed;

        public WebSocketMatchmakingService(INetworkMessenger messenger, IRuntimeDataProvider dataProvider)
        {
            _messenger = messenger;
            _dataProvider = dataProvider;
        }

        public void Initialize()
        {
            _config = _dataProvider.Get<NetworkRuntimeData>();

            _messenger.Observe<RoomState>(MatchmakingEvents.ROOM_UPDATED)
                .Subscribe(HandleRoomUpdated)
                .AddTo(DisposableR3);
            _messenger.Observe<SessionClosedNotice>(MatchmakingEvents.SESSION_CLOSED)
                .Subscribe(HandleSessionClosed)
                .AddTo(DisposableR3);
            _messenger.IsConnected
                .Where(isConnected => false == isConnected)
                .Subscribe(_ => HandleDisconnected())
                .AddTo(DisposableR3);
        }

        protected override void SafeDispose()
        {
            _matchAssigned.Dispose();
            _roomUpdated.Dispose();
            _sessionClosed.Dispose();
        }

        public async UniTask<bool> ConnectAsync(CancellationToken ct = default)
        {
            if (true == _messenger.IsConnected.CurrentValue)
            {
                return true;
            }

            try
            {
                var readySource = new UniTaskCompletionSource<bool>();

                using var subscription = _messenger.IsConnected
                    .Where(isConnected => true == isConnected)
                    .Subscribe(isConnected => readySource.TrySetResult(isConnected));

                var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(_config.TimeoutSeconds), cancellationToken: ct);
                var (isReady, _) = await UniTask.WhenAny(readySource.Task, timeoutTask);

                return isReady;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public async UniTask<MatchmakingResult<MatchConnectionInfo>> CreateRoomAsync(string playerId, int maxPlayers, CancellationToken ct = default)
        {
            var request = new CreateRoomReq
            {
                PlayerId = playerId,
                MaxPlayers = maxPlayers,

                Port = _config.MultiplayerPort,
                BuildCompatibilityId = BuildCompatibilityId.Current,
            };

            var ack = await _messenger.SendDataAndWaitAsync<CreateRoomReq, CreateRoomAck>(MatchmakingEvents.CREATE_ROOM, request, ct);

            if (null == ack)
            {
                return MatchmakingResult<MatchConnectionInfo>.Failure(MatchmakingReasons.NO_RESPONSE);
            }
            if (false == ack.IsSuccess)
            {
                return MatchmakingResult<MatchConnectionInfo>.Failure(ToUserFacing(ack.Reason));
            }

            return AcceptConnection(ack.Connection);
        }

        public async UniTask<MatchmakingResult<MatchConnectionInfo>> JoinRoomAsync(string playerId, string roomCode, CancellationToken ct = default)
        {
            var request = new JoinRoomReq
            {
                PlayerId = playerId,
                RoomCode = roomCode,
                BuildCompatibilityId = BuildCompatibilityId.Current,
            };

            var ack = await _messenger.SendDataAndWaitAsync<JoinRoomReq, JoinRoomAck>(MatchmakingEvents.JOIN_ROOM, request, ct);

            if (null == ack)
            {
                return MatchmakingResult<MatchConnectionInfo>.Failure(MatchmakingReasons.NO_RESPONSE);
            }
            if (false == ack.IsSuccess)
            {
                return MatchmakingResult<MatchConnectionInfo>.Failure(ToUserFacing(ack.Reason));
            }

            return AcceptConnection(ack.Connection);
        }

        public async UniTask<MatchmakingResult> LeaveRoomAsync(CancellationToken ct = default)
        {
            var connection = CurrentConnection;
            if (null == connection)
            {
                return MatchmakingResult.Failure(MatchmakingReasons.NOT_IN_ROOM);
            }

            var request = new LeaveRoomReq { SessionId = connection.SessionId };
            var ack = await _messenger.SendDataAndWaitAsync<LeaveRoomReq, LeaveRoomAck>(MatchmakingEvents.LEAVE_ROOM, request, ct);

            if (null == ack)
            {
                return MatchmakingResult.Failure(MatchmakingReasons.NO_RESPONSE);
            }
            if (false == ack.IsSuccess)
            {
                return MatchmakingResult.Failure(ToUserFacing(ack.Reason));
            }

            CurrentConnection = null;
            return MatchmakingResult.Success();
        }

        public async UniTask<MatchmakingResult<RoomSummary[]>> GetRoomListAsync(CancellationToken ct = default)
        {
            var ack = await _messenger.SendDataAndWaitAsync<RoomListReq, RoomListAck>(MatchmakingEvents.ROOM_LIST, new RoomListReq(), ct);

            if (null == ack)
            {
                return MatchmakingResult<RoomSummary[]>.Failure(MatchmakingReasons.NO_RESPONSE);
            }
            if (false == ack.IsSuccess)
            {
                return MatchmakingResult<RoomSummary[]>.Failure(ToUserFacing(ack.Reason));
            }

            return MatchmakingResult<RoomSummary[]>.Success(ack.Rooms ?? Array.Empty<RoomSummary>());
        }

        /// <summary>
        /// 사용자가 손댈 수 없는 사유는 화면에 올리지 않고 접는다. 접으면 원인이 사라지므로 원문 코드는 로그에 남긴다.
        /// </summary>
        private static string ToUserFacing(string reason)
        {
            if (true == MatchmakingReasons.IsPublic(reason))
            {
                return reason;
            }

            Log.Print(Log.LogLevel.Warning, $"거절 사유를 접었다. code={reason ?? "null"}", Log.LogFilter.Server);
            return MatchmakingReasons.REQUEST_REJECTED;
        }

        private MatchmakingResult<MatchConnectionInfo> AcceptConnection(MatchConnectionInfo connection)
        {
            if (null == connection)
            {
                Log.Print(Log.LogLevel.Error, "서버가 성공을 반환했는데 접속 정보가 비어 있다.", Log.LogFilter.Server);
                return MatchmakingResult<MatchConnectionInfo>.Failure(MatchmakingReasons.REQUEST_REJECTED);
            }

            if (false == BuildCompatibilityId.IsMatch(connection.BuildCompatibilityId, BuildCompatibilityId.Current))
            {
                Log.Print(
                    Log.LogLevel.Error,
                    $"방 호환성 값이 내 빌드와 다르다. room={connection.BuildCompatibilityId ?? "null"}, local={BuildCompatibilityId.Current}",
                    Log.LogFilter.Server);
                return MatchmakingResult<MatchConnectionInfo>.Failure(MatchmakingReasons.BUILD_INCOMPATIBLE);
            }

            CurrentConnection = connection;
            _matchAssigned.OnNext(connection);
            return MatchmakingResult<MatchConnectionInfo>.Success(connection);
        }

        private void HandleRoomUpdated(RoomState state)
        {
            if (null == state)
            {
                return;
            }

            _roomUpdated.OnNext(state);
        }

        private void HandleSessionClosed(SessionClosedNotice notice)
        {
            if (null == notice)
            {
                return;
            }

            var connection = CurrentConnection;
            if (null != connection && true == string.Equals(connection.SessionId, notice.SessionId, StringComparison.Ordinal))
            {
                CurrentConnection = null;
            }

            _sessionClosed.OnNext(notice);
        }

        private void HandleDisconnected()
        {
            var connection = CurrentConnection;
            if (null == connection)
            {
                return;
            }

            CurrentConnection = null;
            _sessionClosed.OnNext(new SessionClosedNotice
            {
                SessionId = connection.SessionId,
                Reason = MatchmakingReasons.CONNECTION_LOST,
            });
        }
    }
}
