using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
        
#if ENABLE_FILE_LOG
        private static readonly ConcurrentQueue<string> _logQueue = new();
#endif
        private static readonly object _fileLock = new();
        
        private const int MAX_LOG_COUNT_BEFORE_FLUSH = 50;
        private const float FLUSH_INTERVAL_SECONDS = 5f;
#if ENABLE_FILE_LOG
        private static float _timeSinceLastFlush = 0f;
        private static string _logFilePath = "";
#endif
        public static void Init()
        {
#if ENABLE_FILE_LOG
            string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logDirectory, $"AppLog_{timestamp}.log");
            
            Application.quitting -= FlushToFile;
            Application.quitting += FlushToFile;
#endif
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dev(string str, LogLevel type = LogLevel.Debug, [CallerMemberName] string caller = "")
        {
            Print(type, str, LogFilter.None, caller);
        }
        
        [ThreadStatic] static StringBuilder _sb;
        private static StringBuilder SB => _sb ??= new StringBuilder(256);
        [HideInCallstack]
        public static void Print(LogLevel type, string str, LogFilter filter = LogFilter.None, [CallerMemberName] string caller = "")
        {
            var sb = SB;
            sb.Clear();
            
#if ENABLE_FILE_LOG
            sb.Append("[");
            sb.Append(DateTime.Now.ToString("MM-dd HH:mm:ss.fff"));
            sb.Append("] ");
            int timePrefixLength = sb.Length;
#endif
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
            
#if ENABLE_FILE_LOG
            var editorMsg = sb.ToString(timePrefixLength, sb.Length - timePrefixLength);
            LogToEditor(type, editorMsg);
            var fileMsg = sb.ToString();
            _logQueue.Enqueue(fileMsg);
#else
            var final = sb.ToString();
            LogToEditor(type, final);
#endif
        }

        [System.Diagnostics.Conditional("ENABLE_EDITOR_LOG"),System.Diagnostics.Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogToEditor(LogLevel type, string str)
        {
            switch (type)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(str);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(str);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    Debug.LogError(str);
                    break;
            }
        }
        
        [System.Diagnostics.Conditional("ENABLE_FILE_LOG")]
        private static void FlushToFile()
        {
#if ENABLE_FILE_LOG
            if (_logQueue.IsEmpty) return;

            lock (_fileLock)
            {
                using var stream = File.Open(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                
                while (_logQueue.TryDequeue(out string logLine))
                {
                    writer.WriteLine(logLine);
                }
            }

            _timeSinceLastFlush = 0f;
#endif
        }
        
        [System.Diagnostics.Conditional("ENABLE_FILE_LOG")]
        public static void UpdateFlush(float deltaTime)
        {
#if ENABLE_FILE_LOG
            _timeSinceLastFlush += deltaTime;
            if (_logQueue.Count >= MAX_LOG_COUNT_BEFORE_FLUSH || _timeSinceLastFlush >= FLUSH_INTERVAL_SECONDS)
            {
                FlushToFile();
            }
#endif
        }
    }
}
