using System;

namespace O2un
{
    public interface ISafeUpdateable {}

    public interface ISafeUpdate : ISafeUpdateable
    {
        void SafeUpdate();
    }

    public interface ISafeFixedUpdate : ISafeUpdateable
    {
        void SafeFixedUpdate();
    }

    public interface ISafeLateUpdate : ISafeUpdateable
    {
        void SafeLateUpdate();
    }
}
