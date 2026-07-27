using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    /// <summary>
    /// 디버그 UI 합성 지점. 새 디버그 모듈은 여기 한 줄만 추가하면 되고 LifetimeScope 는 건드리지 않는다.
    /// </summary>
    public static class DebugInstaller
    {
        public static void RegisterDebugModules(this IContainerBuilder builder)
        {
            DebugConfig config = DebugConfig.LoadRuntime();
            if (null == config || false == config.EnableDebugUI)
            {
                return;
            }

            builder.RegisterInstance(config);

            builder.Register<LogViewerModule>(Lifetime.Singleton).As<IDebugModule>();

            builder.RegisterEntryPoint<DebugBootstrap>();
        }
    }
}
