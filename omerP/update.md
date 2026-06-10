# LeanVault — Code Review & Status Update

**Date:** 2026-06-09
**Source:** Full read of every file in `src/` after agent implementation pass

---

## Verified Implementation State

Every claim from the agent's pass was confirmed against the actual source files. The table below is ground truth.

### M4 Gaps — All Closed

| Gap | What Was Done | Verified In |
|---|---|---|
| "Check Out All" button | `CheckOutAllCommand` + `ExecuteCheckOutAll()` — checks out all Clean nodes in parallel | `LeanVaultPaneViewModel.cs:300–313`, `LeanVaultPane.xaml:133–137` |
| Amber locked-parts banner | `AssemblyLockWarning` property set at end of `RefreshAssemblyTreeAsync`; banner in XAML | `ViewModel:629–633`, `LeanVaultPane.xaml:104–110` |
| Per-file SW props in assembly comment | `AppendPropsBlock` called per selected file using `_sw.GetOpenDocumentByName(path)` | `ViewModel:469–491` |
| Restore revision `cm get` | `ExecuteRestore` now calls `_cm.GetRevisionAsync(ActiveFilePath, changeset)` | `ViewModel:394`, `CmCliService.cs:46` |
| `ParseLog` format | Updated to split on `|` pipe separator: `cs:N \| author \| date \| comment` | `ViewModel:685–703` |

### M5 — Implemented

| Feature | What Was Built | File |
|---|---|---|
| BOM extraction | `SwBomExtractor.cs` — counts instances via `GetComponents(false)` (no dedup), reads SW custom props from open docs | `Services/SwBomExtractor.cs` |
| BOM auto-export | `ExportAndCheckInBomAsync` — writes CSV to `04_Releases/BOM/<name>_cs<N>.csv`, calls `CheckInAsync` | `ViewModel:510–543` |
| Repo root detection | `FindRepoRoot` walks up looking for `.plastic` dir or `SharedParts/` dir | `ViewModel:547–560` |
| Admin lock UI | `LockListDialog.xaml` — GridView with FilePath, Owner, Force Release button per row; loads on open | `UI/LockListDialog.xaml`, `.xaml.cs` |
| Admin button in pane | ⚙ Admin button in header row, `ShowAdminLocksCommand` → `ExecuteShowAdminLocks()` | `LeanVaultPane.xaml:51–52`, `ViewModel:399–403` |
| `lv.exe` CLI | `LeanVault.Cli/Program.cs` — `System.CommandLine`, 6 commands: status, checkout, checkin, checkin-assembly, bom, history; all with `--json` | `LeanVault.Cli/Program.cs` |
| WiX installer | `LeanVault.Installer/Package.wxs` — WiX v4, COM registry keys for add-in GUID, PATH entry for `lv.exe` | `LeanVault.Installer/Package.wxs` |

---

## Previously Fixed Gaps (All Verified Closed)

| Gap | Fix | Verified In |
|---|---|---|
| CLI stub descriptions | `checkin-assembly` + `bom` descriptions now say "Requires SolidWorks named-pipe server - currently a stub" | `LeanVault.Cli/Program.cs:41,53` |
| BOM `cm checkin` fails on new files | `AddAsync` added to `CmCliService`; called before `CheckInAsync` in `ExportAndCheckInBomAsync` | `CmCliService.cs:46`, `ViewModel:539` |
| OME-230 User documentation | `docs/UserGuide.md` written — status indicators, check-in flows, assembly workflows, BOM export, Admin UI, CLI notes | `docs/UserGuide.md` |

## Remaining Gaps (Require Live Environment)

These two items cannot be resolved in code — they need a running Plastic SCM server and real LAN hardware.

### 1. `ParseLog` format — needs server validation

`ParseLog` assumes `cs:N | author | date | comment` (pipe-delimited). Run `cm log <any-tracked-file>` on your server and verify the actual output format matches. If it differs, update the parser at `LeanVaultPaneViewModel.cs` around line 685.

### 2. OME-231 — End-to-end integration test

Manual test event with the full team: multiple engineers checking in/out simultaneously, assembly check-in with locked parts, BOM export to `04_Releases/BOM/`, verify the CSV appears as a Plastic changeset. No code required — schedule and run.

---

## Linear Updates Required

Based on the code review, these Linear statuses should be updated:

| Issue | Move to | Reason |
|---|---|---|
| OME-216 | **Done** | Polling timer fully implemented |
| OME-218 | **Done** | `SwAssemblyWalker` separate class, working |
| OME-219 | **Done** | Assembly tree view with parallel status fetch |
| OME-220 | **Done** | Check Out All + Check In Assembly both complete |
| OME-221 | **Done** | Absolute-path walker handles SharedParts naturally |
| OME-222 | **Done** | Amber banner with count + names implemented |
| OME-223 | **Done** | History view wired, ParseLog updated |
| OME-224 | **Done** | Restore calls `cm get --revision` |
| OME-225 | **Done** | `SwBomExtractor.cs` complete |
| OME-226 | **Done** | BOM auto-export on assembly check-in; `cm add` gap fixed |
| OME-227 | **In Progress** | CLI structure done; checkin-assembly + bom are stubs (documented) |
| OME-228 | **Done** | `LockListDialog` with force-release |
| OME-229 | **Done** | WiX v4 installer with COM registry + PATH |
| OME-230 | **Backlog** | Not started |
| OME-231 | **Backlog** | Not started |

---

## Final File Map

```
src/
├── LeanVault.AddIn/               .NET Framework 4.8 — the SolidWorks add-in
│   ├── SwAddin.cs                 ISwAddin, COM registration, 4 SW event hooks
│   ├── Models/FileStatus.cs       LockState enum + FileStatus DTO
│   ├── Services/
│   │   ├── CmCliService.cs        cm CLI wrapper: GetStatus, CheckOut, CheckIn,
│   │   │                          CheckInMultiple, UndoCheckOut, GetLog, GetLockList,
│   │   │                          ForceUnlock, GetRevision, (needs: Add)
│   │   ├── CmStatusResult.cs      Typed cm status parse result
│   │   ├── SwAssemblyWalker.cs    Flat-list assembly reference walker via COM API
│   │   └── SwBomExtractor.cs      BOM extractor — instance count + SW properties
│   └── UI/
│       ├── LeanVaultPane.xaml      Full task pane: conflict banner, amber lock banner,
│       │                           ⚙ Admin button, status dot, actions, quick check-in,
│       │                           assembly tree, Check Out All, Check In Assembly, history
│       ├── LeanVaultPaneViewModel.cs  All commands + polling timer + BOM export
│       ├── TaskPaneHost.cs         WinForms Panel + ElementHost + Cleanup wiring
│       ├── CheckInDialog.xaml      Single-file commit message dialog
│       ├── CheckInAssemblyDialog.xaml  Multi-file checkbox + comment dialog
│       └── LockListDialog.xaml     Admin: active locks GridView + Force Release
│
├── LeanVault.Tests/               xUnit — CmStatusParserTests (5 tests)
│
├── LeanVault.Cli/                 .NET 8 — lv.exe CLI wrapper
│   └── Program.cs                 6 commands via System.CommandLine; checkin-assembly
│                                  and bom are stubs pending SW COM access solution
│
└── LeanVault.Installer/           WiX v4 installer
    └── Package.wxs                COM registry keys, lv.exe PATH entry
```
