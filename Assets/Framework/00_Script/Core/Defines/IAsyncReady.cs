using Cysharp.Threading.Tasks;
using UnityEngine;

namespace O2un.DI
{
    public interface IAsyncReady
    {
        UniTask WaitUntilReadyAsync();
    }
}
