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
            public string Guid { get; set; }
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

                // cm listlocks columns: GUID, user, workspace name, server path.
                // A workspace name containing spaces would shift the path column;
                // the GUID and owner (used for Force Release) are unaffected.
                var parts = trimmed.Split(new[] { ' ', '\t' }, 4, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                _locks.Add(new LockEntry
                {
                    Guid = parts[0],
                    Owner = parts[1],
                    FilePath = parts.Length > 3 ? parts[3] : (parts.Length > 2 ? parts[2] : ""),
                });
            }
        }

        private async void ForceRelease_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is LockEntry entry)
            {
                var res = MessageBox.Show($"Force unlock\n{entry.FilePath}?", "Confirm Unlock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    await _cm.ForceUnlockAsync(entry.Guid);
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
