using O2un.Core;
using O2un.Core.Network;
using O2un.Core.Utils;
using O2un.Pooling;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public class EngineRootScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<LogManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<PoolingManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<NetworkManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SceneManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            builder.RegisterEntryPoint<EngineBootStrapper>();
        }
    }
}
