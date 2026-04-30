using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace SupMail
{
    public class FileItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public string DisplayName { get; set; } = string.Empty;
        public int Index { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class FileSelectionWindow : Window
    {
        public List<FileItem> FileItems { get; }
        public bool Confirmed { get; private set; }

        public FileSelectionWindow(List<FileItem> items)
        {
            FileItems = items;
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
