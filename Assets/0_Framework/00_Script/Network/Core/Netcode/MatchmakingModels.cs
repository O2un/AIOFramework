namespace O2un.Core.Network
{
    public enum MultiplayerRole
    {
        None,
        Host,
        Client,
        DedicatedServer,
    }

    public sealed class MatchConnectionInfo
    {
        public string SessionId { get; set; }
        public string RoomCode { get; set; }
        public MultiplayerRole Role { get; set; }
        public string Address { get; set; }
        public ushort Port { get; set; }
        public string ConnectionToken { get; set; }
    }

    public sealed class CreateRoomReq
    {
        public string PlayerId { get; set; }
        public int MaxPlayers { get; set; }
        public ushort Port { get; set; }
    }

    public sealed class JoinRoomReq
    {
        public string PlayerId { get; set; }
        public string RoomCode { get; set; }
    }

    public sealed class LeaveRoomReq
    {
        public string SessionId { get; set; }
    }

    public sealed class RoomListReq
    {
    }

    public sealed class CreateRoomAck
    {
        public bool IsSuccess { get; set; }
        public string Reason { get; set; }
        public MatchConnectionInfo Connection { get; set; }
    }

    public sealed class JoinRoomAck
    {
        public bool IsSuccess { get; set; }
        public string Reason { get; set; }
        public MatchConnectionInfo Connection { get; set; }
    }

    public sealed class LeaveRoomAck
    {
        public bool IsSuccess { get; set; }
        public string Reason { get; set; }
    }

    public sealed class RoomListAck
    {
        public bool IsSuccess { get; set; }
        public string Reason { get; set; }
        public RoomSummary[] Rooms { get; set; }
    }

    public sealed class RoomSummary
    {
        public string RoomCode { get; set; }
        public string HostPlayerId { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public bool IsJoinable { get; set; }
    }

    public sealed class PlayerInfo
    {
        public string PlayerId { get; set; }
        public bool IsHost { get; set; }
    }

    public sealed class RoomState
    {
        public string RoomCode { get; set; }
        public string HostPlayerId { get; set; }
        public PlayerInfo[] Players { get; set; }
    }

    public sealed class SessionClosedNotice
    {
        public string SessionId { get; set; }
        public string Reason { get; set; }
    }
}
