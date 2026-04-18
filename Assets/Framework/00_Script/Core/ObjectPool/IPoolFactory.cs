using Cysharp.Threading.Tasks;
using UnityEngine;

namespace O2un.Pooling
{
    public interface IPoolFactory<T> where T : Component
    {
        UniTask PreloadAsync();
        T Create();
        void Release();
    }
}
