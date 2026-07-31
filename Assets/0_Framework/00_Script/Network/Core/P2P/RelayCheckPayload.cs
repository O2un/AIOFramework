namespace O2un.Core.Network
{
    public sealed class RelayCheckPayload
    {
        // 송신 때 채운 값은 Host 가 실제 연결에서 확정한 값으로 덮어쓴다. 수신자가 보는 값만 신뢰할 수 있다.
        public ulong SenderId { get; set; }
        public string Message { get; set; }
    }
}
