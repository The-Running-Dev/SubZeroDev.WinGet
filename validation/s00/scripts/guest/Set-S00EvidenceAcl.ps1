#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Creates the protected S00/S01 fixture and evidence folders and locks them to SYSTEM + Administrators.
.DESCRIPTION
    PREPARED, NOT EXECUTED.
    Root: %ProgramData%\SubZeroDev\FileManager\Validation
      Fixture\   frozen profiles, agreement/admission records, verifier scripts (read-only for the worker)
      Evidence\  one folder per case run (written by the S01 host as SYSTEM, read by the operator via PowerShell Direct)
      Work\      transient worker scratch; emptied by cleanup
    Inheritance is removed. Users, Authenticated Users and CREATOR OWNER get nothing. Prints the resulting SDDL.
#>
[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'

$root = Join-Path $env:ProgramData 'SubZeroDev\FileManager\Validation'
$dirs = 'Fixture', 'Evidence', 'Work' | ForEach-Object { Join-Path $root $_ }
if (-not $PSCmdlet.ShouldProcess($root, 'Create and lock validation folders')) { return }

New-Item -ItemType Directory -Force -Path (@($root) + $dirs) | Out-Null
& icacls.exe $root /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls failed ($LASTEXITCODE)" }
foreach ($d in $dirs) {
    & icacls.exe $d /reset | Out-Null
}

foreach ($d in @($root) + $dirs) {
    $acl = Get-Acl -LiteralPath $d
    $extra = $acl.Access | Where-Object {
        $sid = $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        $sid -notin 'S-1-5-18', 'S-1-5-32-544'
    }
    if ($extra) { throw "Unexpected ACE on ${d}: $($extra.IdentityReference -join ', ')" }
    [pscustomobject]@{ Path = $d; Sddl = $acl.Sddl }
}
