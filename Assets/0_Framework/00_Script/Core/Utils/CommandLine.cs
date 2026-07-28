using System;
using UnityEngine;

namespace O2un.Utils
{
    public static class CommandLine
    {
        public static bool HasArgument(string argument)
        {
            var arguments = Environment.GetCommandLineArgs();

            foreach(var value in arguments)
            {
                if(string.Equals(value, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetArgument(string argument, string defaultValue)
        {
            var arguments = Environment.GetCommandLineArgs();

            for(var i = 0; i < arguments.Length - 1; i++)
            {
                if(string.Equals(arguments[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }

            return defaultValue;
        }

        public static string GetArgument(string[] arguments, string key, string defaultValue)
        {
            for(var i = 0; i < arguments.Length - 1; i++)
            {
                if(string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }

            return defaultValue;
        }

        public static ushort GetUShortArgument(string[] arguments, string key, ushort defaultValue)
        {
            var value = GetArgument(arguments, key, defaultValue.ToString());

            if(ushort.TryParse(value, out var result))
            {
                return result;
            }

            return defaultValue;
        }
    }
}
