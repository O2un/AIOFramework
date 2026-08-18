using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Network;
using O2un.Core.Utils;
using O2un.MVVM;
using O2un.Utils;
using R3;

namespace O2un.UI
{
    public sealed class MultiplayerEntryVM : ViewModelBase
    {
        private const string REQUEST_KEY = "MultiplayerEntryRequest";
        private const int MAX_PLAYERS = 4;

        private readonly IMultiplayerManager _multiplayer;

        private readonly ReactiveProperty<bool> _isBusy = new(false);
        private readonly ReactiveProperty<IReadOnlyList<RoomSummary>> _rooms = new(Array.Empty<RoomSummary>());
        private readonly ReactiveProperty<string> _roomCode = new(string.Empty);

        public ReadOnlyReactiveProperty<bool> IsBusy => _isBusy;
        public ReadOnlyReactiveProperty<IReadOnlyList<RoomSummary>> Rooms => _rooms;
        public ReadOnlyReactiveProperty<string> RoomCode => _roomCode;
        public ReadOnlyReactiveProperty<NetcodeSessionState> State => _multiplayer.Session.State;

        public MultiplayerEntryVM(IMultiplayerManager multiplayer)
        {
            _multiplayer = multiplayer;
        }

        public override async UniTask InitAsync()
        {
            await base.InitAsync();

            _multiplayer.Session.State
                        .Select(state => NetcodeSessionState.InLobby != state)
                        .DistinctUntilChanged()
                        .Subscribe(OnOutsideRoomChanged)
                        .AddTo(DisposableR3);
        }

        private void OnOutsideRoomChanged(bool isOutsideRoom)
        {
            SetVisible(isOutsideRoom);

            if (false == isOutsideRoom) return;

            RefreshRoomList();
        }

        internal void SetRoomCode(string roomCode)
        {
            _roomCode.Value = roomCode ?? string.Empty;
        }

        internal void SelectRoom(RoomSummary room)
        {
            if (null == room)
            {
                return;
            }

            if (false == room.IsJoinable)
            {
                return;
            }

            _roomCode.Value = room.RoomCode;
        }

        internal void CreateRoom()
        {
            Request(async ct => await _multiplayer.CreateRoomAsync(MAX_PLAYERS, ct));
        }

        internal void JoinRoom()
        {
            string roomCode = _roomCode.CurrentValue.Trim().ToUpperInvariant();

            if (true == string.IsNullOrEmpty(roomCode))
            {
                return;
            }

            Request(async ct => await _multiplayer.JoinRoomAsync(roomCode, ct));
        }

        internal void RefreshRoomList()
        {
            Request(async ct =>
            {
                RoomSummary[] rooms = await _multiplayer.GetRoomListAsync(ct);

                if (true == IsDisposed)
                {
                    return;
                }

                _rooms.Value = rooms;
            });
        }

        private void Request(Func<CancellationToken, UniTask> request)
        {
            if (true == _isBusy.CurrentValue)
            {
                return;
            }

            _isBusy.Value = true;

            this.StartExclusiveAsync(REQUEST_KEY, async ct =>
            {
                try
                {
                    await request(ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Log.Print(Log.LogLevel.Error, $"[MultiplayerEntryVM] 방 요청 실패. error={e.Message}", Log.LogFilter.Server);
                }

                // Manager 가 실패를 삼키고 정상 반환하므로 Dispose 중에도 취소 예외가 오지 않는다.
                if (true == IsDisposed)
                {
                    return;
                }

                _isBusy.Value = false;
            });
        }

        protected override void SafeDispose()
        {
            _isBusy.Dispose();
            _rooms.Dispose();
            _roomCode.Dispose();

            base.SafeDispose();
        }
    }
}
