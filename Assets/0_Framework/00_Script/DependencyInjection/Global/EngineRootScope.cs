using O2un.Core;
using O2un.Core.Data;
using O2un.Core.Events;
using O2un.Core.Network;
using O2un.Core.Utils;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public class EngineRootScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<UIEventBus>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<LogManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NetworkManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MultiplayerManager>(Lifetime.Singleton).AsImplementedInterfaces();

            RegisterProviders(builder);

            builder.RegisterEntryPoint<EngineBootStrapper>();
            builder.RegisterDebugModules();
            builder.RegisterGameInstallers();
        }

        private void RegisterProviders(IContainerBuilder builder)
        {
            builder.Register<LoadingProvider>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<RuntimeDataProvider>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}
