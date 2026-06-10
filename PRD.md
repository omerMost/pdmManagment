# PRD — LeanVault SW Integration
### A lean SolidWorks add-in built on top of Plastic SCM

> Instead of building a full PDM from scratch, we use Plastic SCM as the versioning engine and build a focused SolidWorks integration layer on top of it.

---

## 1. Vision

Plastic SCM already solves 98% of what a lean PDM needs: versioning, LAN server, exclusive locking, AD auth, a polished GUI (PlasticX), and a powerful CLI (`cm`). What it lacks is awareness of SolidWorks — it doesn't know about assembly dependencies, SW custom properties, or BOM structure.

**We build the 2%**: a SolidWorks add-in that bridges Plastic SCM and SolidWorks, making the version control feel native to CAD engineers.

---

## 2. What We Are NOT Building

- No custom server — Plastic SCM server runs on LAN
- No custom database — Plastic handles all revision storage
- No custom auth — Plastic handles AD/LDAP
- No custom GUI for history/branching — PlasticX already does this
- No custom CLI — `cm` CLI already exists and is scriptable

---

## 3. What We ARE Building

### 3.1 Plastic SCM Server Configuration (not code — setup work)
- Exclusive lock rules for `.SLDPRT`, `.SLDASM`, `.SLDDRW`
- Ignore patterns for SolidWorks temp/junk files
- Repository structure recommendation for CAD projects
- Workspace layout guidelines

### 3.2 SolidWorks Add-in (C# .NET Framework 4.8)
Core of the project. A task pane inside SolidWorks that gives engineers PDM-like behavior without leaving the CAD environment.

### 3.3 Thin CLI Wrapper — `lv.exe` (optional, C# .NET 8)
CAD-aware shortcuts on top of `cm`. Useful for agents and scripting. Wraps `cm` with SolidWorks-specific logic (e.g., "check in this assembly and all its parts").

---

## 4. Plastic SCM Configuration

### 4.1 Exclusive Lock Rules
Configure Plastic to enforce exclusive locks on binary CAD files so two engineers cannot overwrite each other's geometry:

```
*.SLDPRT  → exclusive lock
*.SLDASM  → exclusive lock
*.SLDDRW  → exclusive lock
*.SLDLFP  → exclusive lock (library features)
*.SLDFTP  → exclusive lock (form tool)
```

Text/config files (e.g., `*.txt`, `*.json`, `*.xml`) remain mergeable.

### 4.2 Ignore Patterns (`.plasticignore`)
SolidWorks creates many temp and backup files that should never be versioned.
Production exports (STEP, PDF) are generated on demand and also excluded.

```
# SolidWorks backup and temp files
*~.SLDPRT
*~.SLDASM
*~.SLDDRW
*.swpub
*.sldrec
*.sldtmp
~*.sld*
# SolidWorks auto-recover
__SWbak/
# Windows thumbnails
Thumbs.db
desktop.ini
# Production exports — generated on demand, not version controlled
03_Production/
*.step
*.STEP
*.stp
*.STP
*.pdf
*.PDF
```

### 4.3 Repository Structure
The existing folder structure is kept as-is. Plastic is added on top without restructuring.
`99_Development_WIP` and `Archive` folders remain; over time Plastic branches and history
replace the need to manually manage them, but no forced migration.

**Actual structure (from real projects):**

```
/
├── System_30cm/
│   ├── CFG_Elta_Ku/
│   │   ├── 01_Design/
│   │   │   ├── Assemblies/     ← .SLDASM
│   │   │   ├── Parts/          ← .SLDPRT
│   │   │   ├── Drawings/       ← .SLDDRW
│   │   │   └── Archive/        ← old revisions (kept, Plastic history will replace this role)
│   │   ├── 02_Documents/       ← specs, ICDs, non-CAD
│   │   ├── 03_Production/      ← IGNORED by Plastic (STEP/PDF exports, generated on demand)
│   │   ├── 04_Releases/        ← formal release snapshots
│   │   └── 99_Development_WIP/ ← WIP work (kept; Plastic branches replace this role over time)
│   ├── CFG_Most_Ka/
│   ├── CFG_Mpt_Ku/
│   ├── CFG_RadomeBox_Ku/
│   └── CFG_Russia_Ku/
├── System_40cm/
│   ├── CFG_Most_Ka/
│   └── CFG_Most_Ku/
└── SharedParts/                ← common parts referenced by multiple CFGs (e.g., MEC-GEN-*)
```

> **Both models are supported:**
> - If `MEC-GEN-*` parts are physically shared (one file referenced by multiple assemblies), they live in `SharedParts/` at the repo root and all CFG assemblies reference them there.
> - If each CFG keeps its own copies, `SharedParts/` stays empty or unused — no restructuring required.
> Plastic SCM tracks both equally. The add-in's dependency walker will correctly detect whichever model is in use.

### 4.4 Plastic SCM Edition
- **Self-hosted Plastic SCM server** on a LAN machine (not Unity DevOps cloud)
- **Gluon mode for all standard engineers** — CAD files cannot be merged; branching concepts confuse engineers and create risk. Gluon's simple check-out/check-in flow matches the mental model perfectly.
- **Full Plastic for Team Leads only** — branch management, merges, and history investigation

---

## 5. SolidWorks Add-in Specification

### 5.1 Tech Stack
- **Language**: C# .NET Framework 4.8
- **SolidWorks API**: SolidWorks 2021 (`SolidWorks.Interop.sldworks`, `SolidWorks.Interop.swconst`)
- **Plastic integration**: via `cm` CLI (`System.Diagnostics.Process` + stdout parsing)
- **UI**: SolidWorks Task Pane (`ITaskpaneView`) hosting a WPF UserControl via ElementHost (cleaner visual style than native WinForms)
- **Threading**: ALL `cm` CLI calls must run on a background thread (`Task.Run`). Never block the SolidWorks UI thread — `cm status` or `cm checkout` can take 500ms–several seconds on a LAN. UI updates marshal back via `Dispatcher.Invoke`.
- **Assembly dependency walking**: Always via the running SolidWorks COM API (`Component2.GetPathName`). Never parse `.SLDASM` binary files directly — they are compressed OLE structured storage, not readable XML.

### 5.2 Task Pane UI

```
┌─────────────────────────────────┐
│  LeanVault                   ⚙  │
├─────────────────────────────────┤
│ Active File                     │
│  PartA.SLDPRT                   │
│  Revision: cs:1045 | Clean      │
│  Locked by: (you) since 09:32   │
├─────────────────────────────────┤
│ [Check Out]  [Check In]         │
│ [Undo]       [History]          │
├─────────────────────────────────┤
│ Assembly Tree Status            │
│  ✓ TopAssembly.SLDASM  (you)    │
│  ✓ PartA.SLDPRT        (you)    │
│  ⚠ PartB.SLDPRT        (John)   │
│  ✓ PartC.SLDPRT        clean    │
├─────────────────────────────────┤
│ [Check In Assembly + All Parts] │
└─────────────────────────────────┘
```

### 5.3 Core Features

#### Check-Out
- Check out the active document with one click
- Runs `cm checkout <file>` behind the scenes
- Updates lock status display

#### Check-In
- Check in the active document
- Prompts for a commit message (required)
- Reads SW custom properties and appends them as structured metadata in the changeset comment:
  ```
  Fix flange thickness per ECO-042

  [SW Properties]
  PartNumber: FLG-0042-A
  Description: Flange bracket, 6061-T6
  Material: Aluminum 6061
  Revision: B
  ```
- Runs `cm checkin <file> --comment "..."`

#### Assembly-Aware Check-In
- When the active document is an `.SLDASM`, walk the full SolidWorks reference tree using the SW API
- Show a preview list of all referenced files and their current status (clean / modified / locked by other)
- Let the engineer select which files to include
- Check in all selected files as a single Plastic changeset

#### Status Display
- Show lock owner and timestamp for the active file
- Show assembly tree with per-file status (locked by you / locked by other / clean / modified)
- Color-coded: green = clean, yellow = checked out by you, red = locked by someone else

#### SW Event Hooks
- **On FileSave**: if the file is in the Plastic workspace and is checked out, offer a "Quick Check-In" toast notification inside the task pane
- **On FileOpen**: detect if file is in a Plastic workspace, fetch and show its status
- **On DocumentClose**: if the file is checked out by the current user and has unsaved check-in, warn before closing

#### History View
- Show the last N changesets for the active file (parsed from `cm log <file>`)
- Click a changeset to see its comment and SW properties metadata
- "Restore this version" button (runs `cm get --revision=<cs>`)

### 5.4 BOM Export
- When active document is an assembly, extract BOM using SW API (`IModelDoc2.GetBOMFeature` or table traversal)
- Export as CSV or Excel with columns: Part Number, Description, Quantity, Material, Revision, Plastic Changeset
- Save the BOM snapshot to `BOM/` folder in the repo and optionally auto-check-in it

### 5.5 Property Sync Direction
- **Check-In**: SW properties → written into Plastic changeset comment (structured block)
- **Future / optional**: parse changeset comments on checkout to verify property consistency

---

## 6. CLI Wrapper — `lv.exe`

A thin C# .NET 8 console app for agent/scripting use. Wraps `cm` with CAD-aware commands.

```
lv status                          # cm status, formatted for CAD context
lv checkout <file>                 # cm checkout
lv checkin <file> -m "message"     # cm checkin with SW property extraction
lv checkin-assembly <sldasm>       # walk SW references, checkin all modified
lv lock list                       # cm find lock
lv lock release <file>             # admin: cm unlock
lv history <file>                  # cm log, filtered for CAD metadata
lv bom <sldasm>                    # extract and print BOM as JSON
lv --json <any command>            # machine-readable output
```

> All commands support `--json` for AI agent consumption.
> `lv` requires the `cm` CLI to be on the PATH. It is a wrapper, not a replacement.

---

## 7. What Engineers See Day-to-Day

1. Open SolidWorks — task pane shows file status automatically
2. Click "Check Out" before editing
3. Work normally in SolidWorks
4. On save, toast appears: "Quick Check-In?"
5. Click Check-In, type a message, done
6. For assemblies: "Check In Assembly" bundles all modified parts in one changeset
7. History and restore available via task pane or PlasticX GUI

---

## 8. Milestones

### M1 — Plastic SCM Setup & Configuration
- Install and configure Plastic SCM server on LAN machine
- Set up exclusive lock rules for CAD file types
- Create `.plasticignore` for SW temp files
- Define repository structure
- Configure AD/LDAP auth
- Document workspace setup guide for engineers

### M2 — Add-in Scaffold + Basic Check-In/Out + Event Hooks
- SolidWorks 2021 add-in project (C# .NET Framework 4.8)
- Task pane (WPF UserControl via ElementHost) with file status (runs `cm status` async)
- Check-out button (runs `cm checkout` on background thread)
- Check-in button with message prompt (runs `cm checkin` on background thread)
- Undo checkout (runs `cm undochange`)
- **`FileOpen` event hook** — on every document open, fetch and display its Plastic status in the task pane (async, non-blocking). This is critical: the task pane must know which file is active at all times.
- **`FileSave` event hook** — offer Quick Check-In prompt in task pane after save
- `DocumentClose` warning if file is checked out with uncommitted changes

### M3 — SW Property Sync & Lock Conflict Alerts
- Read SW custom properties on check-in and embed as structured block in changeset comment
- **Lock conflict banner**: when `FileOpen` detects a file is locked by another engineer, show a prominent warning banner at the top of the task pane: _"Read-Only — Locked by John since 09:32"_
- Status icons in task pane: green (clean), yellow (checked out by you), red (locked by someone else)

### M4 — Assembly Awareness
- Walk SolidWorks reference tree for `.SLDASM` files
- Show per-file status in task pane (assembly tree view)
- "Check In Assembly" — select files, single changeset
- Warn when referenced parts are locked by other engineers

### M5 — History, BOM & Polish
- History view in task pane (last N changesets, SW properties, restore button)
- **BOM auto-export on every assembly check-in** — generated automatically as CSV/Excel, committed to `04_Releases/BOM/` in the same changeset. If it's manual, engineers will forget.
- `lv.exe` CLI wrapper with `--json` support
- Installer (WiX): registers the add-in, sets PATH for `lv.exe`

---

## 9. Tech Stack Summary

| Component | Technology |
|---|---|
| Version control engine | Plastic SCM (self-hosted LAN server) |
| Auth | Plastic SCM AD/LDAP integration |
| SW Add-in | C# .NET Framework 4.8, SolidWorks 2021 API |
| Task Pane UI | WinForms panel hosted in ITaskpaneView |
| Plastic integration | `cm` CLI via System.Diagnostics.Process |
| BOM export | ClosedXML (Excel), CsvHelper |
| CLI wrapper | C# .NET 8, System.CommandLine |
| Installer | WiX Toolset |

---

## 10. Open Questions

- [x] Plastic SCM edition → **Existing license in place. M1 is configuration only, no procurement.**
- [x] Gluon vs full Plastic for engineers → **Gluon for engineers, full Plastic for leads only**
- [x] `lv checkin-assembly` headless vs SW API → **SW API only** (binary .SLDASM files are not parseable; use `Component2.GetPathName` on the running instance)
- [x] BOM export trigger → **Automatic on every assembly check-in**
- [x] Shared parts structure → **Both models supported**: shared parts go in `SharedParts/` root; per-CFG copies stay where they are. The dependency walker handles both.
