using System;
using UnityEngine;

namespace O2un
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class RequireComponentFieldAttribute : Attribute
    {
    }
}
