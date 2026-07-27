using O2un.Core;
using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(LoadingView))]
    public sealed partial class LoadingContext : ContextBase<LoadingView, LoadingViewModel>
    {
        [SerializeField] private LoadingType _type;
        [Inject] private ILoadingProvider _loadingProvider;

        protected override LoadingViewModel CreateModel()
        {
            return new(CreateLoadingSource());
        }

        private ILoadingSource CreateLoadingSource()
        {
            return _type switch
            {
                LoadingType.Scene => _loadingProvider.GetRuntime(LoadingType.Scene),
                LoadingType.Patch => _loadingProvider.GetRuntime(LoadingType.Patch),
                LoadingType.Resources => _loadingProvider.GetRuntime(LoadingType.Resources),
                LoadingType.Mock => new MockLoadingSource(),
                _ => throw new System.ArgumentOutOfRangeException()
            };
        }
    }
}
