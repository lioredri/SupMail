using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

            if (string.IsNullOrEmpty(AppSettings.Current.ApiUrl))
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
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.Current.Username}:{AppSettings.Current.Password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                string url = $"{AppSettings.Current.ApiUrl.TrimEnd('/')}/PORDERS('{docNum}')?$select=ORDNAME,AMAIL&$expand=ITML_SUPMAIL_SUBFORM";

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

                var fileItems = new List<FileItem>();
                for (int i = 0; i < attachments.Count; i++)
                {
                    string? name = attachments[i]["EXTFILEDES"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        name = $"Attachment {i + 1}";
                    string fileSize = GetFileSizeDisplay(attachments[i]);
                    fileItems.Add(new FileItem { DisplayName = name, FileSize = fileSize, Index = i });
                }

                Func<int, Task<string>> fileResolver = async (idx) =>
                {
                    var file = attachments[idx];
                    string? sourcePath = file["PATH"]?.ToString() ?? file["EXTFILENAME"]?.ToString();
                    if (string.IsNullOrWhiteSpace(sourcePath))
                        throw new Exception("No path available for this attachment.");
                    if (sourcePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        return await SaveDataUriToTempFileAsync(sourcePath);
                    if (sourcePath.StartsWith("../../system/", StringComparison.OrdinalIgnoreCase))
                        return await DownloadSystemAttachmentAsync(client, sourcePath);
                    return sourcePath;
                };

                var selectionWindow = new FileSelectionWindow(fileItems, docNum, fileResolver);
                selectionWindow.Owner = this;
                if (selectionWindow.ShowDialog() != true || !selectionWindow.Confirmed)
                    throw new Exception("File selection was cancelled.");

                var selectedIndices = selectionWindow.GetSelectedIndices();

                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null) throw new Exception("Outlook is not installed.");

                dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                dynamic mail = outlookApp.CreateItem(0); // 0 = olMailItem
                mail.To = recipient;
                mail.Subject = $"Purchase Order {docNum}";

                int addedAttachments = 0;
                foreach (int idx in selectedIndices)
                {
                    var file = attachments[idx];
                    string? sourcePath = file["PATH"]?.ToString() ?? file["EXTFILENAME"]?.ToString();
                    if (string.IsNullOrWhiteSpace(sourcePath))
                        continue;

                    string fullPath = sourcePath;
                    if (sourcePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        fullPath = await SaveDataUriToTempFileAsync(sourcePath);
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

        private static string GetFileSizeDisplay(JToken attachment)
        {
            // 1. Try known Priority / generic size field names
                string? sizeStr = attachment["EXTFILESIZE"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sizeStr) && long.TryParse(sizeStr, out long sz) && sz > 0)
                    return FormatFileSize(sz);
            string? path = attachment["PATH"]?.ToString()
                        ?? attachment["EXTFILENAME"]?.ToString();

            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            // 2. data: URI — estimate from base64 payload length
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int commaIdx = path.IndexOf(',');
                if (commaIdx >= 0)
                    return FormatFileSize((long)((path.Length - commaIdx - 1) * 0.75));
            }

            // 3. Direct (local/UNC) file path — read from disk
            if (!path.StartsWith("../../", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists)
                        return FormatFileSize(info.Length);
                }
                catch { }
            }

            // 4. system path — size not available without downloading
            return string.Empty;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        private async Task<string> SaveDataUriToTempFileAsync(string dataUri)
        {
            string? base64 = NormalizeBase64(dataUri);
            if (string.IsNullOrWhiteSpace(base64))
                throw new Exception("Data URI did not contain valid base64 content.");

            byte[] bytes = Convert.FromBase64String(base64);

            string tempFolder = Path.Combine(Path.GetTempPath(), "SupMail", "Attachments");
            Directory.CreateDirectory(tempFolder);

            string tempFile = Path.Combine(tempFolder, $"{Guid.NewGuid():N}.bin");
            await File.WriteAllBytesAsync(tempFile, bytes);

            return tempFile;
        }

        private async Task<string> DownloadSystemAttachmentAsync(HttpClient client, string systemPath)
        {
            string relativePath = systemPath
                .Replace('\\', '/')
                .TrimStart('.')
                .TrimStart('/');

            string requestUrl = $"{AppSettings.Current.ApiUrl.TrimEnd('/')}/{relativePath}";
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