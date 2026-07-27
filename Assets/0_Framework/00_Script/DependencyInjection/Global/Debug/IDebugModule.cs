using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.DI
{
    /// <summary>
    /// 디버그 전용 UI 한 덩어리.
    /// CommonLifetimeScope 의 자동 주입은 씬에 배치되고 인스펙터에 물린 UI만 대상으로 한다.
    /// 디버그 UI는 씬마다 배치할 물건이 아니므로, 자기 주소와 선행 조건을 스스로 아는 모듈로 쪼개고
    /// DebugBootstrap 이 로드·주입·초기화를 맡는다. LifetimeScope 는 개별 UI 타입을 알지 않는다.
    /// </summary>
    public interface IDebugModule
    {
        string Key { get; }
        string Address { get; }
        UniTask WaitUntilReadyAsync(CancellationToken ct);
    }
}
