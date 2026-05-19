using System;
using Newtonsoft.Json.Linq;

namespace SupMail.Helpers
{
    public static class FileHelpers
    {
        public static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        public static string? ExtractBase64(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            string raw = responseBody.Trim();

            if (raw.StartsWith("{") || raw.StartsWith("[") || raw.StartsWith("\""))
            {
                try
                {
                    JToken token = JToken.Parse(raw);
                    string? fromJson = FindBase64InJson(token);
                    if (!string.IsNullOrWhiteSpace(fromJson))
                        return fromJson;
                }
                catch
                {
                }
            }

            return NormalizeBase64(raw.Trim('"'));
        }

        public static string? FindBase64InJson(JToken token)
        {
            if (token.Type == JTokenType.String)
                return NormalizeBase64(token.ToString());

            if (token is JObject obj)
            {
                string[] priorityKeys = ["BASE64", "base64", "Content", "content", "Data", "data", "value"];
                foreach (var key in priorityKeys)
                {
                    string? value = obj[key]?.ToString();
                    string? normalized = NormalizeBase64(value);
                    if (!string.IsNullOrWhiteSpace(normalized))
                        return normalized;
                }

                foreach (var property in obj.Properties())
                {
                    string? nested = FindBase64InJson(property.Value);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }
            }

            if (token is JArray array)
            {
                foreach (var item in array)
                {
                    string? nested = FindBase64InJson(item);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }
            }

            return null;
        }

        public static string? NormalizeBase64(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string candidate = value.Trim();
            int markerIndex = candidate.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
                candidate = candidate[(markerIndex + "base64,".Length)..];

            candidate = candidate.Replace("\r", string.Empty).Replace("\n", string.Empty);

            try
            {
                Convert.FromBase64String(candidate);
                return candidate;
            }
            catch
            {
                return null;
            }
        }
    }
}
