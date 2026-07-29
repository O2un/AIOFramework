using Cysharp.Threading.Tasks;

namespace O2un.Core
{
    public interface IRootTask : IAsyncReady { }

    public interface IStartupTask : IAsyncReady
    {
        UniTask StartupTaskAsync();
    }
}
