using System;
using System.IO;
using Newtonsoft.Json;

namespace SupMail.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SupMail",
            "settings.json");

        public static SettingsService Current { get; private set; } = Load();

        public string ApiUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public static SettingsService Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<SettingsService>(json) ?? new SettingsService();
                }
            }
            catch { }
            return new SettingsService();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
                Current = this;
            }
            catch { }
        }
    }
}
