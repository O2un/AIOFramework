using System;
using O2un.Reactive;
using R3;

namespace O2un.Core.Network
{
    public enum NetcodeSessionState
    {
        None,
        ConnectingMatchmaking,
        CreatingRoom,
        JoiningRoom,
        StartingNetcode,
        ConnectingNetcode,
        Connected,
        InLobby,
        Disconnecting,
        Failed,
    }

    public interface INetcodeSessionSource
    {
        ReadOnlyReactiveProperty<NetcodeSessionState> State { get; }
        ReadOnlyReactiveProperty<string> RoomCode { get; }
        ReadOnlyReactiveProperty<PlayerInfo[]> Players { get; }
        ReadOnlyReactiveProperty<int> NetworkId { get; }
    }

    public sealed class NetcodeSessionRuntime : ReactiveClass<NetcodeSessionState>, INetcodeSessionSource
    {
        public NetcodeSessionRuntime() : base(NetcodeSessionState.None)
        {
        }

        private readonly ReactiveProperty<string> _roomCode = new(string.Empty);
        private readonly ReactiveProperty<PlayerInfo[]> _players = new(Array.Empty<PlayerInfo>());
        private readonly ReactiveProperty<int> _networkId = new(0);

        ReadOnlyReactiveProperty<NetcodeSessionState> INetcodeSessionSource.State => State;
        public ReadOnlyReactiveProperty<string> RoomCode => _roomCode;
        public ReadOnlyReactiveProperty<PlayerInfo[]> Players => _players;
        public ReadOnlyReactiveProperty<int> NetworkId => _networkId;

        protected override void SafeDispose()
        {
            _roomCode.Dispose();
            _players.Dispose();
            _networkId.Dispose();

            base.SafeDispose();
        }

        public void SetRoomCode(string roomCode) => _roomCode.Value = roomCode;
        public void SetPlayers(PlayerInfo[] players) => _players.Value = players ?? Array.Empty<PlayerInfo>();
        public void SetNetworkId(int networkId) => _networkId.Value = networkId;

        public void Reset()
        {
            Set(NetcodeSessionState.None);
            _roomCode.Value = string.Empty;
            _players.Value = Array.Empty<PlayerInfo>();
            _networkId.Value = 0;
        }
    }
}
