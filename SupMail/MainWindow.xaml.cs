using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SupMail.Properties;
using Newtonsoft.Json.Linq;

namespace SupMail
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.Owner = this;
            settings.ShowDialog();
        }

        private async void btnProcess_Click(object sender, RoutedEventArgs e)
        {
            string docNum = txtDocNumber.Text.Trim();
            if (string.IsNullOrEmpty(docNum)) return;

            if (string.IsNullOrEmpty(Properties.Settings.Default.ApiUrl))
            {
                MessageBox.Show("Please configure settings first.");
                return;
            }

            btnProcess.IsEnabled = false;
            lblStatus.Text = "Connecting to Priority...";

            try
            {
                await RunPriorityFlow(docNum);
                lblStatus.Text = "Success!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
                lblStatus.Text = "Failed.";
            }
            finally
            {
                btnProcess.IsEnabled = true;
            }
        }

        private async Task RunPriorityFlow(string docNum)
        {
            using (HttpClient client = new HttpClient())
            {
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Properties.Settings.Default.Username}:{Properties.Settings.Default.Password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                string url = $"{Properties.Settings.Default.ApiUrl.TrimEnd('/')}/PORDERS('{docNum}')?$select=ORDNAME,AMAIL&$expand=ITML_SUPMAIL_SUBFORM";

                System.Diagnostics.Debug.WriteLine($"[REQUEST URL]: {url}");

                var response = await client.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[RESPONSE STATUS]: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[RESPONSE BODY]: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Request failed with status {response.StatusCode}");
                }

                var json = JObject.Parse(responseBody);
                var recipient = json["AMAIL"]?.ToString() ?? "Unknown Recipient";
                var attachments = json["ITML_SUPMAIL_SUBFORM"] as JArray;

                if (attachments == null || attachments.Count == 0)
                    throw new Exception("No files found.");

                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null) throw new Exception("Outlook is not installed.");

                dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                dynamic mail = outlookApp.CreateItem(0); // 0 = olMailItem
                mail.To = recipient;
                mail.Subject = $"Purchase Order {docNum}";
                                

                int addedAttachments = 0;
                foreach (var file in attachments)
                {
                    string? sourcePath = file["PATH"]?.ToString() ?? file["EXTFILENAME"]?.ToString();
                    if (string.IsNullOrWhiteSpace(sourcePath))
                        continue;

                    string fullPath = sourcePath;
                    if (sourcePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        fullPath = await SaveDataUriToTempFileAsync(sourcePath, file["EXTFILEDES"]?.ToString());
                    }
                    else if (sourcePath.StartsWith("../../system/", StringComparison.OrdinalIgnoreCase))
                    {
                        fullPath = await DownloadSystemAttachmentAsync(client, sourcePath);
                    }

                    if (File.Exists(fullPath))
                    {
                        string? displayName = file["EXTFILEDES"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(displayName))
                            mail.Attachments.Add(fullPath, 1, Type.Missing, displayName);
                        else
                            mail.Attachments.Add(fullPath);
                        addedAttachments++;
                    }
                }

                if (addedAttachments == 0)
                    throw new Exception("No valid attachment files were found on disk.");

                mail.Display();
            }
        }

        private async Task<string> SaveDataUriToTempFileAsync(string dataUri, string? fileDescription)
        {
            string? base64 = NormalizeBase64(dataUri);
            if (string.IsNullOrWhiteSpace(base64))
                throw new Exception("Data URI did not contain valid base64 content.");

            byte[] bytes = Convert.FromBase64String(base64);

            string fileName = !string.IsNullOrWhiteSpace(fileDescription)
                ? fileDescription
                : $"attachment-{Guid.NewGuid():N}.bin";

            string tempFolder = Path.Combine(Path.GetTempPath(), "SupMail", "Attachments");
            Directory.CreateDirectory(tempFolder);

            string tempFile = Path.Combine(tempFolder, $"{Guid.NewGuid():N}_{fileName}");
            await File.WriteAllBytesAsync(tempFile, bytes);

            return tempFile;
        }

        private async Task<string> DownloadSystemAttachmentAsync(HttpClient client, string systemPath)
        {
            string relativePath = systemPath
                .Replace('\\', '/')
                .TrimStart('.')
                .TrimStart('/');

            string requestUrl = $"{Properties.Settings.Default.ApiUrl.TrimEnd('/')}/{relativePath}";
            var response = await client.GetAsync(requestUrl);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download system attachment ({response.StatusCode}).");

            string? base64 = ExtractBase64(responseBody);
            if (string.IsNullOrWhiteSpace(base64))
                throw new Exception("System attachment response did not contain valid base64 content.");

            byte[] bytes = Convert.FromBase64String(base64);

            string fileName = Path.GetFileName(systemPath.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"attachment-{Guid.NewGuid():N}.bin";

            string tempFolder = Path.Combine(Path.GetTempPath(), "SupMail", "Attachments");
            Directory.CreateDirectory(tempFolder);

            string tempFile = Path.Combine(tempFolder, $"{Guid.NewGuid():N}_{fileName}");
            await File.WriteAllBytesAsync(tempFile, bytes);

            return tempFile;
        }

        private static string? ExtractBase64(string responseBody)
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

        private static string? FindBase64InJson(JToken token)
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

        private static string? NormalizeBase64(string? value)
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