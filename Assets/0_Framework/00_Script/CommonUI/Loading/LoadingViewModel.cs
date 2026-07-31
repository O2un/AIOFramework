using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.MVVM;
using R3;

namespace O2un.UI
{
    public sealed class LoadingViewModel : ViewModelBase
    {
        // Provider 소유라 AddTo 로 물면 안 된다. 이 VM 이 죽을 때 공유 인스턴스가 끊긴다.
        public ReadOnlyReactiveProperty<float> Progress => _source.Progress;

        private readonly ILoadingSource  _source;
        public LoadingViewModel(ILoadingSource  source)
        {
            _source = source;
        }

        public override async UniTask InitAsync()
        {
            await UniTask.CompletedTask;
        }

        protected override void SafeDispose()
        {
            
        }
    }
}
