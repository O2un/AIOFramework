using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public sealed class LoadingSceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
        }
    }
}
