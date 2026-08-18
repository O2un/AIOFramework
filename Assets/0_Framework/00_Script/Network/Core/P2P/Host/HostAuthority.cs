namespace O2un.Core.Network
{
    /// <summary>
    /// Server World 가 보는 Host 판정 기준이다. Client 가 스스로 Host 라 주장하는 값은 쓰지 않고,
    /// 같은 프로세스의 Host 가 자기 NetworkId 를 등록한 것만 신뢰한다.
    /// </summary>
    // ECS World 에는 DI 가 없어 Handler 가 주입을 받을 수 없다. 정적 통로는 그 제약 때문에 생긴 것이므로
    // internal 로 묶어 어셈블리 밖에서는 보이지 않게 한다.
    internal static class HostAuthority
    {
        private static P2PPeerId _hostPeerId = P2PPeerId.None;

        // Dedicated Server 에는 Host Client 가 없어 아무도 통과하지 못한다. 권한 Event 는 열리지 않는 쪽이 기본값이다.
        internal static bool IsHost(P2PPeerId peerId)
        {
            return true == _hostPeerId.IsValid && _hostPeerId == peerId;
        }

        internal static void SetHostPeer(P2PPeerId peerId)
        {
            _hostPeerId = peerId;
        }

        internal static void Clear()
        {
            _hostPeerId = P2PPeerId.None;
        }
    }
}
