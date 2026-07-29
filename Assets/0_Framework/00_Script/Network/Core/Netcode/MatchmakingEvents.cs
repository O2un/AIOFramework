namespace O2un.Core.Network
{
    /// <summary>
    /// 매치메이킹 이벤트명. 오타는 컴파일을 통과하고 런타임에 조용히 라우팅되지 않으므로 한 곳에 모은다.
    /// </summary>
    public static class MatchmakingEvents
    {
        // 요청 이벤트. 응답 이름은 NetworkManager 가 Ack postfix 를 붙여 만든다.
        public const string CREATE_ROOM = "createRoom";
        public const string JOIN_ROOM = "joinRoom";
        public const string LEAVE_ROOM = "leaveRoom";
        public const string ROOM_LIST = "roomList";

        // 서버 푸시. 짝이 되는 요청이 없어 이름이 그대로 수신 이벤트가 된다.
        public const string ROOM_UPDATED = "roomUpdated";
        public const string SESSION_CLOSED = "sessionClosed";
    }
}
