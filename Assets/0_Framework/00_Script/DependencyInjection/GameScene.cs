using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public sealed class GameSceneScope : CommonLifetimeScope
    {
        protected override void ConfigureScene(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<RelayCheckRunner>();
        }
    }
}
