using System.Windows;
using System.Windows.Controls;

namespace LeanVault.AddIn.UI
{
    public partial class CheckInDialog : UserControl
    {
        public string Comment { get; private set; }

        public CheckInDialog()
        {
            InitializeComponent();
            Loaded += (_, __) => CommentBox.Focus();
        }

        private void OnCheckIn(object sender, RoutedEventArgs e)
        {
            var comment = CommentBox.Text.Trim();
            if (string.IsNullOrEmpty(comment))
            {
                ValidationMsg.Visibility = Visibility.Visible;
                return;
            }
            Comment = comment;
            Window.GetWindow(this).DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = false;
        }
    }
}
