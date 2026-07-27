using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Roslyn.Analyzer;
using O2un.Roslyn.Generator;
using R3;

namespace O2un.MVVM
{
    public interface IContextBase
    {

    }

    public abstract partial class ContextBase<V,M> : SafeMono, IContextBase, ISafeInitializable where V : class, IViewBase where M : class, IViewModelBase
    {
        [RequireComponentField] private V _view;
        public M Model { get; private set; }

        public Observable<Unit> OnTurnOn => View.OnTurnOnAfter;
        public Observable<Unit> OnTurnOff => View.OnTurnOffAfter;

        [MustCallBase]
        protected override async UniTask Init(CancellationToken ct)
        {
            await base.Init(ct);
            var childUIs = GetComponentsInChildren<ISafeInitializable>();
            foreach(var su in childUIs)
            {
                su.Initialize();
            }

            Model = CreateModel();
            View.Bind(Model);
        }
        protected abstract M CreateModel();

        [MustCallBase]
        protected override void SafeDestroy()
        {
            if(Model is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Model = null;

            base.SafeDestroy();
        }
    }
}
