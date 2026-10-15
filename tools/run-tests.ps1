<#
.SYNOPSIS
    Runs the Sea of Uncertainty editor test suites headlessly and reports pass/fail per suite.

.EXAMPLE
    .\tools\run-tests.ps1                      # every suite except build validation
    .\tools\run-tests.ps1 -Suite Unit,Integration
    .\tools\run-tests.ps1 -Suite Build         # slow; produces a Windows player build
    .\tools\run-tests.ps1 -List

    Close the Unity Editor first: batch mode cannot open a project that is already open.
    Logs are written to SeaOfUncertainty/TestResults/<suite>.log.
#>
param(
    [string[]]$Suite = @('Regression', 'SharedArchitecture', 'AiEvaluation', 'ContentSimulation', 'AdvancedContentSimulation'),
    [string]$UnityPath = '',
    [switch]$List
)

$ErrorActionPreference = 'Stop'

$suites = [ordered]@{
    # Regression composes Unit, Integration (including Multiplayer), Presentation and every feature suite.
    Regression                = 'SeaOfUncertainty.EditorTools.EnduranceAndVictoryTests.RunAllRegressionTests'
    Unit                      = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunUnitTestSuite'
    Integration               = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunIntegrationTestSuite'
    Presentation              = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunPresentationTestSuite'
    Multiplayer               = 'SeaOfUncertainty.Editor.MultiplayerFoundationTests.Run'
    SharedArchitecture        = 'SeaOfUncertainty.Editor.SharedArchitectureTests.Run'
    AiEvaluation              = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunAiEvaluationTests'
    ContentSimulation         = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunContentSimulationFoundationTests'
    AdvancedContentSimulation = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunAdvancedContentSimulationTests'
    Build                     = 'SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunBuildValidationSuite'
}

if ($List) { $suites.GetEnumerator() | ForEach-Object { '{0,-26} {1}' -f $_.Key, $_.Value }; return }

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'SeaOfUncertainty'
$resultsPath = Join-Path $projectPath 'TestResults'
New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null

if (-not $UnityPath) {
    $version = ((Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt')) -match '^m_EditorVersion:')[0].Split(':')[1].Trim()
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityPath)) { throw "Unity editor not found at '$UnityPath'. Pass -UnityPath." }
if (Test-Path (Join-Path $projectPath 'Temp/UnityLockfile')) {
    $lock = Join-Path $projectPath 'Temp/UnityLockfile'
    try { [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close() } catch { throw 'The project is open in the Unity Editor. Close it and run again.' }
}

$failed = @()
foreach ($name in $Suite) {
    if (-not $suites.Contains($name)) { throw "Unknown suite '$name'. Use -List to see suites." }
    $log = Join-Path $resultsPath "$name.log"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Write-Host ("{0,-26} running..." -f $name)
    # Wait on Unity itself, not -Wait: that also waits for helper processes Unity leaves running.
    $process = Start-Process -FilePath $UnityPath -PassThru -NoNewWindow -ArgumentList @(
        '-batchmode', '-quit',
        '-projectPath', "`"$projectPath`"",
        '-executeMethod', $suites[$name],
        '-logFile', "`"$log`"")
    $null = $process.Handle  # cache the handle so ExitCode is readable after exit
    $process.WaitForExit()
    $seconds = [int]$timer.Elapsed.TotalSeconds
    if ($process.ExitCode -eq 0) {
        Write-Host ('{0,-26} PASS  ({1}s)' -f $name, $seconds) -ForegroundColor Green
    }
    else {
        $failed += $name
        Write-Host ('{0,-26} FAIL  ({1}s, exit {2})' -f $name, $seconds, $process.ExitCode) -ForegroundColor Red
        Select-String -Path $log -Pattern 'Test failed:|check\(s\) failed|Exception:|error CS\d+' |
            Select-Object -First 12 | ForEach-Object { '    ' + $_.Line.Trim() }
        Write-Host "    log: $log"
    }
}

if ($failed.Count -gt 0) { Write-Host "`n$($failed.Count) suite(s) failed: $($failed -join ', ')" -ForegroundColor Red; exit 1 }
Write-Host "`nAll $($Suite.Count) suite(s) passed." -ForegroundColor Green
