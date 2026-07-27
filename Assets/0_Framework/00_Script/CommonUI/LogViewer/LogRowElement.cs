using O2un.Core.Utils;
using UnityEngine.UIElements;

namespace O2un.UI
{
    public sealed class LogRowElement : VisualElement
    {
        private readonly Label _timeLabel;
        private readonly Label _levelLabel;
        private readonly Label _messageLabel;

        public LogRowElement()
        {
            AddToClassList("log-row");

            _timeLabel = new Label();
            _timeLabel.AddToClassList("time");

            _levelLabel = new Label();
            _levelLabel.AddToClassList("level");

            _messageLabel = new Label();
            _messageLabel.AddToClassList("message");

            Add(_timeLabel);
            Add(_levelLabel);
            Add(_messageLabel);
        }

        public void Bind(LogData entry, bool isUtc)
        {
            RemoveFromClassList("info");
            RemoveFromClassList("warning");
            RemoveFromClassList("error");

            AddToClassList(NormalizeLevelClass(entry.Level));

            var time = isUtc ? entry.TimeStamp : entry.TimeStamp.ToLocalTime();
            _timeLabel.text = time.ToString("HH:mm:ss.fff");
            _levelLabel.text = entry.Level.ToString();
            _messageLabel.text = entry.Text;
        }

        private static string NormalizeLevelClass(Log.LogLevel level)
        {
            return level switch
            {
                Log.LogLevel.Warning => "warning",
                Log.LogLevel.Error => "error",
                Log.LogLevel.Fatal => "error",
                _ => "info"
            };
        }
    }
}
