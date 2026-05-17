using System;
using System.Runtime.InteropServices;

namespace O2un.Core
{
    [StructLayout(LayoutKind.Explicit)]
    public class UniqueKey : IEquatable<UniqueKey>
    {
        [FieldOffset(0)]
        private readonly long _raw;
    
        [FieldOffset(0)]
        private readonly int _index;
    
        [FieldOffset(4)]
        private readonly int _group;
    
        public long Raw => _raw;
        public int Group => _group;
        public int Index => _index;
    
        public UniqueKey(int group, int index)
        {
            _raw = 0;
            _group = group;
            _index = index;
        }
    
        public UniqueKey(long raw)
        {
            _group = 0;
            _index = 0;
            _raw = raw;
        }
    
        public bool Equals(UniqueKey other) => other != null && _raw == other._raw;
        public override bool Equals(object obj) => obj is UniqueKey other && Equals(other);
        public override int GetHashCode() => _raw.GetHashCode();
        public static implicit operator long(UniqueKey key) => key._raw;
        public static implicit operator UniqueKey(long raw) => new UniqueKey(raw);
        
        public static UniqueKey Undefined => new UniqueKey(0);
        public bool IsDefined => Undefined.Equals(this);
    }
}