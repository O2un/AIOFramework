using O2un.Core;

namespace O2un.Data
{
    public abstract class StaticData
    {
        public UniqueKey Key {get; init;}

        public abstract bool Set();
        public abstract bool Link();
    }
}