using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using O2un.Roslyn.Analyzer;
using R3;

namespace O2un.MVVM
{
    public abstract class ViewBaseToolkit<T> : SafeUIToolkit, IViewBase where T : class, IViewModelBase
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

        private async UniTask BindModelAsync()
        {
            if(null == Model)
            {
                await TurnOffAsync();
                return;
            }

            // BindElements 는 Init 안에서, BindModel 은 Bind 경로에서 호출된다.
            // 양쪽이 끝난 뒤여야 ViewRoot 와 Model 이 모두 살아있다.
            await WaitUntilReadyAsync();
            await Model.WaitUntilReadyAsync();
            Model.IsVisible.Subscribe(TurnOnOffFromVM).AddTo(DisposableR3);
            BindModel();
        }

        private void TurnOnOffFromVM(bool isOn)
        {
            _= SwitchAsync(isOn);
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
