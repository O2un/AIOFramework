using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    /// <summary>
    /// 역할별 연결 절차. <see cref="ConnectAsync"/> 가 보장하는 것은 Listen/Connect 요청 엔티티를 만든
    /// 데까지이며, 실제 연결 성립은 ECS 쪽에서 확인한다.
    /// </summary>
    public interface INetcodeConnectionModule
    {
        MultiplayerRole Role { get; }

        UniTask ConnectAsync(MatchConnectionInfo connectionInfo, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }
}
