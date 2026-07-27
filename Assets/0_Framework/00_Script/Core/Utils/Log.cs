using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;
using UnityEngine;

namespace O2un.Core.Utils
{
    public static class Log
    {
        public enum LogLevel
        {
            Trace,   // 디버깅
            Debug,   // 디버깅
            Info,    // 일반 정보 (실행 기기정보 등)
            Warning, // 잠재적으로 문제가 발생 할 수 있는 경고
            Error,   // 반드시 처리해야하는 에러
            Fatal,   // 치명적인 오류 (프로그램 종료해야 하는 수준)
        }

        public enum LogFilter
        {
            None,
            Server,
            Client,
            Etc,
        }

#if ENABLE_FILE_LOG
        private static readonly ConcurrentQueue<LogData> _logQueue = new();
        private static int _isFlushing;
        private static string _logFilePath = string.Empty;
#endif

        private static readonly object _fileLock = new();

        public static event Action<LogData> Logged;

#if ENABLE_FILE_LOG
        public static void InitFile(string logFilePath)
        {
            _logFilePath = logFilePath ?? string.Empty;
        }
#else
        public static void InitFile(string logFilePath)
        {
        }
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dev(object str, LogLevel type = LogLevel.Debug, [CallerMemberName] string caller = "")
        {
            Print(type, str, LogFilter.None, caller);
        }

        [HideInCallstack]
        public static void Print(LogLevel type, object str, LogFilter filter = LogFilter.None, [CallerMemberName] string caller = "", Exception exception = null)
        {
            string text = string.IsNullOrWhiteSpace(caller) ? str?.ToString() : $"[{caller}] - {str}";
            var data = new LogData(type, text, DateTime.UtcNow, filter);

#if ENABLE_EDITOR_LOG || UNITY_EDITOR
            LogToEditor(type, data.ToDisplayString(), exception);
#endif

#if ENABLE_FILE_LOG
            if (false == string.IsNullOrEmpty(_logFilePath))
            {
                _logQueue.Enqueue(data);
            }
#endif

            Logged?.Invoke(data);
        }

        public static async UniTask FlushAsync()
        {
#if ENABLE_FILE_LOG
            if (string.IsNullOrEmpty(_logFilePath))
            {
                return;
            }

            if (_logQueue.IsEmpty)
            {
                return;
            }

            if (1 == Interlocked.Exchange(ref _isFlushing, 1))
            {
                return;
            }

            try
            {
                string textToWrite = DequeueToText();

                if (string.IsNullOrEmpty(textToWrite))
                {
                    return;
                }

                await UniTask.RunOnThreadPool(() =>
                {
                    lock (_fileLock)
                    {
                        FileIOUtils.StreamAppendFile(_logFilePath, textToWrite);
                    }
                });
            }
            catch (Exception e)
            {
#if ENABLE_EDITOR_LOG || UNITY_EDITOR
                Debug.LogException(e);
#endif
            }
            finally
            {
                Interlocked.Exchange(ref _isFlushing, 0);
            }
#else
            await UniTask.CompletedTask;
#endif
        }

        public static void ForceFlush()
        {
#if ENABLE_FILE_LOG
            if (string.IsNullOrEmpty(_logFilePath))
            {
                return;
            }

            if (_logQueue.IsEmpty)
            {
                return;
            }

            lock (_fileLock)
            {
                string textToWrite = DequeueToText();

                if (string.IsNullOrEmpty(textToWrite))
                {
                    return;
                }

                FileIOUtils.StreamAppendFile(_logFilePath, textToWrite);
            }
#endif
        }

#if ENABLE_FILE_LOG
        public static int GetPendingCount()
        {
            return _logQueue.Count;
        }

        private static string DequeueToText()
        {
            var sb = new StringBuilder();

            while (_logQueue.TryDequeue(out LogData logData))
            {
                sb.AppendLine(logData.ToJson());
            }

            return sb.ToString();
        }
#else
        public static int GetPendingCount()
        {
            return 0;
        }
#endif

#if ENABLE_EDITOR_LOG || UNITY_EDITOR
        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogToEditor(LogLevel type, object str, Exception e)
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

            if (null != e)
            {
                Debug.LogException(e);
            }
        }
#endif
    }
}
