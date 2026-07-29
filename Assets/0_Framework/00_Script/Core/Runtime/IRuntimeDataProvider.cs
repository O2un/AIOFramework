namespace O2un.Core.Data
{
    public interface IRuntimeDataProvider
    {
        T Get<T>() where T : RuntimeData<T>, new();
    }
}
