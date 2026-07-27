using VContainer;

namespace O2un.DI
{
    public sealed class LoadingSceneScope : CommonLifetimeScope
    {
        // LoadingContext는 더 이상 자체 Scope가 아니라 Root의 ILoadingProvider를 주입받는
        // 일반 Context다. 주입은 CommonLifetimeScope의 _uiRoots 자동 주입이 맡는다.
        protected override void ConfigureScene(IContainerBuilder builder)
        {
        }
    }
}
