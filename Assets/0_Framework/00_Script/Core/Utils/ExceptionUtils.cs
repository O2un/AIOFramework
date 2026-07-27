#nullable enable
using System;

namespace O2un.Utils
{
    public static class ExceptionUtils
    {
        public static T ThrowIfNull<T>(this T? value) where T : class
        {
            if (value is null)
            {
                throw new NullReferenceException(nameof(value));
            }

            return value;
        }

        public static T ThrowIfNullInvalidOperation<T>(this T? value, string desc) where T : class
        {
            if (value is null)
            {
                throw new InvalidOperationException($"{nameof(value)} {desc}");
            }

            return value;
        }

        public static T ThrowIfNullUnityObject<T>(this T? value) where T : UnityEngine.Object
        {
            if (null == value)
            {
                throw new UnityEngine.MissingReferenceException();
            }

            return value;
        }
    }
}
