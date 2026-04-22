using Cysharp.Threading.Tasks;
using O2un.NVVM;
using R3;

namespace O2un.UI
{
    public sealed class LoadingViewModel : ViewModelBase
    {
        public ReadOnlyReactiveProperty<float> Progress { get; private set; }

        private readonly ILoadingSource  _source;
        public LoadingViewModel(ILoadingSource  source)
        {
            _source = source;
        }

        public override async UniTask InitAsync()
        {
            Progress = _source.Progress
            .AddTo(_disposable);

            await UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
