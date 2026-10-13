using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using O2un.MVVM;
using R3;
using UnityEngine.InputSystem;

namespace O2un.UI
{
    public sealed class CheatVM : ViewModelBase
    {
        public IReadOnlyList<ICheatProvider> Providers { get; }

        public CheatVM(IReadOnlyList<ICheatProvider> providers)
        {
            Providers = providers;
        }

        public override async UniTask InitAsync()
        {
            await base.InitAsync();

            DebugHotkey.CtrlAltDoublePress(Key.C)
                       .Subscribe(_ => SetVisible(false == IsVisible.CurrentValue))
                       .AddTo(DisposableR3);
        }

        public void Hide()
        {
            SetVisible(false);
        }
    }
}
