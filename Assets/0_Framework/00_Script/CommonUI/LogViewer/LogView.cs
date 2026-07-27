using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using O2un.MVVM;
using O2un.Utils;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class LogView : ViewBaseToolkit<LogVM>
    {
        private IReadOnlyList<LogData> _currentItems;
        private ListView _logList;
        private VisualElement _emptyState;
        private VisualElement _copyToast;
        private Label _filePath;
        private Label _statusLabel;
        private Label _countLabel;
        private Label _detailText;
        private Label _utcToggle;

        private bool _autoScroll = true;
        private bool _isUtcTime = true;

        private LogData _selectedLog;

        protected override void BindElements(VisualElement root)
        {
            root.QRequiredBinding<Toggle>("InfoToggle").ValueChangedValue<bool>(b => Model.ToggleLevel(Log.LogLevel.Info, b)).AddTo(DisposableR3);
            root.QRequiredBinding<Toggle>("WarningToggle").ValueChangedValue<bool>(b => Model.ToggleLevel(Log.LogLevel.Warning, b)).AddTo(DisposableR3);
            root.QRequiredBinding<Toggle>("ErrorToggle").ValueChangedValue<bool>(b => Model.ToggleLevel(Log.LogLevel.Error, b)).AddTo(DisposableR3);

            root.QRequiredBinding<Toggle>("AutoScrollToggle").ValueChangedValue<bool>(b => _autoScroll = b).AddTo(DisposableR3);
            root.QRequiredBinding<Toggle>("LiveToggle").ValueChangedValue<bool>(b => Model.LiveSwitch(b)).AddTo(DisposableR3);

            root.QRequiredBinding<TextField>("SearchField").ValueChangedValue<string>(s => Model.SetSearchString(s)).AddTo(DisposableR3);
            root.QRequiredBinding<TextField>("MaxCount").ValueChangedValue<string>(s => Model.SetMaxCount(s)).AddTo(DisposableR3);

            root.QRequiredBinding<Button>("CopyDetailButton").Clicked(CopyDetails).AddTo(DisposableR3);

            root.QRequiredBinding<Toggle>("UTCToggle").ValueChangedValue<bool>(b =>
            {
                _utcToggle.text = b ? "UTC" : "LOCAL";
                _isUtcTime = b;
                _logList.Rebuild();
            }).AddTo(DisposableR3);

            _utcToggle = root.QRequired<Label>("UTCToggleLabel");
            _statusLabel = root.QRequired<Label>("StatusLabel");
            _countLabel = root.QRequired<Label>("CountLabel");
            _filePath = root.QRequired<Label>("FilePathLabel");
            _detailText = root.QRequired<Label>("DetailText");

            _logList = root.QRequiredBinding<ListView>("LogList")
                           .SelectChanged(OnLogSelectChanged)
                           .AddTo(DisposableR3).Element;

            _emptyState = root.QRequired<VisualElement>("EmptyState");
            _copyToast = root.QRequired<VisualElement>("CopyToast");

            BindListView();
        }

        protected override void BindModel()
        {
            Model.LogDataList.Subscribe(OnLogDataListChanged).AddTo(DisposableR3);
            Model.StatusText.Subscribe(s => _statusLabel.text = s).AddTo(DisposableR3);

            _filePath.text = Model.CurrentLogFilePath;
        }

        private void BindListView()
        {
            _logList.fixedItemHeight = 30f;
            _logList.selectionType = SelectionType.Single;
            _logList.makeItem = MakeLogRow;
            _logList.bindItem = BindLogRow;
        }

        private VisualElement MakeLogRow()
        {
            return new LogRowElement();
        }

        private void BindLogRow(VisualElement element, int index)
        {
            if (null == _currentItems) return;
            if (index < 0) return;
            if (_currentItems.Count <= index) return;
            if (element is not LogRowElement row) return;

            row.Bind(_currentItems[index], _isUtcTime);
        }

        private void OnLogDataListChanged(IReadOnlyList<LogData> logs)
        {
            _currentItems = logs ?? new List<LogData>();

            _logList.itemsSource = _currentItems is System.Collections.IList list
                ? list
                : new List<LogData>(_currentItems);

            _logList.schedule.Execute(() =>
            {
                _logList.Rebuild();

                int count = _currentItems.Count;
                if (0 == count)
                {
                    _emptyState.RemoveFromClassList("u-invisible");
                }
                else
                {
                    _emptyState.AddToClassList("u-invisible");
                }

                if (0 < count && _autoScroll)
                {
                    _logList.ScrollToItem(count - 1);
                }

                _countLabel.text = $"{count} logs";
            }).ExecuteLater(1);
        }

        private void OnLogSelectChanged(IEnumerable<object> selectedItems)
        {
            foreach (object selectedItem in selectedItems)
            {
                if (selectedItem is not LogData log)
                {
                    continue;
                }

                SetDetail(log);
                return;
            }
        }

        public void SetDetail(LogData log)
        {
            _selectedLog = log;
            _detailText.text = null != _selectedLog ? log.ToDetailedString() : "로그 선택";
        }

        private void CopyDetails()
        {
            if (null == _selectedLog)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = _selectedLog.ToDetailedString();

            this.StartExclusiveAsync("copyToast", async ct =>
            {
                _copyToast.RemoveFromClassList("u-invisible");
                await UniTask.Delay(1000, cancellationToken: ct);
                _copyToast.AddToClassList("u-invisible");
            });
        }
    }
}
