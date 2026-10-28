<#
    Shared read-only probes for the independent machine-scope verifiers. No WinGet, no module, no network.
#>
if (-not ('S00.Msi' -as [type])) {
    Add-Type -Namespace S00 -Name Msi -MemberDefinition @'
[DllImport("msi.dll", CharSet = CharSet.Unicode)]
public static extern uint MsiGetProductInfoEx(string szProductCode, string szUserSid, int dwContext, string szProperty, System.Text.StringBuilder szValue, ref uint pcchValue);
[DllImport("msi.dll", CharSet = CharSet.Unicode)]
public static extern uint MsiEnumProductsEx(string szProductCode, string szUserSid, int dwContext, uint dwIndex, System.Text.StringBuilder szInstalledProductCode, out int pdwInstalledContext, System.Text.StringBuilder szSid, ref uint pcchSid);
'@
}

# Every installation of a product code in the requested contexts, across all users ("s-1-1-0" is only valid for
# MsiEnumProductsEx, not MsiGetProductInfoEx). Context mask: USERMANAGED 1 | USERUNMANAGED 2 | MACHINE 4.
# PowerShell binds $null to "" for .NET string arguments; MSI needs a real NULL, hence [NullString]::Value.
# MACHINE-only enumeration requires a NULL SID (87 otherwise). An empty -ProductCode enumerates every product.
function Get-S00MsiInstallations {
    param([string] $ProductCode, [int] $ContextMask = 7)
    $pc = if ($ProductCode) { $ProductCode } else { [NullString]::Value }
    $sidArg = if ($ContextMask -eq 4) { [NullString]::Value } else { 's-1-1-0' }
    for ($i = [uint32]0; ; $i++) {
        $code = [Text.StringBuilder]::new(39)
        $sid = [Text.StringBuilder]::new(256)
        $sidLen = [uint32]256
        $ctx = 0
        $rc = [S00.Msi]::MsiEnumProductsEx($pc, $sidArg, $ContextMask, $i, $code, [ref]$ctx, $sid, [ref]$sidLen)
        if ($rc -eq 259) { break }   # ERROR_NO_MORE_ITEMS
        if ($rc -eq 5) { throw 'MsiEnumProductsEx failed: 5 (access denied). All-users enumeration needs SYSTEM or elevated Administrator; the check fails closed.' }
        if ($rc -ne 0) { throw "MsiEnumProductsEx failed: $rc" }
        [pscustomobject]@{ ProductCode = $code.ToString(); Context = $ctx; UserSid = $sid.ToString() }
    }
}

# MSIINSTALLCONTEXT_USERMANAGED = 1, USERUNMANAGED = 2, MACHINE = 4. ERROR_UNKNOWN_PRODUCT = 1605.
function Get-S00MsiProperty {
    param([string] $ProductCode, [int] $Context, [string] $UserSid, [string] $Property = 'VersionString')
    $len = [uint32]256
    $sb = [Text.StringBuilder]::new([int]$len)
    $sidArg = if ($UserSid) { $UserSid } else { [NullString]::Value }
    $rc = [S00.Msi]::MsiGetProductInfoEx($ProductCode, $sidArg, $Context, $Property, $sb, [ref]$len)
    [pscustomobject]@{ Context = $Context; UserSid = $UserSid; ReturnCode = $rc; Value = if ($rc -eq 0) { $sb.ToString() } }
}

function ConvertTo-S00PackedGuid {
    param([guid] $Guid)
    $s = $Guid.ToString('N').ToUpperInvariant()
    $rev = { param($x) -join ($x.ToCharArray()[($x.Length - 1)..0]) }
    $out = (& $rev $s.Substring(0, 8)) + (& $rev $s.Substring(8, 4)) + (& $rev $s.Substring(12, 4))
    for ($i = 16; $i -lt 32; $i += 2) { $out += $s[$i + 1] + $s[$i] }
    $out
}

function Get-S00UserHives {
    # Loaded hives only; the evidence collector runs after reboot with no users signed in, so typically only
    # S-1-5-18/19/20 and .DEFAULT are loaded. Unloaded profiles are checked via ProfileList + NTUSER.DAT presence.
    # Unreadable hives (non-elevated caller) are a terminating error: the per-user check must fail closed.
    Get-ChildItem Registry::HKEY_USERS -ErrorAction Stop | Where-Object { $_.PSChildName -notlike '*_Classes' } | ForEach-Object PSChildName
}

function Get-S00Uninstall {
    param([string] $KeyName)
    $paths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$KeyName",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$KeyName"
    )
    foreach ($p in $paths) {
        $v = Get-ItemProperty -LiteralPath $p -ErrorAction SilentlyContinue
        [pscustomobject]@{ Path = $p; Exists = [bool]$v; DisplayVersion = if ($v) { $v.DisplayVersion }; InstallLocation = if ($v) { $v.InstallLocation } }
    }
}

function New-S00Check {
    param([string] $Name, [bool] $Pass, $Observed)
    [pscustomobject]@{ Check = $Name; Pass = $Pass; Observed = $Observed }
}

Export-ModuleMember -Function Get-S00MsiProperty, Get-S00MsiInstallations,ConvertTo-S00PackedGuid, Get-S00UserHives, Get-S00Uninstall, New-S00Check
