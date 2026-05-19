using System.ComponentModel;

namespace SupMail.Models
{
    public class FileItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public string DisplayName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
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
}
