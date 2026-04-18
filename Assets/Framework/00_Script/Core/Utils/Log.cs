using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace O2un.Core.Utils
{
    public static class Log
    {
        public enum LogLevel
        {
            Trace, // 디버깅
            Debug, // 디버깅
            Info,  // 일반 정보 (실행 기기정보 등)
            Warning, // 잠재적으로 문제가 발생 할 수 있는 경고
            Error, // 반드시 처리해야하는 에러
            Fatal, // 치명적인 오류 (프로그램 종료해야 하는 수준)
        }

        public enum LogFilter
        {
            None,
            Server,
            Client,
            Etc,
        }

        private static readonly Dictionary<LogFilter, string> CACHE = new();
        private static string ToStringOPT(this LogFilter type)
        {
            lock(CACHE)
            {
                if(CACHE.TryGetValue(type, out var str))
                {
                    return str;
                }
                
                str = type.ToString();
                CACHE.Add(type,str);
                return str;
            }
        }

        private static LogManager _manager;
        private static LogManager Manager => _manager ??= SystemProvider.GetSubsystem<LogManager>();

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Dev(string str, LogLevel type = LogLevel.Debug, [CallerMemberName] string caller = "")
        {
            Print(type, str, LogFilter.None, caller);
        }
        
        [ThreadStatic] static StringBuilder _sb;
        private static StringBuilder SB => _sb ??= new StringBuilder(256);
        public static void Print(LogLevel type, string str, LogFilter filter = LogFilter.None, [CallerMemberName] string caller = "")
        {
            var sb = SB;
            sb.Clear();
            if(LogFilter.None != filter)
            {
                sb.Append("[");
                sb.Append(filter.ToStringOPT());
                sb.Append("]");
            }
            sb.Append("|");
            sb.Append("[");
            sb.Append(caller);
            sb.Append("]");
            sb.Append(" - ");
            sb.Append(str);
            
            var final = sb.ToString();
            LogToEditor(type, final);
            LogToFile(type, final);
            //필요 시 표준 시스템 콘솔 출력
            //System.Console.WriteLine(filtered);
        }

        [System.Diagnostics.Conditional("ENABLE_EDITOR_LOG")]
        private static void LogToEditor(LogLevel type, string str)
        {
            Manager?.LogToEditor(type, str);
        }

        [System.Diagnostics.Conditional("ENABLE_FILE_LOG")]
        private static void LogToFile(LogLevel type, string str)
        {
            Manager?.LogToFile(type, str);
        }
    }
}
