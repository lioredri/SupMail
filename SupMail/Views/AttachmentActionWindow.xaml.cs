using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public AttachmentActionWindow(AttachmentContext context)
        {
            _context = context;
            _outlookService = new OutlookService();
            _zipService = new ZipService();

            InitializeComponent();
            lstFiles.ItemsSource = _context.Files;
        }

        private List<int> GetSelectedIndices()
        {
            return _context.Files.Where(f => f.IsSelected).Select(f => f.Index).ToList();
        }

        private bool ValidateSelection()
        {
            if (!_context.Files.Any(f => f.IsSelected))
            {
                MessageBox.Show("Please select at least one file.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                MessageBox.Show($"ZIP file created successfully:\n{zipPath}", "Zip Created", MessageBoxButton.OK, MessageBoxImage.Information);
                _zipService.OpenInExplorer(zipPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create ZIP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Failed to create email: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnAttach.IsEnabled = true;
            }
        }

        private async void OneDrive_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            btnOneDrive.IsEnabled = false;
            try
            {
                var oneDriveService = new OneDriveService();
                var selectedIndices = GetSelectedIndices();

                foreach (int idx in selectedIndices)
                {
                    var fileItem = _context.Files[idx];
                    string localPath = await _context.FileResolver(idx);
                    await oneDriveService.UploadFileAsync(localPath, fileItem.DisplayName, _context.DocNum);
                }

                MessageBox.Show("Files uploaded to OneDrive successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to upload to OneDrive: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
