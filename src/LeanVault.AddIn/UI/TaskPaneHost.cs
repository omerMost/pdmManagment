using System.Windows.Forms;
using System.Windows.Forms.Integration;
using SolidWorks.Interop.sldworks;

namespace LeanVault.AddIn.UI
{
    /// <summary>
    /// WinForms Panel that SolidWorks hosts via ITaskpaneView.DisplayWindowFromHandle.
    /// Bridges to a WPF UserControl via ElementHost.
    /// </summary>
    public class TaskPaneHost : Panel
    {
        public LeanVaultPane Pane { get; }

        public TaskPaneHost(ISldWorks sw)
        {
            Dock = DockStyle.Fill;
            Pane = new LeanVaultPane(sw);
            var host = new ElementHost { Dock = DockStyle.Fill, Child = Pane };
            Controls.Add(host);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Pane?.Cleanup();
            }
            base.Dispose(disposing);
        }
    }
}
