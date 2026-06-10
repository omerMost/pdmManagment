using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using SolidWorks.Interop.sldworks;
using LeanVault.AddIn.Models;
using LeanVault.AddIn.Services;

namespace LeanVault.AddIn.UI
{
    public class LeanVaultPaneViewModel : INotifyPropertyChanged
    {
        private readonly ISldWorks _sw;
        private readonly CmCliService _cm;

        private IModelDoc2 _activeDoc;
        private FileStatus _activeStatus;
        private readonly DispatcherTimer _pollTimer;

        public LeanVaultPaneViewModel(ISldWorks sw, CmCliService cm)
        {
            _sw = sw;
            _cm = cm;

            CheckOutCommand = new RelayCommand(_ => ExecuteCheckOut(), _ => CanCheckOut);
            CheckOutAllCommand = new RelayCommand(_ => ExecuteCheckOutAll(), _ => HasActiveFile);
            CheckInCommand = new RelayCommand(_ => ExecuteCheckIn(), _ => CanCheckIn);
            UndoCheckOutCommand = new RelayCommand(_ => ExecuteUndoCheckOut(), _ => CanUndo);
            ShowHistoryCommand = new RelayCommand(_ => ExecuteShowHistory(), _ => HasActiveFile);
            CheckInAssemblyCommand = new RelayCommand(_ => ExecuteCheckInAssembly());
            DismissQuickCheckInCommand = new RelayCommand(_ => ShowQuickCheckIn = false);
            RestoreRevisionCommand = new RelayCommand(cs => ExecuteRestore(cs?.ToString()));
            ShowAdminLocksCommand = new RelayCommand(_ => ExecuteShowAdminLocks());

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _pollTimer.Tick += OnPollTimerTick;
            _pollTimer.Start();
        }

        private async void OnPollTimerTick(object sender, EventArgs e)
        {
            if (!HasActiveFile) return;
            await RefreshStatusAsync(ActiveFilePath);
            if (ActiveFilePath != null && ActiveFilePath.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                await RefreshAssemblyTreeAsync(_activeDoc);
        }

        public void Cleanup()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Tick -= OnPollTimerTick;
            }
        }

        // ----------------------------------------------------------------
        // Properties

        private string _activeFileName = "No file open";
        public string ActiveFileName
        {
            get => _activeFileName;
            set => Set(ref _activeFileName, value);
        }

        private string _activeFilePath;
        public string ActiveFilePath
        {
            get => _activeFilePath;
            set => Set(ref _activeFilePath, value);
        }

        public bool HasActiveFile => _activeDoc != null;

        private string _statusText = "—";
        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        private Brush _statusColor = Brushes.Gray;
        public Brush StatusColor
        {
            get => _statusColor;
            set => Set(ref _statusColor, value);
        }

        private string _lockDetail;
        public string LockDetail
        {
            get => _lockDetail;
            set => Set(ref _lockDetail, value);
        }

        public Visibility LockDetailVisibility =>
            string.IsNullOrEmpty(LockDetail) ? Visibility.Collapsed : Visibility.Visible;

        private string _changesetText;
        public string ChangesetText
        {
            get => _changesetText;
            set => Set(ref _changesetText, value);
        }

        // conflict banner
        private string _conflictMessage;
        public string ConflictMessage
        {
            get => _conflictMessage;
            set { Set(ref _conflictMessage, value); OnPropertyChanged(nameof(ConflictBannerVisibility)); }
        }
        public Visibility ConflictBannerVisibility =>
            string.IsNullOrEmpty(ConflictMessage) ? Visibility.Collapsed : Visibility.Visible;

        // busy
        private string _busyMessage;
        public string BusyMessage
        {
            get => _busyMessage;
            set { Set(ref _busyMessage, value); OnPropertyChanged(nameof(BusyVisibility)); }
        }
        public Visibility BusyVisibility =>
            string.IsNullOrEmpty(BusyMessage) ? Visibility.Collapsed : Visibility.Visible;

        // assembly lock warning (amber banner)
        private string _assemblyLockWarning;
        public string AssemblyLockWarning
        {
            get => _assemblyLockWarning;
            set { Set(ref _assemblyLockWarning, value); OnPropertyChanged(nameof(AssemblyLockBannerVisibility)); }
        }
        public Visibility AssemblyLockBannerVisibility =>
            string.IsNullOrEmpty(AssemblyLockWarning) ? Visibility.Collapsed : Visibility.Visible;

        // buttons
        public bool CanCheckOut =>
            _activeStatus?.LockState == LockState.Clean && HasActiveFile;
        public bool CanCheckIn =>
            _activeStatus?.LockState == LockState.CheckedOutByMe && HasActiveFile;
        public bool CanUndo =>
            _activeStatus?.LockState == LockState.CheckedOutByMe && HasActiveFile;

        // quick check-in
        private bool _showQuickCheckIn;
        public bool ShowQuickCheckIn
        {
            get => _showQuickCheckIn;
            set { Set(ref _showQuickCheckIn, value); OnPropertyChanged(nameof(QuickCheckInVisibility)); }
        }
        public Visibility QuickCheckInVisibility =>
            ShowQuickCheckIn ? Visibility.Visible : Visibility.Collapsed;

        // assembly tree
        public ObservableCollection<AssemblyNodeViewModel> AssemblyNodes { get; } = new();
        public Visibility AssemblyTreeVisibility =>
            AssemblyNodes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // history
        public ObservableCollection<HistoryEntryViewModel> HistoryEntries { get; } = new();
        public Visibility HistoryVisibility =>
            HistoryEntries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // ----------------------------------------------------------------
        // Commands

        public ICommand CheckOutCommand { get; }
        public ICommand CheckOutAllCommand { get; }
        public ICommand CheckInCommand { get; }
        public ICommand UndoCheckOutCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand CheckInAssemblyCommand { get; }
        public ICommand DismissQuickCheckInCommand { get; }
        public ICommand RestoreRevisionCommand { get; }
        public ICommand ShowAdminLocksCommand { get; }

        // ----------------------------------------------------------------
        // Document tracking

        public async void SetActiveDocument(IModelDoc2 doc)
        {
            _activeDoc = doc;
            ShowQuickCheckIn = false;
            HistoryEntries.Clear();

            if (doc == null)
            {
                ActiveFileName = "No file open";
                ActiveFilePath = null;
                ClearStatus();
                return;
            }

            var path = doc.GetPathName();
            ActiveFilePath = path;
            ActiveFileName = Path.GetFileName(path);

            await RefreshStatusAsync(path);

            // Walk assembly tree if active doc is .SLDASM
            if (path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                await RefreshAssemblyTreeAsync(doc);
            else
                AssemblyNodes.Clear();

            OnPropertyChanged(nameof(AssemblyTreeVisibility));
            OnPropertyChanged(nameof(HasActiveFile));
        }

        private async Task RefreshStatusAsync(string filePath)
        {
            BusyMessage = "Refreshing status…";
            ConflictMessage = null;

            var result = await _cm.GetStatusAsync(filePath);
            _activeStatus = new FileStatus
            {
                FilePath = filePath,
                LockState = result.LockState,
                LockedBy = result.LockedBy,
                LockedSince = result.LockedSince,
                Changeset = result.Changeset,
            };

            ApplyStatusToUI(_activeStatus);
            BusyMessage = null;
        }

        private void ApplyStatusToUI(FileStatus s)
        {
            switch (s.LockState)
            {
                case LockState.Clean:
                    StatusText = "Clean";
                    StatusColor = Brushes.Green;
                    LockDetail = null;
                    ConflictMessage = null;
                    break;
                case LockState.CheckedOutByMe:
                    StatusText = "Checked out by you";
                    StatusColor = Brushes.DarkOrange;
                    LockDetail = s.LockedSince != null ? $"Since {s.LockedSince}" : null;
                    ConflictMessage = null;
                    break;
                case LockState.CheckedOutByOther:
                    StatusText = "Locked";
                    StatusColor = Brushes.Red;
                    LockDetail = $"Locked by {s.LockedBy}";
                    ConflictMessage = $"Read-Only — Locked by {s.LockedBy}" +
                                      (s.LockedSince != null ? $" since {s.LockedSince}" : "");
                    break;
                case LockState.NotInWorkspace:
                    StatusText = "Not in workspace";
                    StatusColor = Brushes.Gray;
                    LockDetail = null;
                    ConflictMessage = null;
                    break;
                default:
                    ClearStatus();
                    break;
            }

            ChangesetText = s.Changeset != null ? $"Revision: {s.Changeset}" : null;
            OnPropertyChanged(nameof(LockDetailVisibility));
            OnPropertyChanged(nameof(CanCheckOut));
            OnPropertyChanged(nameof(CanCheckIn));
            OnPropertyChanged(nameof(CanUndo));
        }

        private void ClearStatus()
        {
            StatusText = "—";
            StatusColor = Brushes.Gray;
            LockDetail = null;
            ChangesetText = null;
            ConflictMessage = null;
        }

        // ----------------------------------------------------------------
        // Check-out (OME-207)

        private async void ExecuteCheckOut()
        {
            var path = ActiveFilePath;
            if (string.IsNullOrEmpty(path)) return;
            BusyMessage = "Checking out…";
            await _cm.CheckOutAsync(path);
            await RefreshStatusAsync(path);
        }

        private async void ExecuteCheckOutAll()
        {
            var toCheckOut = AssemblyNodes
                .Where(n => n.LockState == LockState.Clean)
                .Select(n => n.FilePath)
                .ToList();
            if (toCheckOut.Count == 0) return;

            BusyMessage = $"Checking out {toCheckOut.Count} files…";
            foreach (var path in toCheckOut)
                await _cm.CheckOutAsync(path);
            await RefreshAssemblyTreeAsync(_activeDoc);
            BusyMessage = null;
        }

        // ----------------------------------------------------------------
        // Check-in (OME-208)

        private async void ExecuteCheckIn()
        {
            var path = ActiveFilePath;
            if (string.IsNullOrEmpty(path)) return;

            var comment = PromptComment();
            if (comment == null) return;

            BusyMessage = "Checking in…";
            ShowQuickCheckIn = false;

            // Append SW custom properties as structured block
            var props = ReadSwProperties(_activeDoc);
            var fullComment = BuildComment(comment, props);

            await _cm.CheckInAsync(path, fullComment);
            await RefreshStatusAsync(path);
        }

        private string PromptComment()
        {
            var dlg = new CheckInDialog();
            var win = new System.Windows.Window
            {
                Title = "Check In",
                Content = dlg,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
            };
            return win.ShowDialog() == true ? dlg.Comment : null;
        }

        // ----------------------------------------------------------------
        // Undo check-out (OME-209)

        private async void ExecuteUndoCheckOut()
        {
            var path = ActiveFilePath;
            if (string.IsNullOrEmpty(path)) return;

            var confirm = MessageBox.Show(
                $"Discard all local changes to\n{Path.GetFileName(path)}\nand release the lock?",
                "Undo Check-Out", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            BusyMessage = "Undoing checkout…";
            await _cm.UndoCheckOutAsync(path);
            await RefreshStatusAsync(path);
        }

        // ----------------------------------------------------------------
        // History (OME-223 foundation)

        private async void ExecuteShowHistory()
        {
            if (string.IsNullOrEmpty(ActiveFilePath)) return;
            BusyMessage = "Loading history…";
            var raw = await _cm.GetLogAsync(ActiveFilePath);
            BusyMessage = null;

            HistoryEntries.Clear();
            foreach (var entry in ParseLog(raw))
                HistoryEntries.Add(entry);
            OnPropertyChanged(nameof(HistoryVisibility));
        }

        private async void ExecuteRestore(string changeset)
        {
            if (string.IsNullOrEmpty(changeset) || string.IsNullOrEmpty(ActiveFilePath)) return;
            var confirm = MessageBox.Show(
                $"Restore {Path.GetFileName(ActiveFilePath)} to {changeset}?\nThis will overwrite local changes.",
                "Restore Revision", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            BusyMessage = $"Restoring {changeset}…";
            await _cm.GetRevisionAsync(ActiveFilePath, changeset);
            BusyMessage = null;
            await RefreshStatusAsync(ActiveFilePath);
        }

        private void ExecuteShowAdminLocks()
        {
            var dlg = new LockListDialog(_cm);
            dlg.ShowDialog();
        }

        // ----------------------------------------------------------------
        // Assembly check-in (OME-220)

        private async void ExecuteCheckInAssembly()
        {
            if (AssemblyNodes.Count == 0 && _activeStatus == null) return;

            var modifiedFiles = new List<CheckInAssemblyDialog.SelectableFile>();

            if (_activeStatus != null && (_activeStatus.LockState == LockState.CheckedOutByMe || _activeStatus.LockState == LockState.CheckedOutByOther))
            {
                modifiedFiles.Add(new CheckInAssemblyDialog.SelectableFile
                {
                    FilePath = _activeStatus.FilePath,
                    FileName = Path.GetFileName(_activeStatus.FilePath),
                    LockState = _activeStatus.LockState,
                    OwnerTag = _activeStatus.LockedBy,
                    IsSelected = _activeStatus.LockState == LockState.CheckedOutByMe
                });
            }

            foreach (var n in AssemblyNodes)
            {
                if (n.LockState == LockState.CheckedOutByMe || n.LockState == LockState.CheckedOutByOther)
                {
                    modifiedFiles.Add(new CheckInAssemblyDialog.SelectableFile
                    {
                        FilePath = n.FilePath,
                        FileName = n.FileName,
                        LockState = n.LockState,
                        OwnerTag = n.OwnerTag,
                        IsSelected = n.LockState == LockState.CheckedOutByMe
                    });
                }
            }

            if (modifiedFiles.Count == 0)
            {
                MessageBox.Show("No checked-out files found in the assembly.", "Nothing to check in", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (modifiedFiles.Any(n => n.LockState == LockState.CheckedOutByOther))
            {
                MessageBox.Show("Some parts are locked by other engineers. You cannot check them in.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var dlg = new CheckInAssemblyDialog(modifiedFiles);
            var win = new Window
            {
                Title = "Check In Assembly",
                Content = dlg,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
            };

            if (win.ShowDialog() == true)
            {
                var selectedFiles = dlg.SelectedFiles.ToList();
                if (selectedFiles.Count == 0) return;

                BusyMessage = "Checking in assembly...";
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(dlg.Comment);

                // Add properties for the active assembly
                var props = ReadSwProperties(_activeDoc);
                AppendPropsBlock(sb, ActiveFileName, props);

                // Add properties for each selected part
                foreach (var path in selectedFiles)
                {
                    if (string.Equals(path, ActiveFilePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var loadedDoc = _sw.GetOpenDocumentByName(path) as IModelDoc2;
                    if (loadedDoc != null)
                    {
                        var partProps = ReadSwProperties(loadedDoc);
                        if (partProps.Count > 0)
                        {
                            AppendPropsBlock(sb, Path.GetFileName(path), partProps);
                        }
                    }
                }

                await _cm.CheckInMultipleAsync(selectedFiles, sb.ToString());
                
                // OME-226: Auto-export BOM
                try
                {
                    await ExportAndCheckInBomAsync((IAssemblyDoc)_activeDoc, ActiveFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export BOM: {ex.Message}", "BOM Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await RefreshStatusAsync(ActiveFilePath);
                await RefreshAssemblyTreeAsync(_activeDoc);
            }
        }

        private async Task ExportAndCheckInBomAsync(IAssemblyDoc assembly, string assemblyPath)
        {
            var extractor = new SwBomExtractor(_sw);
            var bom = extractor.Extract(assembly);

            var repoRoot = FindRepoRoot(assemblyPath);
            if (string.IsNullOrEmpty(repoRoot)) return;

            var bomDir = Path.Combine(repoRoot, "04_Releases", "BOM");
            if (!Directory.Exists(bomDir))
                Directory.CreateDirectory(bomDir);

            // Get the current changeset number for the assembly (after check-in)
            var status = await _cm.GetStatusAsync(assemblyPath);
            var cs = status.Changeset?.Replace("cs:", "") ?? "latest";

            var assyName = Path.GetFileNameWithoutExtension(assemblyPath);
            var csvPath = Path.Combine(bomDir, $"{assyName}_cs{cs}.csv");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Part Number,Description,Quantity,Material,Revision");
            foreach (var row in bom)
            {
                sb.AppendLine($"\"{EscapeCsv(row.PartNumber)}\",\"{EscapeCsv(row.Description)}\",{row.Quantity},\"{EscapeCsv(row.Material)}\",\"{EscapeCsv(row.Revision)}\"");
            }

            File.WriteAllText(csvPath, sb.ToString());

            // Make sure the file is added before checking in
            await _cm.AddAsync(csvPath);
            await _cm.CheckInAsync(csvPath, $"Auto-export BOM for {assyName} at cs:{cs}");
        }

        private static string EscapeCsv(string value) => value?.Replace("\"", "\"\"") ?? "";

        private static string FindRepoRoot(string path)
        {
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".plastic")))
                    return dir;
                // If no .plastic dir is found, just go up until we find a root-like structure
                if (Directory.Exists(Path.Combine(dir, "SharedParts")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null; // or fallback
        }

        // ----------------------------------------------------------------
        // FileSave prompt (OME-210)

        public void OnFileSaved(string filePath)
        {
            if (_activeStatus?.LockState != LockState.CheckedOutByMe) return;
            if (!string.Equals(filePath, ActiveFilePath, StringComparison.OrdinalIgnoreCase)) return;
            ShowQuickCheckIn = true;
        }

        // ----------------------------------------------------------------
        // DocumentClose warning (OME-211)

        public void OnDocumentClosing(string filePath)
        {
            if (_activeStatus?.LockState != LockState.CheckedOutByMe) return;
            if (!string.Equals(filePath, ActiveFilePath, StringComparison.OrdinalIgnoreCase)) return;

            var result = MessageBox.Show(
                $"{Path.GetFileName(filePath)} is still checked out.\n\nCheck in before closing?",
                "File Still Checked Out",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                CheckInCommand.Execute(null);
        }

        // ----------------------------------------------------------------
        // Assembly tree refresh (M4)

        private async Task RefreshAssemblyTreeAsync(IModelDoc2 doc)
        {
            AssemblyNodes.Clear();
            var assy = doc as IAssemblyDoc;
            if (assy == null) return;

            var walker = new SwAssemblyWalker();
            var refs = walker.GetAllReferencedFiles(assy);

            var tasks = new List<Task<(string path, CmStatusResult status)>>();
            foreach (var r in refs)
            {
                var filePath = r.FilePath;
                tasks.Add(Task.Run(async () =>
                {
                    var status = await _cm.GetStatusAsync(filePath);
                    return (filePath, status);
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Re-clear to avoid duplicates if multiple refresh calls overlap slightly
            AssemblyNodes.Clear();
            foreach (var res in results.OrderBy(x => Path.GetFileName(x.path)))
            {
                var s = res.status;
                AssemblyNodes.Add(new AssemblyNodeViewModel
                {
                    FilePath = res.path,
                    LockState = s.LockState,
                    OwnerTag = s.LockState == LockState.CheckedOutByOther ? s.LockedBy : 
                               s.LockState == LockState.CheckedOutByMe ? "(you)" : "",
                });
            }

            var locked = results.Where(r => r.status.LockState == LockState.CheckedOutByOther).ToList();
            AssemblyLockWarning = locked.Count > 0
                ? $"⚠ {locked.Count} part(s) locked:\n" +
                  string.Join("\n", locked.Select(r => $"  {Path.GetFileName(r.path)} — {r.status.LockedBy}"))
                : null;

            OnPropertyChanged(nameof(AssemblyTreeVisibility));
        }

        // ----------------------------------------------------------------
        // SW property reader (OME-212 foundation)

        private static Dictionary<string, string> ReadSwProperties(IModelDoc2 doc)
        {
            var props = new Dictionary<string, string>();
            if (doc == null) return props;

            var customPropMgr = doc.Extension.CustomPropertyManager[""];
            if (customPropMgr == null) return props;

            string[] names = (string[])customPropMgr.GetNames() ?? Array.Empty<string>();
            foreach (var name in names)
            {
                customPropMgr.Get4(name, false, out string val, out string resolvedVal);
                props[name] = resolvedVal ?? val ?? "";
            }
            return props;
        }

        // ----------------------------------------------------------------
        // Comment builder (OME-213)

        private static void AppendPropsBlock(System.Text.StringBuilder sb, string fileName, Dictionary<string, string> props)
        {
            if (props == null || props.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine($"[LV:PROPS {fileName}]");
            foreach (var kv in props)
                sb.AppendLine($"{kv.Key}: {kv.Value}");
            sb.AppendLine($"[/LV:PROPS]");
        }

        private static string BuildComment(string userComment, Dictionary<string, string> props)
        {
            if (props == null || props.Count == 0)
                return userComment;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(userComment);
            AppendPropsBlock(sb, "Active", props);
            return sb.ToString();
        }

        // ----------------------------------------------------------------
        // Log parser (simple — OME-223)

        private static IEnumerable<HistoryEntryViewModel> ParseLog(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;
            
            // Format expected: cs:1045 | johndoe | 2026-06-09 09:32:11 | "Fix flange thickness"
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("cs:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(new[] { '|' }, 4, StringSplitOptions.RemoveEmptyEntries);
                    yield return new HistoryEntryViewModel
                    {
                        Changeset = parts.Length > 0 ? parts[0].Trim() : trimmed,
                        Author = parts.Length > 1 ? parts[1].Trim() : "",
                        Comment = parts.Length > 3 ? parts[3].Trim() : "",
                    };
                }
            }
        }

        // ----------------------------------------------------------------
        // INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    // ----------------------------------------------------------------
    // Supporting view-models

    public class AssemblyNodeViewModel
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public LockState LockState { get; set; }
        public Brush StatusColor => LockState switch
        {
            LockState.Clean => Brushes.Green,
            LockState.CheckedOutByMe => Brushes.DarkOrange,
            LockState.CheckedOutByOther => Brushes.Red,
            _ => Brushes.Gray,
        };
        public string OwnerTag { get; set; }
    }

    public class HistoryEntryViewModel
    {
        public string Changeset { get; set; }
        public string Author { get; set; }
        public string Comment { get; set; }
    }

    // ----------------------------------------------------------------

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object p) => _canExecute == null || _canExecute(p);
        public void Execute(object p) => _execute(p);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
