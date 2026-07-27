using O2un.Core;
using O2un.Core.Events;
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
            builder.Register<UIEventBus>(Lifetime.Singleton).As<IUIEventPublisher, IUIEventSubscriber>();

            builder.Register<LogManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<PoolingManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<NetworkManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SceneManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            RegisterProviders(builder);

            builder.RegisterEntryPoint<EngineBootStrapper>();
        }

        private void RegisterProviders(IContainerBuilder builder)
        {
            builder.Register<LoadingProvider>(Lifetime.Singleton).As<ILoadingProvider>();
        }
    }
}
