# Attempt 1 host captures (2026-10-28)

[Get-S00VmConfig.ps1](../../../scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session, during the first run of [01 § Operator procedure](../../../01-environment-inventory.md#operator-procedure-prepare-windows11-test). These are the captures behind the D-9 incident record in [01 § D-9](../../../01-environment-inventory.md#d-9-existing-127-gib-disk-approved-2026-10-28-revised-the-same-day). Their `checks` were computed by the script version of that run, which checked for the retained checkpoint; that rule was revised afterwards.

| File | capturedUtc | Procedure step | Notes |
|---|---|---|---|
| `vm-config.json` | 2026-10-28T20:36:57.7480987Z | 3, before preparation | Written over `evidence/host/vm-config.json`; moved here, and the 19:01:44Z capture was restored from git. Checkpoint `87637f8f-a956-40a9-b101-31d982f890cf` present |
| `vm-config.after-preparation.json` | 2026-10-28T20:38:17.0661762Z | 4, after [Set-S00VmPreparation.ps1](../../../scripts/host/Set-S00VmPreparation.ps1) | No checkpoints; the `.avhdx` still attached while Hyper-V merged it |
| `vm-config.after-checkpoint.json` | 2026-10-28T20:38:46.4918438Z | 6, after provisioning attempt 1 | Named after the step only: no checkpoint was taken, because [attempt 1](../../guest/provisioning-20261028T203829Z/provisioning.log) stopped at `Start-VM` (0x800705AA). Shows the merged layout: the base VHDX alone, no checkpoints |

No content was changed. No redaction was needed: the script writes no host or guest machine name, FQDN or IP address.
