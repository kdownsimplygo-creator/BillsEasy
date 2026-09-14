# Development Environment Specification & Status

**Project:** BillsEasy (Indian Accounting Desktop Application)  
**Phase:** Phase 0 — Environment Preparation & Verification  
**Last Verified:** September 2026  

---

## 1. Environment Summary

| Component | Target / Required Specification | Current Machine Installed Version | Status | Notes / Location |
| :--- | :--- | :--- | :--- | :--- |
| **Operating System** | Windows 10/11 (64-bit) | Windows 11 Home Single Language (Build 26200, 64-bit) | ? Ready | Native 64-bit environment |
| **.NET SDK** | .NET 8.0 SDK (LTS) or higher | .NET SDK 8.0.425 (LTS) | ? Installed & Verified | Located at C:\Users\Kuldipsinh\dotnet |
| **.NET Runtime** | .NET Core Runtime 8.0.x | Microsoft.NETCore.App 8.0.31 | ? Installed & Verified | Core runtime active |
| **WPF / Desktop Runtime** | Windows Desktop Runtime 8.0.x | Microsoft.WindowsDesktop.App 8.0.31 | ? Installed & Verified | WPF compilation & execution ready |
| **ASP.NET Core Runtime** | ASP.NET Core Runtime 8.0.x | Microsoft.AspNetCore.App 8.0.31 | ? Installed & Verified | Present in shared runtime folder |
| **Visual Studio** | Visual Studio 2022/2026 or Build Tools | Visual Studio Community 2026 (v18.6.11819.183) | ? Detected | C:\Program Files\Microsoft Visual Studio\18\Community |
| **MSBuild** | MSBuild 17.x+ | MSBuild 17.11.48 (.NET SDK & VS 2026) | ? Ready | Used by dotnet build & VS |
| **Version Control (Git)** | Git 2.30+ | Git version 2.52.0.windows.1 | ? Configured | C:\Program Files\Git\cmd\git.exe |
| **Database Tooling (SQLite)** | SQLite 3 / Microsoft.Data.Sqlite 8.x | Microsoft.Data.Sqlite 8.0.8 / SQLitePCLRaw | ? Verified via Tests | In-memory & file-based CRUD validated |
| **Testing Framework** | xUnit / VSTest | xUnit + Microsoft Test Execution CLI v17.11.1 | ? Verified | Automated tests pass via dotnet test |
| **Shell Environment** | Windows PowerShell 5.1+ / PowerShell 7 | Windows PowerShell 5.1 (Build 10.0.26100.9278) | ? Ready | PATH and DOTNET_ROOT configured in user environment |

---

## 2. Environment Variables Configured

The following environment variables have been configured in the User environment (HKCU\Environment):

1. **DOTNET_ROOT**: C:\Users\Kuldipsinh\dotnet
2. **PATH**: Includes C:\Users\Kuldipsinh\dotnet;C:\Program Files\Git\cmd;...

---

## 3. Installation & Verification History

### .NET SDK & Runtimes
- **Action Taken:** Downloaded and installed official Microsoft .NET SDK 8.0 (v8.0.425) and corresponding runtimes (Microsoft.NETCore.App 8.0.31, Microsoft.WindowsDesktop.App 8.0.31) using Microsoft's official dotnet-install utility.
- **Verification:** Executed dotnet --info and confirmed runtime/SDK detection.

### WPF Build Verification
- **Action Taken:** Generated a standalone WPF project (WpfValidationApp) targeting 
et8.0-windows.
- **Verification:** Ran dotnet build — succeeded with 0 warnings, 0 errors. Cleaned up temp artifacts.

### SQLite & Test Framework Verification
- **Action Taken:** Generated an xUnit test suite (SqliteValidationTests) referencing Microsoft.Data.Sqlite (8.0.8).
- **Verification:** Executed unit tests verifying database creation, table DDL, row insertion, and queries. Ran dotnet test — 2/2 tests passed (Duration: 265 ms). Cleaned up temp artifacts.

### Git Repository Initialization
- **Action Taken:** Initialized Git repository on main branch with .gitignore for .NET, WPF, Visual Studio, and SQLite databases.
- **Verification:** Verified git status and user configuration (kdownsimplygo / kdownsimplygo@gmail.com).

---

## 4. Manual Actions & Optional Enhancements

- **Visual Studio IDE Workloads (Optional):** If opening the solution inside Visual Studio Community GUI, ensure the ".NET Desktop Development" workload is selected in Visual Studio Installer for integrated visual XAML designer support.
- **CLI / Autonomous Builds:** Fully configured and operational out of the box with dotnet build, dotnet test, and dotnet run.
