using System;
using Cysharp.Threading.Tasks;
using O2un.Core.Network;
using O2un.Core.Utils;
using O2un.MVVM;
using O2un.Utils;
using R3;

namespace O2un.UI
{
    public sealed class RoomVM : ViewModelBase
    {
        private const string REQUEST_KEY = "RoomRequest";

        private readonly IMultiplayerManager _multiplayer;

        private readonly ReactiveProperty<bool> _isBusy = new(false);

        public string LocalPlayerId => _multiplayer.LocalPlayerId;

        public ReadOnlyReactiveProperty<string> RoomCode => _multiplayer.Session.RoomCode;
        public ReadOnlyReactiveProperty<PlayerInfo[]> Players => _multiplayer.Session.Players;
        public ReadOnlyReactiveProperty<bool> IsBusy => _isBusy;

        // 방을 만든 직후에는 참가자 목록이 아직 서버에서 오지 않는다. 역할은 접속 응답으로 확정된다.
        public Observable<bool> IsHost => _multiplayer.Session.Role.Select(role => MultiplayerRole.Host == role);

        public RoomVM(IMultiplayerManager multiplayer)
        {
            _multiplayer = multiplayer;
        }

        public override async UniTask InitAsync()
        {
            await base.InitAsync();

            _multiplayer.Session.State
                        .Select(state => NetcodeSessionState.InLobby == state)
                        .DistinctUntilChanged()
                        .Subscribe(SetVisible)
                        .AddTo(DisposableR3);
        }

        internal void StartGame()
        {
            // NULL
        }

        internal void LeaveRoom()
        {
            if (true == _isBusy.CurrentValue) return;

            _isBusy.Value = true;

            this.StartExclusiveAsync(REQUEST_KEY, async ct =>
            {
                try
                {
                    await _multiplayer.LeaveAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    // 취소는 Dispose 경로에서만 온다. 이미 정리된 _isBusy 를 여기서 되돌리면 안 된다.
                    return;
                }
                catch (Exception e)
                {
                    Log.Print(Log.LogLevel.Error, $"[RoomVM] 퇴장 실패. error={e.Message}", Log.LogFilter.Server);
                }

                _isBusy.Value = false;
            });
        }

        protected override void SafeDispose()
        {
            _isBusy.Dispose();

            base.SafeDispose();
        }
    }
}
