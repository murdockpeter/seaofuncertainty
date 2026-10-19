<#
.SYNOPSIS
Build a versioned Windows launcher package for a GitHub Release (does not publish).
.EXAMPLE
.\tools\package-release.ps1 -Version 0.2.0
.EXAMPLE
.\tools\package-release.ps1 -Version 0.2.0 -PlayerDirectory C:\Builds\Windows
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$UnityPath = '',
    [string]$PlayerDirectory = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'distribution/SeaLauncher.ps1') -LibraryOnly
$parsed = ConvertTo-ReleaseVersion $Version
$tag = 'v' + $parsed.ToString(3)
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'SeaOfUncertainty'
$releaseRoot = Join-Path $project "Builds/Releases/$tag"
if (Test-Path -LiteralPath $releaseRoot) { throw "Output already exists: $releaseRoot. Choose a new version or move the old output." }
$package = Join-Path $releaseRoot 'package'
$player = Join-Path $package "versions/$tag"
New-Item -ItemType Directory -Path $player -Force | Out-Null

if ($PlayerDirectory) {
    # Repackage a previously built player; caller must ensure its embedded version matches.
    Copy-Item -Path (Join-Path $PlayerDirectory '*') -Destination $player -Recurse
} else {
    if (-not $UnityPath) {
        $editorVersion = ((Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt')) -match '^m_EditorVersion:')[0].Split(':')[1].Trim()
        $UnityPath = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
    }
    if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found: $UnityPath" }
    $unityLock = Join-Path $project 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $unityLock) {
        try { [IO.File]::Open($unityLock, 'Open', 'ReadWrite', 'None').Dispose() }
        catch { throw 'Close the Unity Editor before building a release.' }
    }
    $oldVersion = $env:SEA_RELEASE_VERSION
    $oldOutput = $env:SEA_RELEASE_OUTPUT
    try {
        $env:SEA_RELEASE_VERSION = $parsed.ToString(3)
        $env:SEA_RELEASE_OUTPUT = $player
        $build = Start-Process -FilePath $UnityPath -PassThru -WindowStyle Hidden -ArgumentList @(
            '-batchmode', '-quit', '-projectPath', "`"$project`"",
            '-executeMethod', 'SeaOfUncertainty.Editor.GitHubReleaseBuilder.BuildWindowsRelease',
            '-logFile', "`"$(Join-Path $releaseRoot 'Build.log')`"")
        $null = $build.Handle
        $build.WaitForExit()
        if ($build.ExitCode -ne 0) { throw "Release build failed. See $releaseRoot/Build.log" }
    } finally {
        $env:SEA_RELEASE_VERSION = $oldVersion
        $env:SEA_RELEASE_OUTPUT = $oldOutput
    }
}
foreach ($required in @('Sea of Uncertainty.exe', 'UnityPlayer.dll', 'Sea of Uncertainty_Data/globalgamemanagers')) {
    if (-not (Test-Path -LiteralPath (Join-Path $player $required) -PathType Leaf)) { throw "Missing player file: $required" }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'distribution/SeaLauncher.ps1') -Destination $package
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'distribution/Start Sea of Uncertainty.cmd') -Destination $package
@{ schema = 1; version = $tag } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'release.json') -Encoding UTF8
Set-InstalledVersion $package $tag ''
@'
Extract this entire ZIP into a writable folder, then double-click Start Sea of Uncertainty.cmd.
The launcher checks for stable GitHub Releases and asks before installing. Offline play remains available.
Settings > Check for Updates can install an update while playing; restart through the launcher afterward.
Keep the versions folder: previous builds are retained for rollback. Saves and settings live outside this folder.
Rollback: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\SeaLauncher.ps1 -Rollback
'@ | Set-Content -LiteralPath (Join-Path $package 'READ ME.txt') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = Join-Path $releaseRoot $script:AssetName
$zip = [IO.Compression.ZipFile]::Open($archive, 'Create')
try {
    foreach ($file in Get-ChildItem -LiteralPath $package -File -Recurse) {
        $relative = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        if ($relative -match '(^|/)[^/]*(_BurstDebugInformation_DoNotShip|_BackUpThisFolder_ButDontShipItWithYourGame)(/|$)') { continue }
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $relative, 'Optimal')
    }
} finally { $zip.Dispose() }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $script:AssetName" | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Package ready: $archive"
Write-Host "After review, create a draft release at the tested commit and upload $script:AssetName and SHA256SUMS.txt."
Write-Host "Tag: $tag. The launcher requires GitHub's sha256 asset digest before installing."
