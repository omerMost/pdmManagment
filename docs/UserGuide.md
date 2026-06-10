# LeanVault Quick Reference Guide

Welcome to LeanVault, your native SolidWorks integration for Plastic SCM! This guide will help you understand the task pane interface and common engineering workflows.

## The Task Pane

When you open SolidWorks, the LeanVault task pane will be available on the right-hand side. It automatically tracks the active document you are working on.

### Status Indicators
- 🟢 **Green (Clean)**: Your local file matches the server and is not checked out.
- 🟡 **Yellow (Checked Out By You)**: You have an exclusive lock on this file. It's safe to edit and save.
- 🔴 **Red (Locked By Other)**: Someone else is editing this file. Do not save changes, as they will conflict.

### Active File Actions
- **Check Out**: Acquires an exclusive lock on the active file so you can make changes.
- **Undo Check Out**: Discards your local changes and releases your lock.
- **Check In**: Commits your changes to the server and releases your lock. You will be prompted for a commit message.

### History
The lower section of the task pane displays the recent commit history of the active file.
- **Restore Revision**: You can right-click or select a past revision from the history view to roll back the active file to that state.

## Assembly Workflows

When you open a SolidWorks Assembly (`.SLDASM`), the **Assembly Tree Status** section appears.

- **Check Out All**: Checks out every part in the assembly that is currently "Clean". This is useful when you plan to make widespread changes.
- **Amber Warning Banner**: If you open an assembly and see a yellow banner at the top of the tree, it means one or more parts in the assembly are currently locked by other engineers. You cannot check in those specific parts.
- **Check In Assembly + All Parts**: This button opens a dialog showing all modified or checked-out parts in the assembly. You can select which parts to include in a single, unified commit.

### Automated BOM Export
Whenever you successfully check in an assembly using the "Check In Assembly + All Parts" button, LeanVault automatically extracts the Bill of Materials (including Part Number, Description, Quantity, Material, and Revision) and saves it as a CSV file in `04_Releases/BOM/` within your workspace. This file is automatically committed to the server along with your assembly.

## Administrative Tools

If you are a PDM administrator, you can click the **⚙ Admin** button in the Active File header.
- **Lock List**: This dialog displays all currently locked files across the entire workspace.
- **Force Release**: Use this button next to a locked file to forcibly break another user's lock (e.g., if they are on vacation and forgot to check a part back in). Use this with caution!

## CLI Operations
Advanced users and automation scripts can use the `lv.exe` command-line tool. It supports all common operations (e.g., `lv checkout <file>`, `lv status`) and outputs machine-readable JSON using the `--json` flag. 

*(Note: The `lv checkin-assembly` and `lv bom` CLI commands are currently stubs and will be fully supported in a future update).*
