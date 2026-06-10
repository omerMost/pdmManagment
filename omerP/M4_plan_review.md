# M4 Assembly Awareness — Plan Review

**Date:** 2026-06-09
**Reviewer:** Claude (LeanVault session)
**Plan source:** Agent implementation plan for Milestone 4

---

## Summary Verdict

The plan is mostly solid and the core approach is correct. Four issues must be addressed before starting M4 code.

---

## What the Plan Gets Right

- **Locking behavior**: "Warn but allow, uncheck locked items by default" — matches PRD and OME-222 exactly
- **`CheckInAssemblyDialog` with checkboxes** — right UX approach, matches OME-220 spec
- **`CheckInMultipleAsync` added to `CmCliService`** — correct place, correct signature
- **Open question answered**: `cm checkin "file1" "file2" ... --comment "..."` is supported directly. No response file needed unless assembly has 200+ unique files — unlikely in practice

---

## Issue 1 — M3 Is Already Implemented; Linear Doesn't Reflect It

The code in `src/LeanVault.AddIn/` already contains M3 features. Linear still shows them as Backlog.

| M3 Issue | Feature | Code Status |
|---|---|---|
| OME-212 | SW property reader | ✅ `ReadSwProperties()` in `LeanVaultPaneViewModel.cs` |
| OME-213 | `[LV:PROPS]` block in changeset comment | ✅ Built inside `ExecuteCheckIn()` |
| OME-214 | Red lock conflict banner | ✅ `ConflictBannerVisibility` + red banner in `LeanVaultPane.xaml` |
| OME-215 | Status color icons (green/yellow/red/gray) | ✅ `StatusColor` + status dot in XAML |
| OME-216 | 30s background polling timer | ⚠️ Missing — status refresh is event-driven only, no timer |
| OME-217 | Unit tests for parsers | ✅ `CmStatusParserTests.cs` exists in `LeanVault.Tests` |

**Action required before starting M4:**
1. Mark OME-212, 213, 214, 215, 217 as **Done** in Linear
2. Implement OME-216 (30s `DispatcherTimer`) — ~20 lines, low risk, needed for M4 tree refresh too

---

## Issue 2 — Assembly Walker Must Be a Separate Class (not inline in ViewModel)

The plan puts `IAssemblyDoc.GetComponents` → `IComponent2.GetPathName` logic directly inside `RefreshAssemblyTreeAsync` in `LeanVaultPaneViewModel.cs`.

Per our architecture and OME-218, the walking logic belongs in its own service class:

```
src/LeanVault.AddIn/Services/SwAssemblyWalker.cs
```

```csharp
public class SwAssemblyWalker
{
    public IReadOnlyList<AssemblyFileRef> GetAllReferencedFiles(IAssemblyDoc assembly)
    { ... }
}

public record AssemblyFileRef(string FilePath, bool IsMissing, bool IsVirtual, int Depth);
```

The ViewModel calls the walker; it does not contain the walk logic.

**Why this matters:**
- OME-218 is independently completable and testable as a unit
- Keeps the ViewModel focused on UI state
- Makes the walker reusable for `lv checkin-assembly` CLI (M5)

---

## Issue 3 — `GetComponents(false)` Returns a Flat Array, Not a Tree

The plan calls `GetComponents(false)` and then populates the tree view. The SolidWorks API `IAssemblyDoc.GetComponents(bool topLevelOnly)` with `topLevelOnly = false` returns a **flat `object[]`** containing every component at every depth — there is no parent/child structure in the returned array.

**Two options — pick one before coding:**

| Option | Approach | Effort | Result |
|---|---|---|---|
| **A — Flat list (recommended for M4)** | Use the flat array, sort by file path, display as a flat list in the task pane | Low | Acceptable — engineers care about status, not tree shape |
| **B — Proper tree** | Walk `IComponent2.GetChildren()` recursively to build hierarchy | Medium | Matches the nested tree mockup in the PRD |

Option A is faster and perfectly functional. Option B can be a follow-up if engineers request it.

---

## Issue 4 — Assembly Tree Polling Refresh Is Missing

The plan only triggers `RefreshAssemblyTreeAsync` on document open/change events. OME-222 requires the tree to update on the background poll interval — otherwise a locked part that gets released by a colleague will stay showing red until the engineer switches away and back.

**Fix:** When OME-216 (30s timer) fires, if the active document is an assembly, also call `RefreshAssemblyTreeAsync()`. The tree status fetch is already parallel (one `GetStatusAsync` per file), so the poll hit is acceptable.

---

## Recommended Pre-M4 Checklist

- [ ] Mark OME-212, 213, 214, 215, 217 as Done in Linear
- [ ] Implement OME-216: add `DispatcherTimer` in `LeanVaultPaneViewModel` constructor, 30s interval, calls `RefreshStatusAsync()` + `RefreshAssemblyTreeAsync()` if active doc is assembly
- [ ] Create `SwAssemblyWalker.cs` as a separate service class (OME-218)
- [ ] Decide flat list vs. tree hierarchy for assembly tree view and note it in the plan
- [ ] Confirm `cm checkin "f1" "f2" --comment "..."` works with a real multi-file test on the LAN server before coding the full flow

---

## Current Code State Reference

```
src/LeanVault.AddIn/
  SwAddin.cs                        ← COM entry, event wiring (FileOpen, ActiveDocChange, FileSave, FileClose)
  Services/CmCliService.cs          ← async cm wrapper (GetStatus, CheckOut, CheckIn, Undo, GetLog, GetLockList, ForceUnlock)
  Services/CmStatusResult.cs        ← typed status parse result
  Models/FileStatus.cs              ← LockState enum (Unknown, Clean, CheckedOutByMe, CheckedOutByOther, NotInWorkspace)
  UI/LeanVaultPane.xaml             ← WPF task pane: conflict banner, status dot, action buttons, assembly tree section, history panel
  UI/LeanVaultPaneViewModel.cs      ← MVVM: all commands, stubs for M4 (RefreshAssemblyTreeAsync, ExecuteCheckInAssembly)
  UI/TaskPaneHost.cs                ← WinForms Panel + ElementHost bridge
  UI/CheckInDialog.xaml             ← commit message dialog (already exists)
src/LeanVault.Tests/
  CmStatusParserTests.cs            ← xUnit: Clean, CheckedOutByOther, NotInWorkspace, ParsesChangeset
```

**M4 stubs already in ViewModel:**
- `AssemblyNodes` — `ObservableCollection<AssemblyNodeViewModel>` (populated by `RefreshAssemblyTreeAsync`)
- `CheckInAssemblyCommand` → `ExecuteCheckInAssembly()` shows "Coming in M4" dialog
- `AssemblyNodeViewModel` — FilePath, FileName, LockState, StatusColor, OwnerTag

**Files to create for M4:**
- `src/LeanVault.AddIn/Services/SwAssemblyWalker.cs` (OME-218)
- `src/LeanVault.AddIn/UI/CheckInAssemblyDialog.xaml` + `.xaml.cs` (OME-220)

**Files to modify for M4:**
- `CmCliService.cs` — add `CheckInMultipleAsync(IEnumerable<string>, string)` (OME-220)
- `LeanVaultPaneViewModel.cs` — implement `RefreshAssemblyTreeAsync` + `ExecuteCheckInAssembly` (OME-219, 220)
- `LeanVaultPaneViewModel.cs` — add `DispatcherTimer` for 30s polling (OME-216)
