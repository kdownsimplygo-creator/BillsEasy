@echo off
setlocal

rem ==============================================================================
rem BillsEasy - Interactive E2E Release Application Launcher
rem Launches the Release WPF application targeted strictly to the protected
rem interactive E2E database: W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db
rem
rem Options:
rem   run-e2e.bat                Launches the WPF Release application against E2E DB
rem   run-e2e.bat --verify-only  Validates environment and paths without launching
rem ==============================================================================

set "SCRIPT_DIR=%~dp0"
set "RELEASE_EXE=%SCRIPT_DIR%src\BillsEasy.Presentation.Wpf\bin\Release\net8.0-windows\BillsEasy.Presentation.Wpf.exe"
set "INTERACTIVE_E2E_DB=%SCRIPT_DIR%TestData\E2E\BillsEasy_RealDB_E2E.db"

echo ==============================================================================
echo BillsEasy - Interactive E2E Launcher (Release)
echo ==============================================================================
echo Intended Database:   %INTERACTIVE_E2E_DB%
echo Target Executable:   %RELEASE_EXE%
echo ==============================================================================

rem 1. Validate that the Release executable exists
if not exist "%RELEASE_EXE%" (
    echo [ERROR] Expected Release executable not found:
    echo         "%RELEASE_EXE%"
    echo.
    echo Please build the solution in Release configuration:
    echo   dotnet build -c Release
    echo.
    exit /b 1
)

rem 2. Validate that the interactive E2E database file exists
if not exist "%INTERACTIVE_E2E_DB%" (
    echo [ERROR] Interactive E2E database file not found:
    echo         "%INTERACTIVE_E2E_DB%"
    echo.
    echo The interactive database must exist prior to launch.
    echo Automated tests must never overwrite or delete this file.
    echo.
    exit /b 1
)

rem 3. Resolve .NET environment without hardcoding user-specific paths
if defined DOTNET_ROOT (
    set "PATH=%DOTNET_ROOT%;%PATH%"
) else if exist "%USERPROFILE%\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\dotnet"
    set "PATH=%USERPROFILE%\dotnet;%PATH%"
) else if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%ProgramFiles%\dotnet"
    set "PATH=%ProgramFiles%\dotnet;%PATH%"
)

rem 4. Set process-local environment variable for the child process only
rem (setlocal ensures this is never persisted to User/Machine scope or caller shell)
set "BILLSEASY_DB_PATH=%INTERACTIVE_E2E_DB%"

echo Configuration Status: READY
echo Process Environment:  BILLSEASY_DB_PATH=%BILLSEASY_DB_PATH%
echo Environment Scope:    Process-local only (not persisted to User or Machine)
echo ==============================================================================

if /i "%~1"=="--verify-only" (
    echo Verification mode: Preconditions verified successfully. Exiting without launch.
    exit /b 0
)

if /i "%~1"=="/verify" (
    echo Verification mode: Preconditions verified successfully. Exiting without launch.
    exit /b 0
)

echo Launching BillsEasy.Presentation.Wpf.exe...
start "" "%RELEASE_EXE%"
echo Application process initiated.
echo.

endlocal