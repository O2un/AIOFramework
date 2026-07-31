namespace O2un.Core.Network
{
    /// <summary>
    /// P2P 요청·응답의 실패 사유를 호출부까지 올리기 위한 봉투. 세션은 퇴장·강퇴·호스트 종료로 정상적으로 끊기므로
    /// 응답을 못 받은 것을 예외가 아니라 값으로 다룬다.
    /// </summary>
    public sealed class P2PPacketResult<T>
    {
        public bool IsSuccess { get; }
        public string Reason { get; }
        public T Value { get; }

        private P2PPacketResult(bool isSuccess, string reason, T value)
        {
            IsSuccess = isSuccess;
            Reason = reason;
            Value = value;
        }

        public static P2PPacketResult<T> Success(T value) => new(true, null, value);
        public static P2PPacketResult<T> Failure(string reason) => new(false, reason, default);
    }

    public static class P2PPacketReasons
    {
        public const string NOT_CONNECTED = "NOT_CONNECTED";
        public const string SESSION_CLOSED = "SESSION_CLOSED";
        public const string SEND_FAILED = "SEND_FAILED";
        public const string TIMEOUT = "TIMEOUT";
    }
}
