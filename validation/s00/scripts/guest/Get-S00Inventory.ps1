#Requires -Version 5.1
<#
.SYNOPSIS
    Captures the S00 guest inventory: OS edition/build/UBR, servicing state, App Installer and framework packages,
    WinGet policy, sessions, and the private worker/module as installed. Read-only.
.DESCRIPTION
    PREPARED, NOT EXECUTED.
    Run from an elevated Windows PowerShell 5.1 prompt in the guest before the prerequisites-ready checkpoint.
    Writes JSON to -OutFile (default: Evidence\S00-inventory-<utc>.json). Contains no user names: session rows
    keep SessionId, state and station name only (redaction rule, 05-run-reset-procedure.md).

    App Installer is inventoried, not required: the S00 route uses the in-proc module, and its presence or absence
    must be recorded either way so S01 can attribute behaviour.

    Baseline check: the guest must be Windows 11 Pro 26H2 x64 (BC-1, approved 2026-10-28). The JSON is always
    written; the script then prints BASELINE PASS, or BASELINE FAIL with each mismatch and exits 1.
#>
[CmdletBinding()]
param(
    [string] $OutFile = (Join-Path $env:ProgramData ("SubZeroDev\FileManager\Validation\Evidence\S00-inventory-{0:yyyyMMddTHHmmssZ}.json" -f [DateTime]::UtcNow))
)
$ErrorActionPreference = 'Stop'

$cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem

function Get-SessionRows {
    # quser prints user names; keep only session id and state. No output (exit 1) means no user sessions.
    # With no user signed in, quser writes "No User exists for *" to stderr; under Stop, Windows PowerShell 5.1
    # turns that redirected stderr into a terminating NativeCommandError. Continue applies to this function only.
    $ErrorActionPreference = 'Continue'
    $raw = & "$env:windir\System32\quser.exe" 2>$null
    foreach ($line in ($raw | Select-Object -Skip 1)) {
        $cols = ($line.Trim() -replace '\s{2,}', '|').Split('|')
        [pscustomobject]@{
            SessionId = $cols | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1
            State     = $cols | Where-Object { $_ -in 'Active', 'Disc' } | Select-Object -First 1
        }
    }
}

$serviceRoot = Join-Path $env:ProgramFiles 'SubZeroDev\FileManager\Service'
$pwshExe = Join-Path $serviceRoot 'pwsh\pwsh.exe'
$moduleDir = Join-Path $serviceRoot 'Modules\Microsoft.WinGet.Client\1.29.380'

# Approved baseline (BC-1). Passing this establishes nothing about 25H2.
$expected = [ordered]@{ EditionID = 'Professional'; DisplayVersion = '26H2'; CurrentBuild = '26300'; Architecture = 'AMD64' }
$observed = [ordered]@{
    EditionID      = "$($cv.EditionID)"
    DisplayVersion = "$($cv.DisplayVersion)"
    CurrentBuild   = "$($cv.CurrentBuild)"
    Architecture   = "$((Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment').PROCESSOR_ARCHITECTURE)"
}
$mismatches = @(foreach ($k in $expected.Keys) { if ($observed[$k] -ne $expected[$k]) { "$k expected '$($expected[$k])' observed '$($observed[$k])'" } })

$inventory = [ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    baseline = [ordered]@{ decision = 'BC-1'; expected = $expected; observed = $observed; pass = ($mismatches.Count -eq 0); mismatches = $mismatches }
    os = [ordered]@{
        ProductName      = $cv.ProductName
        EditionID        = $cv.EditionID
        DisplayVersion   = $cv.DisplayVersion
        CurrentBuild     = $cv.CurrentBuild
        UBR              = $cv.UBR
        BuildLabEx       = $cv.BuildLabEx
        Caption          = $os.Caption
        Version          = $os.Version
        OSArchitecture   = $os.OSArchitecture
        InstallDateUtc   = $os.InstallDate.ToUniversalTime().ToString('o')
        LastBootUtc      = $os.LastBootUpTime.ToUniversalTime().ToString('o')
        Model            = $cs.Model
        Manufacturer     = $cs.Manufacturer
        PartOfDomain     = $cs.PartOfDomain
        SecureBoot       = $(try { Confirm-SecureBootUEFI } catch { "error: $($_.Exception.Message)" })
        Tpm              = $(try { (Get-Tpm | Select-Object TpmPresent, TpmReady | Out-String).Trim() } catch { "error: $($_.Exception.Message)" })
    }
    updates = [ordered]@{
        HotFix           = @(Get-HotFix | Sort-Object InstalledOn | ForEach-Object { "$($_.HotFixID) $($_.Description) $($_.InstalledOn)" })
        PendingReboot    = [ordered]@{
            CbsRebootPending   = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
            WuRebootRequired   = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
            PendingFileRename  = [bool](Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue)
        }
    }
    # Get-AppxPackage -Name takes one pattern, so query each.
    appx = @('Microsoft.DesktopAppInstaller', 'Microsoft.VCLibs.140.00*', 'Microsoft.UI.Xaml.*', 'Microsoft.WindowsAppRuntime.*' | ForEach-Object { Get-AppxPackage -AllUsers -Name $_ } |
        ForEach-Object { [ordered]@{ Name = $_.Name; Version = $_.Version.ToString(); Arch = $_.Architecture.ToString(); PackageFullName = $_.PackageFullName; Status = $_.Status.ToString() } })
    appxProvisioned = @(Get-AppxProvisionedPackage -Online | Where-Object DisplayName -in 'Microsoft.DesktopAppInstaller', 'Microsoft.VCLibs.140.00.UWPDesktop' |
        ForEach-Object { "$($_.DisplayName) $($_.Version)" })
    wingetPolicy = (Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppInstaller' -ErrorAction SilentlyContinue |
        Select-Object * -ExcludeProperty PS* | ConvertTo-Json -Compress)
    worker = [ordered]@{
        PwshPath         = $pwshExe
        PwshFileVersion  = $(if (Test-Path $pwshExe) { (Get-Item $pwshExe).VersionInfo.FileVersion })
        # The .NET runtime pwsh actually loads (expected 10.0.12, accepted subject to this check; the service host is 10.0.14).
        PwshRuntime      = $(if (Test-Path $pwshExe) { "$(& $pwshExe -NoProfile -NonInteractive -Command '[System.Environment]::Version.ToString()')".Trim() })
        PwshOnPath       = [bool]($env:Path -split ';' | Where-Object { $_ -and (Test-Path (Join-Path $_ 'pwsh.exe')) })
        ModulePath       = $moduleDir
        ModuleFileCount  = $(if (Test-Path $moduleDir) { (Get-ChildItem $moduleDir -Recurse -File).Count })
        ModuleInMachinePSModulePath = [Environment]::GetEnvironmentVariable('PSModulePath', 'Machine') -like "*$serviceRoot*"
        ModuleFileHashes = $(if (Test-Path $moduleDir) { @(Get-ChildItem $moduleDir -Recurse -File | ForEach-Object { "$((Get-FileHash $_.FullName).Hash)  $($_.FullName.Substring($moduleDir.Length + 1))" }) })
    }
    executionAliases = @(Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WindowsApps" -Filter 'winget.exe' -ErrorAction SilentlyContinue | ForEach-Object FullName)
    packagesAbsent = [ordered]@{
        PuTTYUninstallKey   = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{3B0346D3-155D-43E5-9561-B9BE51CF2EB6}'
        PuTTYDir            = Test-Path (Join-Path $env:ProgramFiles 'PuTTY')
        SevenZipUninstallKey = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\7-Zip'
        SevenZipDir         = Test-Path (Join-Path $env:ProgramFiles '7-Zip')
    }
    sessions = @(Get-SessionRows)
    autoAdminLogon = "$((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon').AutoAdminLogon)"
    network = [ordered]@{
        WinHttpProxy     = (& netsh.exe winhttp show proxy | Out-String).Trim()
        CdnReachable     = $(try { (Invoke-WebRequest 'https://cdn.winget.microsoft.com/cache/source2.msix' -Method Head -UseBasicParsing -TimeoutSec 20).StatusCode } catch { "error: $($_.Exception.Message)" })
    }
    shares = @(Get-SmbMapping -ErrorAction SilentlyContinue | ForEach-Object RemotePath)
}

New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutFile -Encoding UTF8
Write-Output $OutFile
if ($mismatches.Count -gt 0) { Write-Output 'BASELINE FAIL'; $mismatches | ForEach-Object { Write-Output "  $_" }; exit 1 }
Write-Output 'BASELINE PASS (Windows 11 Pro 26H2 x64)'
