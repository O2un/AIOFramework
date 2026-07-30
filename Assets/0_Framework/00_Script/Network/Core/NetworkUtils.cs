using System.Text.Json;

namespace O2un.Core.Network
{
    public static class NetworkUtils
    {
        public static bool TryFindTypedPayload(this JsonElement root, string payloadKey, string typeKey, out string type, out JsonElement typedPayload)
        {
            type = null;
            typedPayload = default;

            JsonElement candidate = root;
            if (false == string.IsNullOrEmpty(payloadKey))
            {
                if (JsonValueKind.Object != root.ValueKind)
                {
                    return false;
                }

                if (false == root.TryGetProperty(payloadKey, out candidate))
                {
                    return false;
                }
            }

            if (JsonValueKind.String == candidate.ValueKind)
            {
                string text = candidate.GetString();
                if (true == string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                text = text.Trim();
                if ('{' != text[0] && '[' != text[0])
                {
                    return false;
                }

                using var document = JsonDocument.Parse(text);
                candidate = document.RootElement.Clone();
            }

            if (JsonValueKind.Object != candidate.ValueKind)
            {
                return false;
            }

            if (false == candidate.TryGetProperty(typeKey, out JsonElement typeElement))
            {
                return false;
            }

            type = ConvertScalarToString(typeElement);
            if (true == string.IsNullOrEmpty(type))
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
                _ => null,
            };
        }
    }
}
