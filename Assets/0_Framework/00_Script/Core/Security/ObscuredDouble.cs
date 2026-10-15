using System;

namespace O2un.Security
{
    public struct ObscuredDouble
    {
        private ObscuredULong _bits;

        public ObscuredDouble(double value)
        {
            _bits = new ObscuredULong((ulong)BitConverter.DoubleToInt64Bits(value));
        }

        public double Value
        {
            get => BitConverter.Int64BitsToDouble((long)_bits.Value);
            set => _bits.Value = (ulong)BitConverter.DoubleToInt64Bits(value);
        }

        public static implicit operator double(ObscuredDouble obscured) => obscured.Value;
        public static implicit operator ObscuredDouble(double value) => new ObscuredDouble(value);
    }
}
