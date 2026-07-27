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
            // 자식 Scope들이 같은 인스턴스를 받아야 상위/하위 무관하게 이벤트가 오간다
            builder.Register<UIEventBus>(Lifetime.Singleton).As<IUIEventPublisher, IUIEventSubscriber>();

            builder.Register<LogManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<PoolingManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<NetworkManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SceneManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            builder.RegisterEntryPoint<EngineBootStrapper>();
        }
    }
}
