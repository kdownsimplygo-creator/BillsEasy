# BillsEasy — Interactive E2E Launch & Database Mode Guide

This guide explains how to reliably launch the BillsEasy WPF Release desktop application against the **interactive E2E database** for manual accounting and UI validation, versus launching in **normal user mode**.

---

## 1. Overview of Database Modes

| Mode | Target Database Path | Launch Command | Purpose |
| :--- | :--- | :--- | :--- |
| **Interactive E2E Validation** | `W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db` | `run-e2e.bat`<br>`launch-interactive-e2e.bat`<br>`run.bat --e2e`<br>`powershell -File .\run-e2e.ps1` | Manual verification of seeded company (*Shree Krishna Electronics*), masters, purchase register, and financial balances. Protected from automated test resets. |
| **Normal User Mode** | `%LOCALAPPDATA%\BillsEasy\billseasy.db`<br>(e.g. `C:\Users\<User>\AppData\Local\BillsEasy\billseasy.db`) | `run.bat`<br>`run.bat --user`<br>Direct execution of `BillsEasy.Presentation.Wpf.exe` | Standard single-user workstation execution. Also protected from automated test resets. |
| **Automated Simulation Run** | `W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_Simulation_Run.db` | Automated test suites (`RealDbBusinessSimulationTests`, etc.) | Isolated test runs executed by `dotnet test`. |

---

## 2. Launching in Interactive E2E Mode

To launch the Release application connected to the interactive E2E database:

### Command Prompt (`cmd.exe`) or File Explorer
Double-click or run:
```cmd
run-e2e.bat
```
*(Alternative equivalent: `launch-interactive-e2e.bat` or `run.bat --e2e`)*

### PowerShell
```powershell
.\run-e2e.bat
```
or
```powershell
powershell -ExecutionPolicy Bypass -File .\run-e2e.ps1
```

### Precondition & Verification Check (Dry-Run)
To verify that all paths and files are in place without launching a GUI process:
```cmd
run-e2e.bat --verify-only
```

### Output Example
```text
==============================================================================
BillsEasy - Interactive E2E Launcher (Release)
==============================================================================
Intended Database:   W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db
Target Executable:   W:\BillsEasy\src\BillsEasy.Presentation.Wpf\bin\Release\net8.0-windows\BillsEasy.Presentation.Wpf.exe
==============================================================================
Configuration Status: READY
Process Environment:  BILLSEASY_DB_PATH=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db
Environment Scope:    Process-local only (not persisted to User or Machine)
==============================================================================
Launching BillsEasy.Presentation.Wpf.exe...
Application process initiated.
```

---

## 3. Launching in Normal User Mode

To launch the Release application in normal user mode (using `%LOCALAPPDATA%\BillsEasy\billseasy.db`):

```cmd
run.bat
```
*(or `run.bat --user`)*

`run.bat` explicitly unsets `BILLSEASY_DB_PATH` in its local environment, guaranteeing that even if a developer terminal previously set that variable, the application falls back cleanly to the normal user database.

---

## 4. Verification & Safety Guarantees

1. **Process-Local Environment Scoping:**
   `run-e2e.bat` uses `setlocal` / `endlocal` (and `run-e2e.ps1` uses `ProcessStartInfo.EnvironmentVariables`). The `BILLSEASY_DB_PATH` override is passed strictly to the child process and is **never** written to Windows User or Machine registry scopes (`HKCU\Environment` or `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment`).
2. **Missing Prerequisite Protection:**
   The script verifies both the Release executable and the E2E database file before execution. If either is missing, it aborts with error code `1` and a descriptive message.
3. **No Database Modification:**
   Neither script performs database creation, seeding, deletion, migration, or copy operations.
4. **Automated Test Guarding:**
   Automated simulation tests target `BillsEasy_RealDB_Simulation_Run.db` and are blocked from touching either `BillsEasy_RealDB_E2E.db` or `%LOCALAPPDATA%\BillsEasy\billseasy.db` by `InteractiveDatabaseGuard`.
