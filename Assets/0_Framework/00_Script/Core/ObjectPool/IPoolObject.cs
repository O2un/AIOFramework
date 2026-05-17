using Cysharp.Threading.Tasks;
namespace O2un.Pooling
{
    public interface IPoolObject
    {
        string ID {get; set;}
        void OnDespawn();
    }
    public interface IPoolObject<TData> : IPoolObject
    {
        UniTask InitFromPool(TData data);
    }
}