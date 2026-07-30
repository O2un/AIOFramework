using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace O2un.Core.Network
{
    public static class NetworkJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public static T CommonOptionDeserialize<T>(this JsonElement element)
        {
            if (JsonValueKind.String == element.ValueKind)
            {
                string jsonString = element.GetString();
                if (null == jsonString)
                {
                    return default;
                }

                if (jsonString is T str)
                {
                    return str;
                }

                return JsonSerializer.Deserialize<T>(jsonString, Options);
            }

            return element.Deserialize<T>(Options);
        }

        public static T Deserialize<T>(ReadOnlySpan<byte> payload)
        {
            return JsonSerializer.Deserialize<T>(payload, Options);
        }

        public static byte[] SerializeToUtf8Bytes<T>(T obj)
        {
            return JsonSerializer.SerializeToUtf8Bytes(obj, Options);
        }

        public static string CommonOptionSerialize<T>(this T obj)
        {
            return JsonSerializer.Serialize(obj, Options);
        }

        public static JsonElement CommonOptionSerializeToElement<T>(this T obj)
        {
            return JsonSerializer.SerializeToDocument(obj, Options).RootElement;
        }
    }
}
