<#
.SYNOPSIS
    Captures production UI screens and automated sessions from the development player.
    Build first with tools/run-tests.ps1 -Suite Build. Human/DPI/controller acceptance is separate.
#>
param([string]$OutputDirectory = '', [switch]$Visible)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$player = Join-Path $repoRoot 'SeaOfUncertainty/Builds/Windows/Sea of Uncertainty.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'Build the Windows development player first.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'SeaOfUncertainty/TestResults/acceptance-2026-10-19/final' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$log = Join-Path $OutputDirectory 'Player.log'
$report = Join-Path $OutputDirectory 'capture-report.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$windowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$process = Start-Process -FilePath $player -WindowStyle $windowStyle -PassThru -ArgumentList @(
    '-captureAcceptance', "`"-acceptanceOutput=$OutputDirectory`"",
    '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
    '-logFile', "`"$log`"")
$null = $process.Handle
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(1000)) {
    if ($timer.Elapsed.TotalMinutes -gt 10) { $process.Kill(); throw "Capture timed out; inspect $log" }
}
if ($process.ExitCode -ne 0) { throw "Capture player exited with $($process.ExitCode); inspect $log" }
if (-not (Test-Path -LiteralPath $report)) { throw "Capture report missing; inspect $log" }
$reportLines = Get-Content -LiteralPath $report
if ($reportLines -match "SESSION FAILED|completed False") { throw "An automated session did not complete; inspect $report" }
$reportLines
Write-Host "Captures saved to $OutputDirectory"
