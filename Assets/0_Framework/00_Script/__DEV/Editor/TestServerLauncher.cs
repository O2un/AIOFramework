using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using O2un.Core.Utils;
using UnityEditor;
using UnityEngine;

namespace O2un.DEV
{
    /// <summary>
    /// 개발용 매치메이킹 서버(.TestServer)의 node 프로세스 수명을 소유한다.
    /// 창과 분리해 두어야 창을 닫아도 프로세스가 남지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class TestServerLauncher
    {
#if UNITY_EDITOR_WIN
        private const string NpmExecutable = "npm.cmd";
#else
        private const string NpmExecutable = "npm";
#endif
        private const string NodeExecutable = "node";
        private const string PidKey = "O2un.TestServer.Pid";
        private const string PidStartTicksKey = "O2un.TestServer.PidStartTicks";
        private const int MaxLines = 500;

        /// <summary>index.js 의 PORT 와 같아야 한다. 여기서는 기동 전 점유 확인에만 쓴다.</summary>
        private const int ServerPort = 8080;

        private static readonly ConcurrentQueue<string> Pending = new();
        private static readonly List<string> Buffer = new();

        private static Process _server;
        private static Process _task;
        private static long _logOffset;
        private static bool _expectServerAlive;

        public static event Action OnLogChanged;

        public static bool IsRunning => IsAlive(_server);
        public static bool IsTaskRunning => IsAlive(_task);
        public static IReadOnlyList<string> Lines => Buffer;

        /// <summary>.TestServer 는 `.` 으로 시작해 Unity 임포트 대상이 아니다. 경로를 직접 조립한다.</summary>
        public static string ServerPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "0_Framework/99_DEV/.TestServer"));

        public static bool HasModules => Directory.Exists(Path.Combine(ServerPath, "node_modules"));

        private static string LogPath => Path.Combine(ServerPath, "server.log");

        static TestServerLauncher()
        {
            // Play 진입이 도메인 리로드다. 리로드에서 죽이면 서버가 필요한 순간에 꺼진다.
            Readopt();

            EditorApplication.quitting += Stop;
            EditorApplication.update += Pump;
        }

        public static void Start()
        {
            if (true == IsRunning) return;

            if (false == HasNode())
            {
                Append("node 를 찾지 못했습니다. https://nodejs.org 에서 설치한 뒤 에디터를 다시 시작하세요.");
                return;
            }

            if (false == HasModules)
            {
                Append("node_modules 가 없습니다. [npm install] 을 먼저 실행하세요.");
                return;
            }

            if (true == IsPortInUse(ServerPort))
            {
                Append($"포트 {ServerPort} 가 이미 사용 중입니다. 이전 실행의 node 가 남아 있는지 확인하세요.");
                return;
            }

            // 리로드가 stdout 파이프를 끊으므로 서버 로그는 server.log 를 따라 읽는다.
            _server = Spawn(NodeExecutable, "index.js", false);
            if (null == _server) return;

            Remember(_server);
            _logOffset = 0;
            _expectServerAlive = true;
            Append($"서버 시작 — pid {_server.Id}");
        }

        public static void Stop()
        {
            Kill(ref _task);

            bool wasRunning = IsAlive(_server);

            _expectServerAlive = false;
            Kill(ref _server);
            Forget();

            if (true == wasRunning) Append("서버 중지");
        }

        public static void Install()
        {
            if (true == IsTaskRunning) return;

            _task = Spawn(NpmExecutable, "install", true);
            if (null == _task) return;

            Append("npm install 실행 중...");
        }

        /// <summary>실행 중인 서버에 소켓 2개로 붙어 방 생성·참가·이탈을 왕복시킨다.</summary>
        public static void Diagnose()
        {
            if (true == IsTaskRunning) return;

            if (false == IsRunning)
            {
                Append("자가 진단은 서버가 실행 중일 때만 됩니다. 먼저 [시작]을 누르세요.");
                return;
            }

            _task = Spawn(NodeExecutable, "smoke.js", true);
            if (null == _task) return;

            Append("자가 진단 실행 중...");
        }

        public static void ClearLog()
        {
            Buffer.Clear();
            OnLogChanged?.Invoke();
        }

        // SessionState 는 리로드를 넘기고 에디터를 닫을 때 비워진다 — 프로세스 수명과 같다.
        private static void Readopt()
        {
            int pid = SessionState.GetInt(PidKey, 0);
            string startTicksText = SessionState.GetString(PidStartTicksKey, string.Empty);

            if (0 == pid || false == long.TryParse(startTicksText, out long startTicks))
            {
                Forget();
                return;
            }

            try
            {
                var process = Process.GetProcessById(pid);

                if (false == process.ProcessName.StartsWith("node", StringComparison.OrdinalIgnoreCase)
                    || startTicks != process.StartTime.ToUniversalTime().Ticks)
                {
                    process.Dispose();
                    Forget();
                    return;
                }

                _server = process;
                _expectServerAlive = true;
            }
            catch (Exception)
            {
                Forget();
            }
        }

        private static void Remember(Process process)
        {
            SessionState.SetInt(PidKey, process.Id);
            SessionState.SetString(PidStartTicksKey, process.StartTime.ToUniversalTime().Ticks.ToString());
        }

        private static void Forget()
        {
            SessionState.EraseInt(PidKey);
            SessionState.EraseString(PidStartTicksKey);
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                foreach (var endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                {
                    if (port == endpoint.Port) return true;
                }
            }
            catch (Exception)
            {
                // NULL
            }

            return false;
        }

        private static bool HasNode()
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo
                {
                    FileName = NodeExecutable,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });

                probe.WaitForExit(3000);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Process Spawn(string fileName, string arguments, bool pipeOutput)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = ServerPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = pipeOutput,
                RedirectStandardError = pipeOutput
            };

            if (true == pipeOutput)
            {
                info.StandardOutputEncoding = Encoding.UTF8;
                info.StandardErrorEncoding = Encoding.UTF8;
            }

            try
            {
                var process = new Process { StartInfo = info, EnableRaisingEvents = true };

                if (true == pipeOutput)
                {
                    process.OutputDataReceived += (_, e) => Enqueue(e.Data);
                    process.ErrorDataReceived += (_, e) => Enqueue(e.Data);
                }

                process.Start();

                if (true == pipeOutput)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                return process;
            }
            catch (Exception ex)
            {
                Append($"{fileName} 실행 실패 — {ex.Message}");
                return null;
            }
        }

        private static void Kill(ref Process process)
        {
            if (false == IsAlive(process))
            {
                process = null;
                return;
            }

            try
            {
                process.Kill();
                process.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Log.Print(Log.LogLevel.Error, $"테스트 서버 프로세스 종료 실패 — {ex.Message}");
            }

            process = null;
        }

        private static bool IsAlive(Process process)
        {
            if (null == process) return false;

            try
            {
                return false == process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 파이프 콜백은 워커 스레드에서 온다. 큐에만 넣고 메인 스레드에서 꺼낸다.
        private static void Enqueue(string line)
        {
            if (true == string.IsNullOrEmpty(line)) return;
            Pending.Enqueue(line);
        }

        private static void Pump()
        {
            TailServerLog();
            Drain();
            WatchServerExit();
        }

        private static void WatchServerExit()
        {
            if (false == _expectServerAlive) return;
            if (true == IsAlive(_server)) return;

            _expectServerAlive = false;
            _server = null;
            Forget();

            Append("서버가 스스로 종료됐습니다. 위 로그에서 원인을 확인하세요.");
        }

        private static void TailServerLog()
        {
            if (false == File.Exists(LogPath)) return;

            try
            {
                using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                // 서버가 재시작하며 파일을 비운 경우. 되감지 않으면 영영 못 읽는다.
                if (stream.Length < _logOffset) _logOffset = 0;
                if (stream.Length == _logOffset) return;

                stream.Seek(_logOffset, SeekOrigin.Begin);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                string line;
                while (null != (line = reader.ReadLine())) Enqueue(line);

                _logOffset = stream.Length;
            }
            catch (IOException)
            {
                // NULL
            }
        }

        private static void Drain()
        {
            if (true == Pending.IsEmpty) return;

            while (Pending.TryDequeue(out string line)) Buffer.Add(line);
            if (MaxLines < Buffer.Count) Buffer.RemoveRange(0, Buffer.Count - MaxLines);

            OnLogChanged?.Invoke();
        }

        private static void Append(string line)
        {
            Enqueue(line);
            Drain();
        }
    }
}
