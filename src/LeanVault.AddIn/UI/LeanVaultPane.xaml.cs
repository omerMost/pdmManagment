using System.Windows.Controls;
using SolidWorks.Interop.sldworks;
using LeanVault.AddIn.Services;

namespace LeanVault.AddIn.UI
{
    public partial class LeanVaultPane : UserControl
    {
        private readonly LeanVaultPaneViewModel _vm;

        public LeanVaultPane(ISldWorks sw)
        {
            InitializeComponent();
            _vm = new LeanVaultPaneViewModel(sw, new CmCliService());
            DataContext = _vm;
        }

        public void OnActiveDocumentChanged(IModelDoc2 doc)
        {
            _vm.SetActiveDocument(doc);
        }

        public void OnFileSaved(string filePath)
        {
            _vm.OnFileSaved(filePath);
        }

        public void OnDocumentClosing(string filePath)
        {
            _vm.OnDocumentClosing(filePath);
        }

        public void Cleanup()
        {
            _vm.Cleanup();
        }
    }
}
