using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SupMail.Services
{
    public static class AppConfig
    {
        private static JObject? _config;

        public static string OneDriveClientId
        {
            get
            {
                // First try build-time embedded secret
                if (!string.IsNullOrWhiteSpace(AppSecrets.OneDriveClientId))
                    return AppSecrets.OneDriveClientId;

                // Fallback to appsettings.json for local development
                return GetValue("OneDriveClientId");
            }
        }

        private static string GetValue(string key)
        {
            if (_config == null)
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _config = JObject.Parse(json);
                }
                else
                {
                    _config = new JObject();
                }
            }

            return _config[key]?.ToString() ?? string.Empty;
        }
    }
}
