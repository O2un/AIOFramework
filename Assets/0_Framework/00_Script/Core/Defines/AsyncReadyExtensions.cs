using Cysharp.Threading.Tasks;

namespace O2un.Core
{
    public static class AsyncReadyExtensions
    {
        public static async UniTask<T> ReadyAsync<T>(this T target) where T : IAsyncReady
        {
            await target.WaitUntilReadyAsync();
            return target;
        }
    }
}
