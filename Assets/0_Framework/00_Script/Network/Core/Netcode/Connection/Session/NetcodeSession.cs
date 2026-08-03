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
        ReadOnlyReactiveProperty<MultiplayerRole> Role { get; }
        ReadOnlyReactiveProperty<PlayerInfo[]> Players { get; }
        ReadOnlyReactiveProperty<int> NetworkId { get; }
    }

    public sealed class NetcodeSessionRuntime : ReactiveClass<NetcodeSessionState>, INetcodeSessionSource
    {
        public NetcodeSessionRuntime() : base(NetcodeSessionState.None)
        {
        }

        private readonly ReactiveProperty<string> _roomCode = new(string.Empty);
        private readonly ReactiveProperty<MultiplayerRole> _role = new(MultiplayerRole.None);
        private readonly ReactiveProperty<PlayerInfo[]> _players = new(Array.Empty<PlayerInfo>());
        private readonly ReactiveProperty<int> _networkId = new(0);

        public ReadOnlyReactiveProperty<string> RoomCode => _roomCode;
        public ReadOnlyReactiveProperty<MultiplayerRole> Role => _role;
        public ReadOnlyReactiveProperty<PlayerInfo[]> Players => _players;
        public ReadOnlyReactiveProperty<int> NetworkId => _networkId;

        protected override void SafeDispose()
        {
            _roomCode.Dispose();
            _role.Dispose();
            _players.Dispose();
            _networkId.Dispose();

            base.SafeDispose();
        }

        public void SetRoomCode(string roomCode) => _roomCode.Value = roomCode;
        public void SetRole(MultiplayerRole role) => _role.Value = role;
        public void SetPlayers(PlayerInfo[] players) => _players.Value = players ?? Array.Empty<PlayerInfo>();
        public void SetNetworkId(int networkId) => _networkId.Value = networkId;

        public void Reset()
        {
            Set(NetcodeSessionState.None);
            _roomCode.Value = string.Empty;
            _role.Value = MultiplayerRole.None;
            _players.Value = Array.Empty<PlayerInfo>();
            _networkId.Value = 0;
        }
    }
}
