using O2un;
using O2un.Core;
using O2un.Roslyn.Generator;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace O2un.UI
{
    public sealed partial class LoadingContext : LifetimeScope
    {
        [SerializeField] private LoadingType _type;
        [RequireComponentField] private LoadingView _view;
        
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(View);
            builder.Register(CreateLoadingSource, Lifetime.Scoped);
            builder.Register<LoadingViewModel>(Lifetime.Scoped);
        }

        private ILoadingSource CreateLoadingSource(IObjectResolver resolver)
        {
            return _type switch
            {
                LoadingType.Scene => new SceneLoadingSource(resolver.Resolve<SceneManager>()),
                LoadingType.Patch => throw new System.NotImplementedException(),
                LoadingType.Resources => throw new System.NotImplementedException(),
                LoadingType.Mock => new MockLoadingSource(),
                _ => throw new System.ArgumentOutOfRangeException()
            };
        }
    }
}
