#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Applies the S00 machine policy pins that keep WinGet behaviour fixed for the SYSTEM worker.
.DESCRIPTION
    PREPARED, NOT EXECUTED.
    - HKLM\SOFTWARE\Policies\Microsoft\Windows\AppInstaller\EnableExperimentalFeatures = 0
      Pins experimental features (including resume) off for every context, so the result does not depend on
      a settings.json under any profile. See 04-worker-mapping.md for the source basis.
    - Records, without changing, Windows Update / restart policy so a pending Windows reboot cannot be confused
      with a package reboot during S01.
    Prints before/after values. Uses only inbox registry cmdlets; no LGPO tool.
#>
[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'

$key = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppInstaller'
$before = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
if ($PSCmdlet.ShouldProcess($key, 'EnableExperimentalFeatures=0')) {
    New-Item -Path $key -Force | Out-Null
    New-ItemProperty -LiteralPath $key -Name EnableExperimentalFeatures -PropertyType DWord -Value 0 -Force | Out-Null
}
$after = Get-ItemProperty -LiteralPath $key

[pscustomobject]@{
    Key                         = $key
    ExperimentalBefore          = $before.EnableExperimentalFeatures
    ExperimentalAfter           = $after.EnableExperimentalFeatures
    AllPolicyValues             = ($after.PSObject.Properties | Where-Object Name -notlike 'PS*' | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '; '
    WindowsUpdateAU             = (Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -ErrorAction SilentlyContinue | Select-Object NoAutoRebootWithLoggedOnUsers, AUOptions | Out-String).Trim()
}
