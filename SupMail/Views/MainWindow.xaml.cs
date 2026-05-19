using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;
using SupMail.Helpers;
using SupMail.Models;
using SupMail.Services;

namespace SupMail.Views
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

            if (string.IsNullOrEmpty(SettingsService.Current.ApiUrl))
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
            var apiService = new PriorityApiService();

            string responseBody = await apiService.GetPurchaseOrderAsync(docNum);

            var json = JObject.Parse(responseBody);
            var recipient = json["AMAIL"]?.ToString() ?? "Unknown Recipient";
            var attachments = json["ITML_SUPMAIL_SUBFORM"] as JArray;

            if (attachments == null || attachments.Count == 0)
                throw new Exception("No files found.");

            var fileItems = new List<FileItem>();
            for (int i = 0; i < attachments.Count; i++)
            {
                string? displayName = attachments[i]["EXTFILEDES"]?.ToString();
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = $"Attachment {i + 1}";

                string fileSize = GetFileSizeDisplay(attachments[i]);
                fileItems.Add(new FileItem
                {
                    DisplayName = displayName,
                    FileSize = fileSize,
                    Index = i
                });
            }

            Func<int, Task<string>> fileResolver = async (idx) =>
            {
                var file = attachments[idx];
                string? sourcePath = file["PATH"]?.ToString() ?? file["EXTFILENAME"]?.ToString();
                string? displayName = file["EXTFILEDES"]?.ToString();
                if (string.IsNullOrWhiteSpace(sourcePath))
                    throw new Exception("No path available for this attachment.");
                if (sourcePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return await apiService.SaveDataUriToTempFileAsync(sourcePath, file["EXTFILEDES"]?.ToString());
                if (sourcePath.StartsWith("../../system/", StringComparison.OrdinalIgnoreCase))
                    return await apiService.DownloadSystemAttachmentAsync(sourcePath, displayName);
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
            dynamic mail = outlookApp.CreateItem(0);
            mail.To = recipient;
            mail.Subject = $"Purchase Order {docNum}";

            int addedAttachments = 0;
            foreach (int idx in selectedIndices)
            {
                var file = attachments[idx];
                string? sourcePath = file["PATH"]?.ToString() ?? file["EXTFILENAME"]?.ToString();
                string? displayName = file["EXTFILEDES"]?.ToString();
                if (string.IsNullOrWhiteSpace(sourcePath))
                    continue;

                string fullPath = sourcePath;
                if (sourcePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = await apiService.SaveDataUriToTempFileAsync(sourcePath, displayName);
                }
                else if (sourcePath.StartsWith("../../system/", StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = await apiService.DownloadSystemAttachmentAsync(sourcePath, displayName);
                }

                if (File.Exists(fullPath))
                {
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

        private static string GetFileSizeDisplay(JToken attachment)
        {
            string? sizeStr = attachment["EXTFILESIZE"]?.ToString();
            if (!string.IsNullOrWhiteSpace(sizeStr) && long.TryParse(sizeStr, out long sz) && sz > 0)
                return FileHelpers.FormatFileSize(sz);

            string? path = attachment["PATH"]?.ToString()
                        ?? attachment["EXTFILENAME"]?.ToString();

            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int commaIdx = path.IndexOf(',');
                if (commaIdx >= 0)
                    return FileHelpers.FormatFileSize((long)((path.Length - commaIdx - 1) * 0.75));
            }

            if (!path.StartsWith("../../", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists)
                        return FileHelpers.FormatFileSize(info.Length);
                }
                catch { }
            }

            return string.Empty;
        }
    }
}
