using System;

namespace O2un.Security
{
    internal static class ObscureKey
    {
        private static ulong _state = (ulong)DateTime.UtcNow.Ticks | 1UL;

        public static ulong Next()
        {
            ulong x = _state;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            _state = x;
            return x;
        }
    }
}
