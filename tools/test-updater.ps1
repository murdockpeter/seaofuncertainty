$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'distribution/SeaLauncher.ps1') -LibraryOnly
Add-Type -AssemblyName System.IO.Compression.FileSystem
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('sea-updater-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$script:checks = 0
function Assert($Condition, [string]$Label) {
    if (-not $Condition) { throw "Test failed: $Label" }
    $script:checks++
}
function Reject([scriptblock]$Action, [string]$Label) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Assert $rejected $Label
}
function Write-Player([string]$Root, [string]$Version) {
    $path = Join-Path $Root "versions/$Version"
    New-Item -ItemType Directory -Path (Join-Path $path 'Sea of Uncertainty_Data') -Force | Out-Null
    foreach ($name in @('Sea of Uncertainty.exe', 'UnityPlayer.dll', 'Sea of Uncertainty_Data/globalgamemanagers')) {
        [IO.File]::WriteAllText((Join-Path $path $name), "fixture $Version")
    }
}
function New-Archive([string]$Name, [string]$ManifestVersion = 'v1.1.0', [string]$ExtraPath = '') {
    $source = Join-Path $testRoot "$Name-source"
    Write-Player $source 'v1.1.0'
    @{ schema = 1; version = $ManifestVersion } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $source 'release.json')
    $archive = Join-Path $testRoot "$Name.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($source, $archive)
    if ($ExtraPath) {
        $zip = [IO.Compression.ZipFile]::Open($archive, 'Update')
        try { $null = $zip.CreateEntry($ExtraPath) } finally { $zip.Dispose() }
    }
    return $archive
}
try {
    Assert ((ConvertTo-ReleaseVersion 'v1.10.0') -gt (ConvertTo-ReleaseVersion 'v1.9.0')) 'numeric version order'
    foreach ($bad in @('v1.0.0-beta', '../1.0.0', 'v01.0.0', 'v1.0', 'v1.0.0.0')) { Reject { ConvertTo-ReleaseVersion $bad } "reject version $bad" }
    Reject { Get-ContainedPath $testRoot '../escape' } 'reject parent traversal'
    Reject { Get-ContainedPath $testRoot 'C:\escape' } 'reject absolute path'

    $install = Join-Path $testRoot 'install with spaces'
    Write-Player $install 'v1.0.0'
    Set-InstalledVersion $install 'v1.0.0' ''
    $before = Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw
    $archive = New-Archive 'valid'
    $digest = 'sha256:' + (Get-FileHash -LiteralPath $archive).Hash
    Reject { Install-Release $install $archive 'v1.1.0' ('sha256:' + ('0' * 64)) } 'reject checksum mismatch'
    Assert ((Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw) -ceq $before) 'failed hash preserves current pointer'
    $lockedInstall = Join-Path $testRoot 'locked pointer'
    Write-Player $lockedInstall 'v1.0.0'
    Set-InstalledVersion $lockedInstall 'v1.0.0' ''
    $pointerLock = [IO.File]::Open((Join-Path $lockedInstall 'current.json'), 'Open', 'Read', 'Read')
    try { Reject { Install-Release $lockedInstall $archive 'v1.1.0' $digest } 'failed atomic replacement rejects update' }
    finally { $pointerLock.Dispose() }
    Assert ((Read-InstalledVersion $lockedInstall).version -eq 'v1.0.0') 'replacement failure preserves playable old selection'
    foreach ($badPath in @('../escape.txt', 'C:/escape.txt', '/absolute.txt', 'versions/v1.1.0/Sea of Uncertainty.exe', (('a' * 200) + '.txt'))) {
        $badArchive = New-Archive ('bad' + [guid]::NewGuid().ToString('N')) 'v1.1.0' $badPath
        $badDigest = 'sha256:' + (Get-FileHash -LiteralPath $badArchive).Hash
        Reject { Install-Release $install $badArchive 'v1.1.0' $badDigest } "reject unsafe/duplicate archive $badPath"
        Assert ((Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw) -ceq $before) 'rejected ZIP preserves current pointer'
    }
    $mismatch = New-Archive 'mismatch' 'v9.0.0'
    Reject { Install-Release $install $mismatch 'v1.1.0' ('sha256:' + (Get-FileHash -LiteralPath $mismatch).Hash) } 'manifest version must match tag'
    $incomplete = New-Archive 'incomplete'
    $zip = [IO.Compression.ZipFile]::Open($incomplete, 'Update')
    try { ($zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'versions/v1.1.0/UnityPlayer.dll' }).Delete() } finally { $zip.Dispose() }
    Reject { Install-Release $install $incomplete 'v1.1.0' ('sha256:' + (Get-FileHash -LiteralPath $incomplete).Hash) } 'reject missing Unity player'
    Install-Release $install $archive 'v1.1.0' $digest
    $current = Read-InstalledVersion $install
    Assert ($current.version -eq 'v1.1.0' -and $current.previous -eq 'v1.0.0') 'successful install switches pointer and retains rollback'
    Reject { Install-Release $install $archive 'v0.9.0' $digest } 'ordinary install cannot downgrade'
    Assert (Test-Path -LiteralPath (Join-Path $install 'versions/v1.0.0/Sea of Uncertainty.exe')) 'previous player preserved'
    Reject { Install-Release $install $archive 'v1.1.0' $digest } 'do not overwrite installed version'
    Set-InstalledVersion $install $current.previous $current.version
    Assert ((Read-InstalledVersion $install).version -eq 'v1.0.0') 'rollback selects previous player'
    Assert (@(Get-ChildItem -LiteralPath $install -Filter '.stage-*').Count -eq 0) 'failed and successful installs clean staging'
    $firstLock = [IO.File]::Open((Join-Path $install '.update.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    try { Reject { [IO.File]::Open((Join-Path $install '.update.lock'), 'OpenOrCreate', 'ReadWrite', 'None').Dispose() } 'concurrent updater is locked out' }
    finally { $firstLock.Dispose() }

    $release = [pscustomobject]@{ tag_name = 'v2.0.0'; draft = $false; prerelease = $false; assets = @(
        [pscustomobject]@{ name = $script:AssetName; state = 'uploaded'; digest = $digest; size = 100; browser_download_url = "https://github.com/$script:Repository/releases/download/v2.0.0/$script:AssetName" }
    ) }
    Assert ((Get-ReleaseAsset $release).name -eq $script:AssetName) 'select exact Windows release asset'
    $release.prerelease = $true
    Reject { Get-ReleaseAsset $release } 'reject prerelease'
    $release.prerelease = $false
    $release.draft = $true
    Reject { Get-ReleaseAsset $release } 'reject draft'
    $release.draft = $false
    $release.assets[0].browser_download_url = 'https://example.com/game.zip'
    Reject { Get-ReleaseAsset $release } 'reject unexpected download host'
    $release.assets[0].browser_download_url = "https://github.com/$script:Repository/releases/download/v2.0.0/$script:AssetName"
    $release.assets[0].digest = $null
    Reject { Get-ReleaseAsset $release } 'reject missing GitHub digest'
    $release.assets = @()
    Reject { Get-ReleaseAsset $release } 'reject missing asset'

    # Exercise the production orchestration with network, dialogs and process launch replaced.
    # No fixture executable is ever started and no network request is made.
    $script:launched = $null
    $script:messages = @()
    $script:offline = $true
    function Invoke-RestMethod { if ($script:offline) { throw 'offline fixture' }; return $script:availableRelease }
    function Show-LauncherMessage([string]$Message, [switch]$Question) { $script:messages += $Message; return 'No' }
    function Start-Process { param($FilePath, $WorkingDirectory, $WindowStyle); $script:launched = $FilePath }
    Start-SeaLauncher $install
    Assert ($script:launched -eq (Join-Path $install 'versions/v1.0.0/Sea of Uncertainty.exe')) 'offline launcher still starts installed game'
    Assert (($script:messages -join ' ').Contains('offline fixture')) 'offline failure is explained'
    $script:offline = $false
    $script:availableRelease = [pscustomobject]@{ tag_name = 'v2.0.0'; draft = $false; prerelease = $false; assets = @(
        [pscustomobject]@{ name = $script:AssetName; state = 'uploaded'; digest = $digest; size = 100; browser_download_url = "https://github.com/$script:Repository/releases/download/v2.0.0/$script:AssetName" }
    ) }
    $script:launched = $null
    Start-SeaLauncher $install
    Assert ($script:launched -eq (Join-Path $install 'versions/v1.0.0/Sea of Uncertainty.exe')) 'declining update starts installed game'
    Assert ((Read-InstalledVersion $install).version -eq 'v1.0.0') 'declining update preserves selection'
    $NoLaunch = $true
    $script:launched = $null
    Start-SeaLauncher $install
    Assert ($null -eq $script:launched) 'in-game check never launches a second player'
    $NoLaunch = $false
    $Rollback = $true
    Start-SeaLauncher $install
    Assert ($script:launched -eq (Join-Path $install 'versions/v1.1.0/Sea of Uncertainty.exe')) 'explicit rollback starts retained player'
    Write-Host "Updater: all $script:checks checks passed."
} finally {
    # Cleanup only the verified, uniquely named test directory under the system temp directory.
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ((Split-Path -Parent $resolved) -ne $expectedParent -or (Split-Path -Leaf $resolved) -notlike 'sea-updater-tests-*') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
