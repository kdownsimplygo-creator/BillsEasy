<#
.SYNOPSIS
    Launches the BillsEasy WPF Release application targeted strictly to the interactive E2E database.
.DESCRIPTION
    Sets BILLSEASY_DB_PATH for the child process only without modifying User or Machine environment scopes.
.PARAMETER VerifyOnly
    Validates preconditions and prints configuration without launching the application.
#>
[CmdletBinding()]
param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = (Get-Location).Path
}

$releaseExe = Join-Path $scriptDir "src\BillsEasy.Presentation.Wpf\bin\Release\net8.0-windows\BillsEasy.Presentation.Wpf.exe"
$interactiveDb = Join-Path $scriptDir "TestData\E2E\BillsEasy_RealDB_E2E.db"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "BillsEasy - Interactive E2E Launcher (Release - PowerShell)" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "Intended Database:   $interactiveDb"
Write-Host "Target Executable:   $releaseExe"
Write-Host "=============================================================================="

if (-not (Test-Path -Path $releaseExe -PathType Leaf)) {
    Write-Error "[ERROR] Expected Release executable not found at: $releaseExe`nPlease build with: dotnet build -c Release"
    exit 1
}

if (-not (Test-Path -Path $interactiveDb -PathType Leaf)) {
    Write-Error "[ERROR] Interactive E2E database file not found at: $interactiveDb"
    exit 1
}

Write-Host "Configuration Status: READY" -ForegroundColor Green
Write-Host "Process Environment:  BILLSEASY_DB_PATH=$interactiveDb"
Write-Host "Environment Scope:    Process-local (not persisted to User or Machine)"
Write-Host "=============================================================================="

if ($VerifyOnly) {
    Write-Host "Verification mode: Preconditions verified successfully. Exiting without launch." -ForegroundColor Yellow
    exit 0
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $releaseExe
$psi.EnvironmentVariables["BILLSEASY_DB_PATH"] = $interactiveDb
$psi.UseShellExecute = $false

Write-Host "Launching BillsEasy.Presentation.Wpf.exe..."
[System.Diagnostics.Process]::Start($psi) | Out-Null
Write-Host "Application process initiated." -ForegroundColor Green
