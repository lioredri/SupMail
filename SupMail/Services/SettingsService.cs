using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public List<string> RecentDocuments { get; set; } = new();

        public void AddRecentDocument(string docNum)
        {
            if (string.IsNullOrWhiteSpace(docNum)) return;

            // Remove if exists, then add to front
            RecentDocuments.Remove(docNum);
            RecentDocuments.Insert(0, docNum);

            // Keep only last 10
            if (RecentDocuments.Count > 10)
                RecentDocuments = RecentDocuments.Take(10).ToList();

            Save();
        }

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
