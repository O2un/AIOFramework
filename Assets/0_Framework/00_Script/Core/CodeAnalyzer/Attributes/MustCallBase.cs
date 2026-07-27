using System;

namespace O2un.Roslyn.Analyzer
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MustCallBaseAttribute : Attribute
    {
    }
}
