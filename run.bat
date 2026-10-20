@echo off
setlocal

rem ==============================================================================
rem BillsEasy - General Application Launcher (Release)
rem
rem Default: Launches the Release WPF application in Normal User Mode
rem          using %LOCALAPPDATA%\BillsEasy\billseasy.db.
rem
rem Usage:
rem   run.bat                 Launches in Normal User Mode (default app storage)
rem   run.bat --e2e           Launches in Interactive E2E Mode (BillsEasy_RealDB_E2E.db)
rem   run.bat --user          Explicitly launches in Normal User Mode
rem   run.bat --verify-only   Validates environment and paths without launching
rem ==============================================================================

if /i "%~1"=="--e2e" goto :DoE2E
if /i "%~1"=="e2e" goto :DoE2E
if /i "%~1"=="interactive" goto :DoE2E
goto :DoUser

:DoE2E
"%~dp0run-e2e.bat" %2 %3 %4 %5
exit /b %ERRORLEVEL%

:DoUser
set "SCRIPT_DIR=%~dp0"
set "RELEASE_EXE=%SCRIPT_DIR%src\BillsEasy.Presentation.Wpf\bin\Release\net8.0-windows\BillsEasy.Presentation.Wpf.exe"
set "USER_DB=%LOCALAPPDATA%\BillsEasy\billseasy.db"

echo ==============================================================================
echo BillsEasy - Normal User Application Launcher (Release)
echo ==============================================================================
echo Mode:                Normal User Mode (Default AppData storage)
echo Intended Database:   %USER_DB%
echo Target Executable:   %RELEASE_EXE%
echo Note:                To launch against the interactive E2E database, run:
echo                      run-e2e.bat  (or: run.bat --e2e)
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

rem 2. Resolve .NET environment without hardcoding user-specific paths
if defined DOTNET_ROOT (
    set "PATH=%DOTNET_ROOT%;%PATH%"
) else if exist "%USERPROFILE%\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\dotnet"
    set "PATH=%USERPROFILE%\dotnet;%PATH%"
) else if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%ProgramFiles%\dotnet"
    set "PATH=%ProgramFiles%\dotnet;%PATH%"
)

rem 3. Ensure BILLSEASY_DB_PATH is cleared so App.xaml.cs uses the default AppData path
set "BILLSEASY_DB_PATH="

echo Configuration Status: READY
echo Process Environment:  BILLSEASY_DB_PATH is UNSET (falls back to %USER_DB%)
echo ==============================================================================

if /i "%~1"=="--verify-only" (
    echo Verification mode: Preconditions verified successfully. Exiting without launch.
    exit /b 0
)

if /i "%~1"=="/verify" (
    echo Verification mode: Preconditions verified successfully. Exiting without launch.
    exit /b 0
)

if /i "%~2"=="--verify-only" (
    echo Verification mode: Preconditions verified successfully. Exiting without launch.
    exit /b 0
)

echo Launching BillsEasy.Presentation.Wpf.exe...
start "" "%RELEASE_EXE%"
echo Application process initiated.
echo.

endlocal