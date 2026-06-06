using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using SupMail.Models;
using SupMail.Services;

namespace SupMail.Views
{
    public partial class AttachmentActionWindow : Window
    {
        private readonly AttachmentContext _context;
        private readonly OutlookService _outlookService;
        private readonly ZipService _zipService;
        private readonly OneDriveService _oneDriveService;
        private readonly ErrorHandler _errorHandler;

        public AttachmentActionWindow(
            AttachmentContext context,
            OutlookService outlookService,
            ZipService zipService,
            OneDriveService oneDriveService,
            ErrorHandler errorHandler)
        {
            _context = context;
            _outlookService = outlookService;
            _zipService = zipService;
            _oneDriveService = oneDriveService;
            _errorHandler = errorHandler;

            InitializeComponent();
            lstFiles.ItemsSource = _context.Files;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Show OneDrive account if logged in
            try
            {
                var accountName = await _oneDriveService.GetCurrentAccountNameAsync();
                if (!string.IsNullOrEmpty(accountName))
                {
                    lblOneDriveAccount.Text = $"☁ OneDrive: {accountName}";
                    lblOneDriveAccount.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                // Ignore - not logged in or not configured
            }
        }

        private List<int> GetSelectedIndices()
        {
            return _context.Files.Where(f => f.IsSelected).Select(f => f.Index).ToList();
        }

        private bool ValidateSelection()
        {
            if (!_context.Files.Any(f => f.IsSelected))
            {
                _errorHandler.ShowWarning("Please select at least one file.", "No Selection");
                return false;
            }
            return true;
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = chkSelectAll.IsChecked == true;
            foreach (var item in _context.Files)
                item.IsSelected = isChecked;
        }

        private async void ZipSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            btnZip.IsEnabled = false;
            try
            {
                var selectedIndices = GetSelectedIndices();
                string zipPath = await _zipService.CreateZipFromFilesAsync(_context, selectedIndices);

                _errorHandler.ShowInfo($"ZIP file created successfully:\n{zipPath}", "Zip Created");
                _zipService.OpenInExplorer(zipPath);
            }
            catch (Exception ex)
            {
                _errorHandler.Handle(ex, "Zip");
            }
            finally
            {
                btnZip.IsEnabled = true;
            }
        }

        private async void AttachSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            btnAttach.IsEnabled = false;
            try
            {
                var selectedIndices = GetSelectedIndices();
                await _outlookService.CreateEmailWithAttachmentsAsync(_context, selectedIndices);
            }
            catch (Exception ex)
            {
                _errorHandler.Handle(ex, "Outlook");
            }
            finally
            {
                btnAttach.IsEnabled = true;
            }
        }

        private void InitializeUploadProgress(int selectedFiles, long totalBytes)
        {
            uploadProgressPanel.Visibility = Visibility.Visible;
            txtUploadFilesProgress.Text = $"0 / {selectedFiles} files";
            txtUploadBytesProgress.Text = $"0 B / {FormatBytes(totalBytes)}";
            pbUploadBytes.Minimum = 0;
            pbUploadBytes.Maximum = totalBytes > 0 ? totalBytes : 1;
            pbUploadBytes.Value = 0;
        }

        private void UpdateUploadProgress(int currentFileIndex, int selectedFiles, long transferredBytes, long totalBytes)
        {
            txtUploadFilesProgress.Text = $"{currentFileIndex} / {selectedFiles} files";
            txtUploadBytesProgress.Text = $"{FormatBytes(transferredBytes)} / {FormatBytes(totalBytes)}";
            pbUploadBytes.Value = Math.Min(transferredBytes, pbUploadBytes.Maximum);
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{bytes} {units[unitIndex]}" : $"{size:0.00} {units[unitIndex]}";
        }

        private async void OneDrive_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            btnOneDrive.IsEnabled = false;
            try
            {
                var selectedIndices = GetSelectedIndices();
                int processedFiles = 0;
                long transferredBytes = 0;

                var uploadItems = new List<(int Index, string LocalPath, long Size, string DisplayName)>();

                foreach (int idx in selectedIndices)
                {
                    var fileItem = _context.Files[idx];

                    // Files without displayed size are considered unavailable; skip silently.
                    if (string.IsNullOrWhiteSpace(fileItem.FileSize))
                        continue;

                    try
                    {
                        string localPath = await _context.FileResolver(idx);
                        long size = new FileInfo(localPath).Length;
                        uploadItems.Add((idx, localPath, size, fileItem.DisplayName));
                    }
                    catch (IOException)
                    {
                        // Skip inaccessible files silently
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible files silently
                    }
                }

                int selectedFiles = uploadItems.Count;
                long totalBytes = uploadItems.Sum(i => i.Size);
                InitializeUploadProgress(selectedFiles, totalBytes);

                // Upload only available files with byte-level progress updates
                foreach (var item in uploadItems.Select((value, index) => (value, index)))
                {
                    long currentFileTransferred = 0;
                    int currentFileIndex = item.index + 1;

                    UpdateUploadProgress(currentFileIndex, selectedFiles, transferredBytes, totalBytes);

                    var progress = new Progress<long>(bytesTransferredInFile =>
                    {
                        currentFileTransferred = Math.Min(bytesTransferredInFile, item.value.Size);
                        UpdateUploadProgress(currentFileIndex, selectedFiles, transferredBytes + currentFileTransferred, totalBytes);
                    });

                    try
                    {
                        await _oneDriveService.UploadFileAsync(item.value.LocalPath, item.value.DisplayName, _context.DocNum, progress);
                        transferredBytes += item.value.Size;
                    }
                    catch (IOException)
                    {
                        // Skip inaccessible files silently
                        transferredBytes += currentFileTransferred;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible files silently
                        transferredBytes += currentFileTransferred;
                    }
                    finally
                    {
                        processedFiles++;
                        int displayedIndex = processedFiles >= selectedFiles ? selectedFiles : Math.Min(processedFiles + 1, selectedFiles);
                        UpdateUploadProgress(displayedIndex, selectedFiles, transferredBytes, totalBytes);
                    }
                }

                // Update account display
                var accountName = await _oneDriveService.GetCurrentAccountNameAsync();
                if (!string.IsNullOrEmpty(accountName))
                {
                    lblOneDriveAccount.Text = $"☁ OneDrive: {accountName}";
                    lblOneDriveAccount.Visibility = Visibility.Visible;
                }

                // Get folder link and copy to clipboard
                string folderLink = await _oneDriveService.GetFolderLinkAsync(_context.DocNum);
                if (!string.IsNullOrEmpty(folderLink))
                {
                    Clipboard.SetText(folderLink);
                    _errorHandler.ShowInfo(
                        $"Files uploaded to OneDrive successfully!\n\n" +
                        $"Folder link copied to clipboard.",
                        "Upload Complete");
                }
                else
                {
                    _errorHandler.ShowInfo("Files uploaded to OneDrive successfully!", "Upload Complete");
                }
            }
            catch (Exception ex)
            {
                _errorHandler.Handle(ex, "OneDrive");
            }
            finally
            {
                btnOneDrive.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
