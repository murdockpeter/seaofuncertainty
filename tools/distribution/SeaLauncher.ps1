param([switch]$NoLaunch, [switch]$Rollback, [switch]$LibraryOnly)

$ErrorActionPreference = 'Stop'
$script:Repository = 'murdockpeter/seaofuncertainty'
$script:AssetName = 'SeaOfUncertainty-windows-x64.zip'
$script:Executable = 'Sea of Uncertainty.exe'

function ConvertTo-ReleaseVersion([string]$Value) {
    if ($Value -cnotmatch '^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw "Unsupported release version '$Value'; expected vMAJOR.MINOR.PATCH."
    }
    return [version]$Value.TrimStart('v')
}

function Get-ContainedPath([string]$Root, [string]$Relative) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $path = [IO.Path]::GetFullPath([IO.Path]::Combine($base, $Relative))
    if (-not $path.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes the installation directory.' }
    return $path
}

function Read-InstalledVersion([string]$Root) {
    $pointer = Join-Path $Root 'current.json'
    if (-not (Test-Path -LiteralPath $pointer)) { throw 'current.json is missing. Extract the complete release ZIP again.' }
    $state = Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json
    $null = ConvertTo-ReleaseVersion $state.version
    $player = Get-ContainedPath $Root "versions/$($state.version)/$script:Executable"
    if (-not (Test-Path -LiteralPath $player -PathType Leaf)) { throw 'The selected game version is incomplete. Use rollback or extract the release ZIP again.' }
    return $state
}

function Set-InstalledVersion([string]$Root, [string]$Version, [string]$Previous) {
    $null = ConvertTo-ReleaseVersion $Version
    $target = Join-Path $Root 'current.json'
    $temporary = Join-Path $Root ('current.' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $json = @{ version = $Version; previous = $Previous } | ConvertTo-Json -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $stream = [IO.File]::Open($temporary, 'CreateNew', 'Write', 'None')
        try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        if (Test-Path -LiteralPath $target) { [IO.File]::Replace($temporary, $target, (Join-Path $Root 'current.json.bak')) }
        else { [IO.File]::Move($temporary, $target) }
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}

function Get-ReleaseAsset($Release) {
    if ($Release.draft -or $Release.prerelease) { throw 'Only published stable releases are supported.' }
    $null = ConvertTo-ReleaseVersion $Release.tag_name
    $assets = @($Release.assets | Where-Object { $_.name -ceq $script:AssetName -and $_.state -eq 'uploaded' })
    if ($assets.Count -ne 1) { throw "Release must contain exactly one $script:AssetName asset." }
    $asset = $assets[0]
    $expected = "https://github.com/$script:Repository/releases/download/$($Release.tag_name)/$script:AssetName"
    if ($asset.browser_download_url -cne $expected) { throw 'Unexpected release download address.' }
    if ($asset.digest -cnotmatch '^sha256:[a-fA-F0-9]{64}$') { throw 'GitHub has not supplied a SHA-256 digest for this release asset.' }
    if ([long]$asset.size -le 0 -or [long]$asset.size -gt 4GB) { throw 'Release asset size is outside supported limits.' }
    return $asset
}

function Expand-ValidatedRelease([string]$Archive, [string]$Destination, [string]$Version) {
    $null = ConvertTo-ReleaseVersion $Version
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $total = 0L
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.Contains(':') -or $entry.FullName.StartsWith('/') -or $entry.FullName.StartsWith('\')) { throw 'Unsafe ZIP path.' }
            $path = Get-ContainedPath $Destination $entry.FullName
            if ($path.Length -ge 260 -or ([IO.Path]::GetDirectoryName($path)).Length -ge 248) {
                throw 'This installation path is too long for Windows PowerShell. Extract the launcher package into a shorter writable path and retry.'
            }
            if (-not $seen.Add($path)) { throw 'Duplicate ZIP path.' }
            # Reject symlinks and reparse points; extract regular files only.
            if (($entry.ExternalAttributes -band 0x400) -ne 0 -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'ZIP links are unsupported.' }
            $total += $entry.Length
            if ($total -gt 8GB) { throw 'Release expands beyond the supported size.' }
        }
        [IO.Compression.ZipFile]::ExtractToDirectory($Archive, $Destination)
    } finally { $zip.Dispose() }
    $manifest = Get-Content -LiteralPath (Join-Path $Destination 'release.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or $manifest.version -cne $Version) { throw 'Release manifest does not match its tag.' }
    $player = Get-ContainedPath $Destination "versions/$Version/$script:Executable"
    $data = Get-ContainedPath $Destination "versions/$Version/Sea of Uncertainty_Data/globalgamemanagers"
    $unity = Get-ContainedPath $Destination "versions/$Version/UnityPlayer.dll"
    foreach ($required in @($player, $data, $unity)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw 'Release is missing required Unity player files.' }
    }
}

function Install-Release([string]$Root, [string]$Archive, [string]$Version, [string]$Digest) {
    $null = ConvertTo-ReleaseVersion $Version
    if ($Digest -cnotmatch '^sha256:[a-fA-F0-9]{64}$') { throw 'Invalid SHA-256 digest.' }
    if ((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ine $Digest.Substring(7)) { throw 'Download checksum failed. The current installation was preserved.' }
    $current = Read-InstalledVersion $Root
    if ((ConvertTo-ReleaseVersion $Version) -le (ConvertTo-ReleaseVersion $current.version)) { throw 'Updates must be newer than the installed version. Use explicit rollback for an older build.' }
    $stage = Get-ContainedPath $Root ('.stage-' + [guid]::NewGuid().ToString('N'))
    $destination = Get-ContainedPath $Root "versions/$Version"
    try {
        Expand-ValidatedRelease $Archive $stage $Version
        # Never overwrite a running, previous, or interrupted installation.
        if (Test-Path -LiteralPath $destination) { throw "Version $Version already exists. Remove the unused version directory manually before retrying." }
        [IO.Directory]::Move((Join-Path $stage "versions/$Version"), $destination)
        Set-InstalledVersion $Root $Version $current.version
    } finally {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
}

function Show-LauncherMessage([string]$Message, [switch]$Question) {
    $buttons = if ($Question) { 'YesNo' } else { 'OK' }
    return [Windows.Forms.MessageBox]::Show($Message, 'Sea of Uncertainty', $buttons)
}

function Start-SeaLauncher([string]$Root = $PSScriptRoot) {
    Add-Type -AssemblyName System.Windows.Forms
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $installRoot = [IO.Path]::GetFullPath($Root)
    $lockStream = $null
    try {
        # A file lock serializes launchers across processes without requiring administrator rights.
        $lockStream = [IO.File]::Open((Join-Path $installRoot '.update.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
        $current = Read-InstalledVersion $installRoot
        if ($Rollback) {
            $null = ConvertTo-ReleaseVersion $current.previous
            $previous = Get-ContainedPath $installRoot "versions/$($current.previous)/$script:Executable"
            if (-not (Test-Path -LiteralPath $previous)) { throw 'Previous version is unavailable.' }
            Set-InstalledVersion $installRoot $current.previous $current.version
        } else {
            $download = $null
            try {
                $headers = @{ 'User-Agent' = 'SeaOfUncertainty-Launcher'; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
                $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:Repository/releases/latest" -Headers $headers -TimeoutSec 15
                if ((ConvertTo-ReleaseVersion $release.tag_name) -gt (ConvertTo-ReleaseVersion $current.version)) {
                    $asset = Get-ReleaseAsset $release
                    if ((Show-LauncherMessage "Version $($release.tag_name) is available (installed: $($current.version)). Download and install it? Your saves are preserved. A running game can continue; the update takes effect next launch." -Question) -eq 'Yes') {
                        $download = Get-ContainedPath $installRoot ('.download-' + [guid]::NewGuid().ToString('N') + '.zip')
                        Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $download -TimeoutSec 600
                        if ((Get-Item -LiteralPath $download).Length -ne [long]$asset.size) { throw 'Downloaded size does not match the release asset.' }
                        Install-Release $installRoot $download $release.tag_name $asset.digest
                        if ($NoLaunch) { $null = Show-LauncherMessage 'Update installed. Exit the game and use Start Sea of Uncertainty.cmd to play the new version.' }
                    }
                } elseif ($NoLaunch) { $null = Show-LauncherMessage "You are up to date ($($current.version))." }
            } catch {
                # No release, offline, rate limiting and installation failures all leave offline play available.
                $null = Show-LauncherMessage "Update check/install could not complete. Your installed game is still available.`r`n`r`n$($_.Exception.Message)"
            } finally {
                if ($download -and (Test-Path -LiteralPath $download)) { Remove-Item -LiteralPath $download -Force }
            }
        }
        if (-not $NoLaunch) {
            $selected = Read-InstalledVersion $installRoot
            $player = Get-ContainedPath $installRoot "versions/$($selected.version)/$script:Executable"
            Start-Process -FilePath $player -WorkingDirectory (Split-Path -Parent $player) -WindowStyle Normal
        }
    } catch { $null = Show-LauncherMessage $_.Exception.Message }
    finally { if ($lockStream) { $lockStream.Dispose() } }
}

if (-not $LibraryOnly) { Start-SeaLauncher }
