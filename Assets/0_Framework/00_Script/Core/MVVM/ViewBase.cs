using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using O2un.Roslyn.Analyzer;
using R3;

namespace O2un.MVVM
{
    public interface IViewBase
    {
        void Bind(IViewModelBase viewModel);
        Observable<Unit> OnTurnOnAfter { get; }
        Observable<Unit> OnTurnOffAfter { get; }
    }

    public abstract class ViewBase<T> : SafeUI, IViewBase where T : class, IViewModelBase
    {
        protected T Model {get; private set;}
        public void Bind(IViewModelBase viewModel)
        {
            if(viewModel is not T typedModel)
            {
                Log.Print(Log.LogLevel.Error, $"[{GetType().Name}] 비정상 ViewModel 타입. expected={typeof(T).Name}, actual={viewModel?.GetType().Name}");
                _= TurnOffAsync();
                return;
            }

            Model = typedModel;
            Model.TryInit(_isVisibleOnInit);
            _=BindModelAsync();
        }

        protected async UniTask BindModelAsync()
        {
            if(null == Model)
            {
                await TurnOffAsync();
                return;
            }

            await Model.WaitUntilReadyAsync();
            Model.IsVisible.Subscribe(TurnOnOffFromVM).AddTo(DisposableR3);
            BindModel();
        }

        private void TurnOnOffFromVM(bool isOn)
        {
            _ = Switch(isOn);
        }

        protected abstract void BindModel();

        [MustCallBase]
        protected override void SafeDestroy()
        {
            base.SafeDestroy();
            Model = null;
        }
    }
}
