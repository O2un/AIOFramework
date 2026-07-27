using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace O2un.Core.Utils
{
    public sealed class LogData
    {
        private static readonly JsonSerializerOptions JSON_OPTIONS = CreateJsonOptions();

        public Log.LogLevel Level { get; set; }
        public Log.LogFilter Filter { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Text { get; set; }

        public LogData()
        {
        }

        public LogData(Log.LogLevel level, object text, DateTime? timestamp = null, Log.LogFilter filter = Log.LogFilter.None)
        {
            Level = level;
            Filter = filter;
            TimeStamp = timestamp ?? DateTime.UtcNow;
            Text = text?.ToString() ?? "null";
        }

        public string ToDisplayString()
        {
            return Log.LogFilter.None != Filter ? $"[{Filter}] {Text}" : Text;
        }

        public string ToDetailedString()
        {
            StringBuilder sb = new();
            sb.Append("Level :"); sb.Append(Level); sb.AppendLine();
            sb.Append("Filter :"); sb.Append(Filter); sb.AppendLine();
            sb.Append("UTC :"); sb.Append(TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff")); sb.AppendLine();
            sb.Append("LOCAL :"); sb.Append(TimeStamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")); sb.AppendLine();
            sb.Append("Log :"); sb.Append(Text);

            return sb.ToString();
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JSON_OPTIONS);
        }

        public static bool TryFromJson(string json, out LogData logData)
        {
            logData = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                logData = JsonSerializer.Deserialize<LogData>(json, JSON_OPTIONS);
                return null != logData;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public override string ToString()
        {
            return ToDisplayString();
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
