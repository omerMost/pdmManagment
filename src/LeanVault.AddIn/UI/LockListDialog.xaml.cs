using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LeanVault.AddIn.Services;
using System.Threading.Tasks;

namespace LeanVault.AddIn.UI
{
    public partial class LockListDialog : Window
    {
        public class LockEntry
        {
            public string FilePath { get; set; }
            public string Owner { get; set; }
        }

        private readonly CmCliService _cm;
        private readonly ObservableCollection<LockEntry> _locks = new ObservableCollection<LockEntry>();

        public LockListDialog(CmCliService cm)
        {
            InitializeComponent();
            _cm = cm;
            LocksListView.ItemsSource = _locks;
            Loaded += async (s, e) => await RefreshLocksAsync();
        }

        private async Task RefreshLocksAsync()
        {
            _locks.Clear();
            var raw = await _cm.GetLockListAsync();
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    _locks.Add(new LockEntry
                    {
                        FilePath = parts[0],
                        Owner = parts.Length > 1 ? parts[1] : "Unknown"
                    });
                }
            }
        }

        private async void ForceRelease_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                var res = MessageBox.Show($"Force unlock\n{path}?", "Confirm Unlock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    await _cm.ForceUnlockAsync(path);
                    await RefreshLocksAsync();
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
