using System.Text.Json;
using System.Text.Json.Serialization;

namespace O2un.Core.Network
{
    public interface IWSS
    {
        void SubscribeNetworkEvent();
    }

    public static class NetworkJson
    {
        public static readonly JsonSerializerOptions Options = new() 
        { 
            PropertyNameCaseInsensitive = true, 
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            IncludeFields = true,
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

                if(jsonString is T str)
                {
                    return str;
                }
                
                return JsonSerializer.Deserialize<T>(jsonString, Options);
            }

            return element.Deserialize<T>(Options);
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

    public static class NetworkUtils
    {
        public static bool TryFindTypedPayload(this JsonElement root, string payloadKey, string typeKey, out string type, out JsonElement typedPayload)
        {
            type = null;
            typedPayload = default;

            JsonElement candidate = root;
            if (!string.IsNullOrEmpty(payloadKey))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {                    
                    return false;
                }

                if (!root.TryGetProperty(payloadKey, out candidate))
                {                    
                    return false;
                }
            }

            if (candidate.ValueKind == JsonValueKind.String)
            {
                string text = candidate.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                text = text.Trim();
                if (text[0] != '{' && text[0] != '[')
                {
                    return false;
                }

                using var document = JsonDocument.Parse(text);
                candidate = document.RootElement.Clone();
            }

            if (candidate.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!candidate.TryGetProperty(typeKey, out JsonElement typeElement))
            {
                return false;
            }

            type = ConvertScalarToString(typeElement);
            if (string.IsNullOrEmpty(type))
            {
                return false;
            }
            typedPayload = candidate.Clone();

            return true;
        }

        private static string ConvertScalarToString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }
    }
}
