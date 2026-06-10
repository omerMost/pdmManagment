using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LeanVault.AddIn.Models;

namespace LeanVault.AddIn.UI
{
    public partial class CheckInAssemblyDialog : UserControl
    {
        public class SelectableFile
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public LockState LockState { get; set; }
            public string OwnerTag { get; set; }

            public bool IsSelected { get; set; }
            public bool CanSelect => LockState != LockState.CheckedOutByOther;

            public string StatusText => LockState == LockState.CheckedOutByOther 
                ? $"Locked by {OwnerTag}" 
                : "Checked out by you";
            public Brush StatusColor => LockState == LockState.CheckedOutByOther 
                ? Brushes.Red 
                : Brushes.DarkOrange;
        }

        public string Comment => CommentTextBox.Text;
        public IEnumerable<string> SelectedFiles => _files.Where(f => f.IsSelected).Select(f => f.FilePath);

        private readonly List<SelectableFile> _files;

        public CheckInAssemblyDialog(IEnumerable<SelectableFile> files)
        {
            InitializeComponent();
            _files = files.ToList();
            FilesListView.ItemsSource = _files;
        }

        private void CheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommentTextBox.Text))
            {
                MessageBox.Show("Please enter a comment.", "Missing Comment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!SelectedFiles.Any())
            {
                MessageBox.Show("Please select at least one file.", "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var win = Window.GetWindow(this);
            if (win != null) win.DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null) win.DialogResult = false;
        }
    }
}
