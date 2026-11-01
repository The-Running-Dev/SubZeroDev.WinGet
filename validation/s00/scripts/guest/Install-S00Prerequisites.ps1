#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Places the pinned private worker host and module at fixed machine paths in the S00 guest. No network access.
.DESCRIPTION
    PREPARED, NOT EXECUTED (guest blocked; see 01-environment-inventory.md).

    - Verifies the staged copy against prerequisites.lock.json first (Test-S00StagedAssets.ps1 semantics, inline
      because the guest has no pwsh 7 yet).
    - Expands PowerShell 7.6.6 ZIP to %ProgramFiles%\SubZeroDev\FileManager\Service\pwsh. Not added to PATH,
      no MSI, no App Paths, no Start menu entry, no Microsoft Update opt-in.
    - Expands Microsoft.WinGet.Client 1.29.380 nupkg payload to
      %ProgramFiles%\SubZeroDev\FileManager\Service\Modules\Microsoft.WinGet.Client\1.29.380 (nupkg metadata files
      [Content_Types].xml, _rels, package, *.nuspec are dropped). Not added to PSModulePath machine-wide.
    - Never runs Repair-WinGetPackageManager, never installs App Installer, never installs from PSGallery.
    - The .NET SDK and runtime pack stay on the host; they build the S01 host, which is not built in S00.
    - Restricts Service\ to SYSTEM + Administrators full, Users read/execute (binaries only).
    - Re-run: a run that stopped after this step leaves the install in place, and D-9 keeps no earlier checkpoint.
      An existing install is accepted only if every file matches the pinned archives by SHA256 with no extra
      files; it is then left as is (AlreadyInstalled = True). Anything else stops the run.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $StagingRoot,
    [Parameter(Mandatory)] [string] $LockPath
)
$ErrorActionPreference = 'Stop'

$lock = Get-Content $LockPath -Raw | ConvertFrom-Json
function Get-Asset([string] $Id) {
    $a = $lock.assets | Where-Object id -eq $Id
    $file = Join-Path $StagingRoot $a.file
    $h = (Get-FileHash -LiteralPath $file -Algorithm SHA512).Hash
    if ($h -ne $a.sha512 -or (Get-Item -LiteralPath $file).Length -ne $a.bytes) { throw "Asset $Id failed verification: $file" }
    $file
}

$serviceRoot = Join-Path $env:ProgramFiles 'SubZeroDev\FileManager\Service'
$pwshDir = Join-Path $serviceRoot 'pwsh'
$moduleDir = Join-Path $serviceRoot 'Modules\Microsoft.WinGet.Client\1.29.380'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$nupkgMetadata = { param($n) $n -eq '[Content_Types].xml' -or $n -like '_rels/*' -or $n -like 'package/*' -or $n -like '*.nuspec' }

# Differences between an archive's file entries and an expanded folder: SHA256 per entry, plus extra files.
function Compare-ArchiveToFolder([string] $Archive, [string] $Folder, [bool] $Unescape, [scriptblock] $Skip) {
    $expected = @{}
    $sha = [Security.Cryptography.SHA256]::Create()
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach ($e in $zip.Entries) {
            $n = $e.FullName
            if ($n.EndsWith('/') -or (& $Skip $n)) { continue }
            $rel = $(if ($Unescape) { [Uri]::UnescapeDataString($n) } else { $n }) -replace '/', '\'
            $s = $e.Open()
            try { $expected[$rel] = [BitConverter]::ToString($sha.ComputeHash($s)) -replace '-' } finally { $s.Dispose() }
        }
    } finally { $zip.Dispose() }
    foreach ($rel in $expected.Keys) {
        $f = Join-Path $Folder $rel
        if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { "missing $f" }
        elseif ((Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash -ne $expected[$rel]) { "changed $f" }
    }
    Get-ChildItem -LiteralPath $Folder -Recurse -File | Where-Object { -not $expected.ContainsKey($_.FullName.Substring($Folder.Length + 1)) } | ForEach-Object { "extra $($_.FullName)" }
}

$pwshZip = Get-Asset 'powershell'
$moduleNupkg = Get-Asset 'winget-client-module'
$existing = @($pwshDir, $moduleDir | Where-Object { Test-Path -LiteralPath $_ })
$alreadyInstalled = $existing.Count -eq 2
if ($existing.Count -eq 1) { throw "Partial install: only $($existing[0]) exists. Remove it, then re-run." }
if ($alreadyInstalled) {
    $diff = @(Compare-ArchiveToFolder $pwshZip $pwshDir $false { $false }) + @(Compare-ArchiveToFolder $moduleNupkg $moduleDir $true $nupkgMetadata)
    if ($diff.Count) { throw "Existing install does not match the pinned assets: $($diff -join '; ')" }
}
if (-not $PSCmdlet.ShouldProcess($serviceRoot, 'Install pinned worker prerequisites')) { return }

if (-not $alreadyInstalled) {
    New-Item -ItemType Directory -Force -Path $pwshDir, $moduleDir | Out-Null
    Expand-Archive -LiteralPath $pwshZip -DestinationPath $pwshDir

    $zip = [IO.Compression.ZipFile]::OpenRead($moduleNupkg)
    try {
        foreach ($e in $zip.Entries) {
            $n = $e.FullName
            if ($n.EndsWith('/') -or (& $nupkgMetadata $n)) { continue }
            $target = Join-Path $moduleDir ([Uri]::UnescapeDataString($n) -replace '/', '\')
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $target, $false)
        }
    } finally { $zip.Dispose() }
}

# Machine binaries: SYSTEM/Administrators full, Users read+execute, inheritance from Program Files removed.
& icacls.exe $serviceRoot /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls failed ($LASTEXITCODE)" }

$pwshExe = Join-Path $pwshDir 'pwsh.exe'
[pscustomobject]@{
    AlreadyInstalled = $alreadyInstalled
    PwshPath       = $pwshExe
    PwshVersion    = (Get-Item $pwshExe).VersionInfo.FileVersion
    PwshSignature  = (Get-AuthenticodeSignature $pwshExe).Status
    ModulePath     = $moduleDir
    ModuleFiles    = (Get-ChildItem $moduleDir -Recurse -File).Count
    NativeDll      = (Get-Item (Join-Path $moduleDir 'net8.0-windows10.0.26100.0\SharedDependencies\x64\WindowsPackageManager.dll') -ErrorAction SilentlyContinue).VersionInfo.FileVersion
}
