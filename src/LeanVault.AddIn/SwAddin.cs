using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using LeanVault.AddIn.UI;

namespace LeanVault.AddIn
{
    [ComVisible(true)]
    [Guid("C1D2E3F4-A5B6-7890-CDEF-123456789ABC")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SwAddin : ISwAddin
    {
        private ISldWorks _sw;
        private int _addinCookie;
        private ITaskpaneView _taskpane;
        private TaskPaneHost _taskpaneHost;

        // SW event sink cookies
        private int _docOpenCookie;
        private int _activeDocChangeCookie;

        public bool ConnectToSW(object thisSW, int cookie)
        {
            _sw = (ISldWorks)thisSW;
            _addinCookie = cookie;
            _sw.SetAddinCallbackInfo2(0, this, cookie);

            CreateTaskPane();
            AttachSwEvents();
            return true;
        }

        public bool DisconnectFromSW()
        {
            DetachSwEvents();
            _taskpane?.DeleteView();
            _taskpane = null;
            _taskpaneHost?.Dispose();
            _taskpaneHost = null;
            Marshal.ReleaseComObject(_sw);
            _sw = null;
            return true;
        }

        private void CreateTaskPane()
        {
            _taskpaneHost = new TaskPaneHost(_sw);
            // CreateTaskpaneView2(iconPath, title) — empty string uses default icon
            _taskpane = _sw.CreateTaskpaneView2("", "LeanVault");
            _taskpane.DisplayWindowFromHandle(_taskpaneHost.Handle.ToInt32());
        }

        private void AttachSwEvents()
        {
            var swEvents = (SldWorks)_sw;
            swEvents.FileOpenNotify2 += OnFileOpen;
            swEvents.ActiveDocChangeNotify += OnActiveDocChange;
            swEvents.FileSaveNotify += OnFileSave;
            swEvents.FileCloseNotify += OnFileClose;
        }

        private void DetachSwEvents()
        {
            var swEvents = (SldWorks)_sw;
            swEvents.FileOpenNotify2 -= OnFileOpen;
            swEvents.ActiveDocChangeNotify -= OnActiveDocChange;
            swEvents.FileSaveNotify -= OnFileSave;
            swEvents.FileCloseNotify -= OnFileClose;
        }

        private int OnFileOpen(string fileName)
        {
            _taskpaneHost?.Pane.OnActiveDocumentChanged(_sw.ActiveDoc as IModelDoc2);
            return 0;
        }

        private int OnActiveDocChange()
        {
            _taskpaneHost?.Pane.OnActiveDocumentChanged(_sw.ActiveDoc as IModelDoc2);
            return 0;
        }

        private int OnFileSave(string fileName)
        {
            _taskpaneHost?.Pane.OnFileSaved(fileName);
            return 0;
        }

        private int OnFileClose(string fileName, int reason)
        {
            _taskpaneHost?.Pane.OnDocumentClosing(fileName);
            return 0;
        }

        #region COM registration

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            var key = Registry.LocalMachine.CreateSubKey(
                $@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}");
            key.SetValue(null, 0);
            key.SetValue("Description", "LeanVault — Plastic SCM PDM integration for SolidWorks");
            key.SetValue("Title", "LeanVault");
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            Registry.LocalMachine.DeleteSubKey(
                $@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}", throwOnMissingSubKey: false);
        }

        #endregion
    }
}
