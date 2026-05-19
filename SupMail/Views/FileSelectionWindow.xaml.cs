using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SupMail.Models;

namespace SupMail.Views
{
    public partial class FileSelectionWindow : Window
    {
        public List<FileItem> FileItems { get; }
        public bool Confirmed { get; private set; }

        private readonly string _docNum;
        private readonly Func<int, Task<string>>? _fileResolver;

        public FileSelectionWindow(List<FileItem> items, string docNum = "", Func<int, Task<string>>? fileResolver = null)
        {
            FileItems = items;
            _docNum = docNum;
            _fileResolver = fileResolver;
            InitializeComponent();
            lstFiles.ItemsSource = FileItems;
        }

        public List<int> GetSelectedIndices()
        {
            return FileItems.Where(f => f.IsSelected).Select(f => f.Index).ToList();
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = chkSelectAll.IsChecked == true;
            foreach (var item in FileItems)
                item.IsSelected = isChecked;
        }

        private async void ZipSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_fileResolver == null) return;

            var selectedItems = FileItems.Where(f => f.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select at least one file.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnZip.IsEnabled = false;
            try
            {
                string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _docNum);
                Directory.CreateDirectory(outputFolder);
                string zipPath = Path.Combine(outputFolder, $"{_docNum}.zip");

                using (var zipStream = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    foreach (var item in selectedItems)
                    {
                        string localPath = await _fileResolver(item.Index);
                        if (!File.Exists(localPath)) continue;

                        string entryName = Path.GetFileName(localPath);
                        if (string.IsNullOrWhiteSpace(entryName))
                            entryName = item.DisplayName;

                        var entry = archive.CreateEntry(entryName);
                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(localPath);
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                MessageBox.Show($"ZIP file created successfully:\n{zipPath}", "Zip Created", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Process.Start("explorer.exe", outputFolder);
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

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!FileItems.Any(f => f.IsSelected))
            {
                MessageBox.Show("Please select at least one file.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
