using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SupMail.Helpers;
using SupMail.Models;
using SupMail.Services;

namespace SupMail.Views
{
    public partial class MainWindow : Window
    {
        private readonly ErrorHandler _errorHandler;
        private readonly PriorityApiService _apiService;
        private readonly Func<AttachmentContext, AttachmentActionWindow> _attachmentWindowFactory;

        public MainWindow(
            ErrorHandler errorHandler,
            PriorityApiService apiService,
            Func<AttachmentContext, AttachmentActionWindow> attachmentWindowFactory)
        {
            _errorHandler = errorHandler;
            _apiService = apiService;
            _attachmentWindowFactory = attachmentWindowFactory;
            InitializeComponent();
            LoadRecentDocuments();
        }

        private void LoadRecentDocuments()
        {
            cboDocNumber.Items.Clear();
            foreach (var doc in SettingsService.Current.RecentDocuments)
            {
                cboDocNumber.Items.Add(doc);
            }
        }

        private void cboDocNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnProcess_Click(sender, e);
                e.Handled = true;
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.Services.GetRequiredService<SettingsWindow>();
            settings.Owner = this;
            settings.ShowDialog();
        }

        private async void btnProcess_Click(object sender, RoutedEventArgs e)
        {
            string docNum = cboDocNumber.Text.Trim();
            if (string.IsNullOrEmpty(docNum)) return;

            if (string.IsNullOrEmpty(SettingsService.Current.ApiUrl))
            {
                _errorHandler.ShowWarning("Please configure settings first.", "Configuration Required");
                return;
            }

            btnProcess.IsEnabled = false;
            lblStatus.Text = "Connecting to Priority...";

            try
            {
                var context = await BuildAttachmentContextAsync(docNum);
                lblStatus.Text = "Ready";

                // Add to recent documents
                SettingsService.Current.AddRecentDocument(docNum);
                LoadRecentDocuments();
                cboDocNumber.Text = docNum;

                var actionWindow = _attachmentWindowFactory(context);
                actionWindow.Owner = this;
                actionWindow.ShowDialog();

                lblStatus.Text = "Done";
            }
            catch (Exception ex)
            {
                _errorHandler.Handle(ex, "API");
                lblStatus.Text = "Failed.";
            }
            finally
            {
                btnProcess.IsEnabled = true;
            }
        }

        private async Task<AttachmentContext> BuildAttachmentContextAsync(string docNum)
        {
            string responseBody = await _apiService.GetPurchaseOrderAsync(docNum);

            var json = JObject.Parse(responseBody);
            var recipient = json["AMAIL"]?.ToString() ?? "Unknown Recipient";
            var attachments = json["ITML_SUPMAIL_SUBFORM"] as JArray;

            if (attachments == null || attachments.Count == 0)
                throw new Exception("No files found.");

            var fileItems = new List<FileItem>();
            for (int i = 0; i < attachments.Count; i++)
            {
                string? filePath = attachments[i]["PATH"]?.ToString() ?? attachments[i]["EXTFILENAME"]?.ToString();
                string? displayName = null;

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    displayName = Path.GetFileName(filePath);
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = attachments[i]["EXTFILEDES"]?.ToString();
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Attachment {i + 1}";
                }

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
                    return await _apiService.SaveDataUriToTempFileAsync(sourcePath, file["EXTFILEDES"]?.ToString());
                if (sourcePath.StartsWith("../../system/", StringComparison.OrdinalIgnoreCase))
                    return await _apiService.DownloadSystemAttachmentAsync(sourcePath, displayName);
                return sourcePath;
            };

            return new AttachmentContext
            {
                DocNum = docNum,
                Recipient = recipient,
                Files = fileItems,
                FileResolver = fileResolver
            };
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
