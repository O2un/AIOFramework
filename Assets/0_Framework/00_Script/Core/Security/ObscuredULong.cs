using System;

namespace O2un.Security
{
    /// <summary>
    /// 메모리 스캐너가 값을 그대로 찾지 못하게 XOR 로 가려 들고 있는 ulong. 쓸 때마다 키를 바꿔 증감 스캔으로도 추적되지 않는다.
    /// </summary>
    public struct ObscuredULong : IEquatable<ObscuredULong>
    {
        private ulong _key;
        private ulong _cipher;

        public ObscuredULong(ulong value)
        {
            _key = ObscureKey.Next();
            _cipher = value ^ _key;
        }

        public ulong Value
        {
            get => _cipher ^ _key;
            set
            {
                _key = ObscureKey.Next();
                _cipher = value ^ _key;
            }
        }

        // 기본 struct 동등성은 키까지 비교해 같은 값도 다르다고 본다 — record 상태 비교가 값 기준이 되게 한다.
        public bool Equals(ObscuredULong other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ObscuredULong other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static implicit operator ulong(ObscuredULong obscured) => obscured.Value;
        public static implicit operator ObscuredULong(ulong value) => new ObscuredULong(value);
    }
}
