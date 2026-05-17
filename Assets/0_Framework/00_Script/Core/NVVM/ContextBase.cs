using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Roslyn.Analyzer;
using O2un.Roslyn.Generator;
using UnityEngine;

namespace O2un.MVVM
{
    public interface IContextBase
    {
        
    }
    
    public abstract partial class ContextBase<V,M> : SafeMono, IContextBase, ISafeInitializable where V : ViewBase<M> where M : ViewModelBase
    {
        [RequireComponentField] private V _view;
        public M Model { get; private set; }
        [CallBase]
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

        [CallBase]
        protected override void SafeDestroy()
        {
            Model?.Dispose();
            Model = null;

            base.SafeDestroy();
        }
    }
}
