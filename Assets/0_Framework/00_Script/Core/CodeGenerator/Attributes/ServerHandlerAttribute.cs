using System;

namespace O2un.Roslyn.Generator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ServerHandlerAttribute : Attribute
    {
    }
}
