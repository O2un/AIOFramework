using System;
using Cysharp.Threading.Tasks;
using O2un.Roslyn.Analyzer;
using UnityEngine;
using VContainer;

namespace O2un.NVVM
{
    public abstract class SafeView<T> : SafeUI where T : ViewModelBase
    {
        protected T Model {get; private set;}

        [Inject]
        public void Inject(T viewModel)
        {
            Model = viewModel;
            Model.TryInit();
        }

        protected override async UniTask Init()
        {
            await base.Init();

            if(null != Model)
            {
                await Model.WaitUntilReadyAsync();
                BindModel();
            }
            else
            {
                await TurnOffAsync();
            }
        }

        protected abstract void BindModel();

        [CallSuper]
        protected override void SafeDestroy()
        {
            base.SafeDestroy();
            Model = null;
        }
    }
}
