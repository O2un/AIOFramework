using O2un.Reactive;
using R3;

namespace O2un.Core
{
    public enum LoadingType
    {
        Scene,
        Patch,
        Resources,
        Mock,
    }

    /// <summary>진행률 읽기 전용 얼굴. UI(ViewModel)가 보는 쪽.</summary>
    public interface ILoadingSource
    {
        ReadOnlyReactiveProperty<float> Progress { get; }
    }

    /// <summary>진행률 쓰기 얼굴. SubSystem(SceneManager 등)이 보는 쪽.</summary>
    public sealed class LoadingRuntime : ReactiveClass<float>, ILoadingSource
    {
        public ReadOnlyReactiveProperty<float> Progress => State;

        public LoadingRuntime() : base(0f)
        {
        }
    }

    public interface ILoadingProvider : ISourceProvider<LoadingType, ILoadingSource, LoadingRuntime>
    {
    }

    public sealed class LoadingProvider : SourceProvider<LoadingType, ILoadingSource, LoadingRuntime>, ILoadingProvider
    {
        protected override void RegistRuntimes()
        {
            _runtimes.Add(LoadingType.Scene, new LoadingRuntime());
        }
    }
}
