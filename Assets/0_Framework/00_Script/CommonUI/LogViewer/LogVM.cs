using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using O2un.MVVM;
using O2un.Utils;
using R3;
using UnityEngine.InputSystem;

namespace O2un.UI
{
    public sealed class LogVM : ViewModelBase
    {
        private readonly ILogManager _manager;

        private readonly Dictionary<Log.LogLevel, bool> _levelToggle = new();
        private readonly List<LogData> _logDataList = new();
        private readonly ReactiveProperty<string> _statusText = new();
        private readonly ReactiveProperty<IReadOnlyList<LogData>> _visibleList = new(new List<LogData>());

        private string _searchString = string.Empty;
        private bool _isLiveUpdate = true;
        private bool _isDirty;
        private int _maxVisibleLogCount = 500;

        public string CurrentLogFilePath => _manager.CurrentLogFilePath;
        public ReadOnlyReactiveProperty<string> StatusText => _statusText;
        public ReadOnlyReactiveProperty<IReadOnlyList<LogData>> LogDataList => _visibleList;

        public LogVM(ILogManager manager)
        {
            _manager = manager;
        }

        public override async UniTask InitAsync()
        {
            await base.InitAsync();

            BindCommand();
            foreach (Log.LogLevel type in Enum.GetValues(typeof(Log.LogLevel)))
            {
                _levelToggle[type] = true;
            }

            _statusText.Value = "Loading current runtime log...";
            _logDataList.AddRange(await _manager.ReadCurrentLogFileFromAsync());
            _statusText.Value = "Loaded";
            _isDirty = true;

            _ = this.StartAsync(UpdateLogs);
        }

        private async UniTask UpdateLogs(CancellationToken ct)
        {
            while (false == ct.IsCancellationRequested)
            {
                await UniTask.Delay(1000, cancellationToken: ct);

                if (false == IsVisible.CurrentValue) continue;
                await LoadCurrentRuntimeLogAsync(ct);
                Filter();
            }
        }

        public async UniTask LoadCurrentRuntimeLogAsync(CancellationToken ct = default)
        {
            if (false == _isLiveUpdate) return;

            await _manager.WaitUntilReadyAsync();
            var addedList = await _manager.ReadCurrentLogFileFromAsync(ct);
            _logDataList.AddRange(addedList);

            _isDirty |= 0 != addedList.Count;
        }

        // Ctrl+Alt+L 을 1초 안에 두 번 눌러야 토글된다. 오작동으로 열리는 것을 막기 위함.
        private void BindCommand()
        {
            var comboStream = Observable.EveryUpdate().Where(_ => IsTogglePressed());

            comboStream
                .Select(_ => comboStream.Take(1).TakeUntil(Observable.Timer(TimeSpan.FromSeconds(1))))
                .Switch()
                .Subscribe(_ => SetVisible(false == IsVisible.CurrentValue))
                .AddTo(DisposableR3);
        }

        private static bool IsTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;

            if (null == keyboard) return false;
            if (false == keyboard.ctrlKey.isPressed) return false;
            if (false == keyboard.altKey.isPressed) return false;

            return keyboard[Key.L].wasPressedThisFrame;
        }

        private void Filter()
        {
            if (false == _isDirty) return;

            _visibleList.Value = _logDataList.Where(Filter).TakeLast(_maxVisibleLogCount).ToList();
            _isDirty = false;
        }

        private bool Filter(LogData data)
        {
            if (false == _levelToggle[data.Level])
            {
                return false;
            }

            return data.Text.Contains(_searchString);
        }

        internal void ClearLog()
        {
            _logDataList.Clear();
            _isDirty = true;
        }

        internal void ToggleLevel(Log.LogLevel level, bool isOn)
        {
            switch (level)
            {
                case Log.LogLevel.Trace:
                case Log.LogLevel.Debug:
                case Log.LogLevel.Info:
                    _levelToggle[Log.LogLevel.Trace] = isOn;
                    _levelToggle[Log.LogLevel.Debug] = isOn;
                    _levelToggle[Log.LogLevel.Info] = isOn;
                    break;
                case Log.LogLevel.Warning:
                    _levelToggle[Log.LogLevel.Warning] = isOn;
                    break;
                case Log.LogLevel.Error:
                case Log.LogLevel.Fatal:
                    _levelToggle[Log.LogLevel.Error] = isOn;
                    _levelToggle[Log.LogLevel.Fatal] = isOn;
                    break;
            }

            _isDirty = true;
        }

        internal void SetSearchString(string s)
        {
            _searchString = s ?? string.Empty;
            _isDirty = true;
        }

        internal void LiveSwitch(bool isLive)
        {
            _isLiveUpdate = isLive;
        }

        internal void SetMaxCount(string s)
        {
            if (int.TryParse(s, out _maxVisibleLogCount))
            {
                _isDirty = true;
            }
        }

        protected override void SafeDispose()
        {
            _statusText.Dispose();
            _logDataList.Clear();
            _visibleList.Dispose();

            base.SafeDispose();
        }
    }
}
