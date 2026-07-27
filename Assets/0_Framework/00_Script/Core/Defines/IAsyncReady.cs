using Cysharp.Threading.Tasks;

namespace O2un.Core
{
    public interface IAsyncReady
    {
        UniTask WaitUntilReadyAsync();
    }
}
