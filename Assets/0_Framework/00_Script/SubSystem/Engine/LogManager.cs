using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Utils;
using UnityEngine;

namespace O2un.Core.Utils
{
    public interface ILogManager : IAsyncReady
    {
        string CurrentLogFilePath { get; }
        UniTask<IReadOnlyList<LogData>> ReadCurrentLogFileFromAsync(CancellationToken ct = default);
    }

    public sealed class LogManager : EngineSubsystemBase, ILogManager
    {
        private const int MAX_LOG_COUNT_BEFORE_FLUSH = 50;
        private const float FLUSH_INTERVAL_SECONDS = 5f;
        private const float UPDATE_INTERVAL_SEC = 1f;
        private const int UPDATE_INTERVAL_MS = (int)(UPDATE_INTERVAL_SEC * 1000);

        private string _currentLogFilePath;
        private float _timeSinceLastFlush;
        private long _readPosition;

        public string CurrentLogFilePath => _currentLogFilePath;

        protected override async UniTask InitAsync()
        {
            InitFileLog();

            Log.Print(Log.LogLevel.Info, "LogManager Initialized");

#if ENABLE_FILE_LOG
            _ = this.StartAsync(UpdateLog);
#endif

            await UniTask.CompletedTask;
        }

#if ENABLE_FILE_LOG
        private void InitFileLog()
        {
            string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");

            if (false == Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            _currentLogFilePath = Path.Combine(logDirectory, $"AppLog_{timestamp}.log");

            Log.InitFile(_currentLogFilePath);
        }

        private async UniTask UpdateLog(CancellationToken ct)
        {
            while (false == ct.IsCancellationRequested)
            {
                await UniTask.Delay(UPDATE_INTERVAL_MS, cancellationToken: ct);

                _timeSinceLastFlush += UPDATE_INTERVAL_SEC;
                if (MAX_LOG_COUNT_BEFORE_FLUSH <= Log.GetPendingCount() || FLUSH_INTERVAL_SECONDS <= _timeSinceLastFlush)
                {
                    await Log.FlushAsync();
                    _timeSinceLastFlush = 0f;
                }
            }
        }
#else
        private void InitFileLog()
        {
            _currentLogFilePath = string.Empty;
        }
#endif

        public async UniTask<IReadOnlyList<LogData>> ReadCurrentLogFileFromAsync(CancellationToken ct = default)
        {
            await Log.FlushAsync();

            if (string.IsNullOrEmpty(_currentLogFilePath))
            {
                return Array.Empty<LogData>();
            }

            if (false == File.Exists(_currentLogFilePath))
            {
                return Array.Empty<LogData>();
            }

            return await UniTask.RunOnThreadPool(() =>
            {
                var result = new List<LogData>();

                using var stream = new FileStream(_currentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long length = stream.Length;
                if (_readPosition < 0 || length < _readPosition)
                {
                    _readPosition = 0;
                }

                stream.Seek(_readPosition, SeekOrigin.Begin);

                using var reader = new StreamReader(stream);

                while (false == reader.EndOfStream)
                {
                    ct.ThrowIfCancellationRequested();

                    string line = reader.ReadLine();

                    if (false == LogData.TryFromJson(line, out LogData logData))
                    {
                        continue;
                    }

                    result.Add(logData);
                }

                _readPosition = length;
                return (IReadOnlyList<LogData>)result;
            }, cancellationToken: ct);
        }

        protected override void SafeDispose()
        {
#if ENABLE_FILE_LOG
            Log.Print(Log.LogLevel.Info, "LogManager Disposed");
            Log.ForceFlush();
#endif
        }
    }
}
