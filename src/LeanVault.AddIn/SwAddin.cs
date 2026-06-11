using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using LeanVault.AddIn.UI;

namespace LeanVault.AddIn
{
    [ComVisible(true)]
    [Guid("C1D2E3F4-A5B6-7890-CDEF-123456789ABC")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
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
            try
            {
                Log("ConnectToSW start");
                _sw = (ISldWorks)thisSW;
                _addinCookie = cookie;
                _sw.SetAddinCallbackInfo2(0, this, cookie);

                CreateTaskPane();
                AttachSwEvents();
                Log("ConnectToSW complete");
                return true;
            }
            catch (Exception ex)
            {
                Log("ConnectToSW failed: " + ex);
                MessageBox.Show(
                    "LeanVault failed to load:\n\n" + ex.Message,
                    "LeanVault Add-in",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            Log("DisconnectFromSW start");
            if (_sw != null)
                DetachSwEvents();
            _taskpane?.DeleteView();
            _taskpane = null;
            _taskpaneHost?.Dispose();
            _taskpaneHost = null;
            if (_sw != null)
                Marshal.ReleaseComObject(_sw);
            _sw = null;
            Log("DisconnectFromSW complete");
            return true;
        }

        private void CreateTaskPane()
        {
            _taskpaneHost = new TaskPaneHost(_sw);
            // CreateTaskpaneView2(iconPath, title) — empty string uses default icon
            _taskpane = _sw.CreateTaskpaneView2("", "LeanVault");
            _taskpane.DisplayWindowFromHandlex64(_taskpaneHost.Handle.ToInt64());
            Log("Task pane created. Handle=" + _taskpaneHost.Handle);
        }

        private void AttachSwEvents()
        {
            var swEvents = (SldWorks)_sw;
            swEvents.FileOpenNotify2 += OnFileOpen;
            swEvents.ActiveDocChangeNotify += OnActiveDocChange;
            swEvents.FileCloseNotify += OnFileClose;
        }

        private void DetachSwEvents()
        {
            var swEvents = (SldWorks)_sw;
            swEvents.FileOpenNotify2 -= OnFileOpen;
            swEvents.ActiveDocChangeNotify -= OnActiveDocChange;
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
            key.SetValue(null, 1, RegistryValueKind.DWord);
            key.SetValue("Description", "LeanVault — Plastic SCM PDM integration for SolidWorks");
            key.SetValue("Title", "LeanVault");

            var versionKey = Registry.LocalMachine.CreateSubKey(
                $@"SOFTWARE\SolidWorks\SOLIDWORKS 2021\Addins\{{{t.GUID}}}");
            versionKey.SetValue(null, 1, RegistryValueKind.DWord);
            versionKey.SetValue("Description", "LeanVault - Plastic SCM PDM integration for SolidWorks");
            versionKey.SetValue("Title", "LeanVault");

            var startup = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\SolidWorks\AddInsStartup\{{{t.GUID}}}");
            startup.SetValue(null, 1, RegistryValueKind.DWord);
            var startupValues = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\SolidWorks\AddInsStartup");
            startupValues.SetValue("{" + t.GUID + "}", 1, RegistryValueKind.DWord);
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            Registry.LocalMachine.DeleteSubKey(
                $@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}", throwOnMissingSubKey: false);
            Registry.LocalMachine.DeleteSubKey(
                $@"SOFTWARE\SolidWorks\SOLIDWORKS 2021\Addins\{{{t.GUID}}}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKey(
                $@"SOFTWARE\SolidWorks\AddInsStartup\{{{t.GUID}}}", throwOnMissingSubKey: false);
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "LeanVault.addin.log"),
                    DateTime.Now.ToString("s") + " " + message + System.Environment.NewLine);
            }
            catch
            {
                // Logging must never block SolidWorks add-in loading.
            }
        }

        #endregion
    }
}
