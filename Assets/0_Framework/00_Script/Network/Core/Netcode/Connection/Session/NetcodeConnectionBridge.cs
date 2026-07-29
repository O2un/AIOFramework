using System;

namespace O2un.Core.Network
{
    // ECS World 에는 DI 가 없어 시스템이 주입을 받을 수 없다. 정적 브리지는 그 제약 때문에 생긴 것이므로
    // internal 로 묶어 어셈블리 밖에서는 보이지 않게 한다. 게임 코드와 UI 는 파사드 매니저만 본다.
    internal static class NetcodeConnectionBridge
    {
        internal static event Action<int> NetworkIdChanged;

        internal static void RaiseNetworkIdChanged(int networkId)
        {
            NetworkIdChanged?.Invoke(networkId);
        }
    }
}
