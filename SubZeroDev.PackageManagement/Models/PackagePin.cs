namespace SubZeroDev.PackageManagement.Models;

public enum PackagePinKind
{
    /// <summary>Pinned: excluded from bulk "upgrade all", but explicit upgrade is allowed.</summary>
    Pinning,

    /// <summary>Blocked: cannot be upgraded until the pin is removed.</summary>
    Blocking,

    /// <summary>Gated: may only be upgraded within the pinned version pattern (e.g. 1.2.*).</summary>
    Gating
}

/// <summary>
/// A WinGet package pin (the "winget pin list" equivalent). Pins have no COM API in
/// Microsoft.Management.Deployment; they are read and written via the winget CLI.
/// </summary>
public sealed record PackagePin(
    string Id,
    string Name,
    string Version,
    PackagePinKind Kind,
    string Source);

/// <summary>
/// The outcome of a CLI-backed operation (pin management, export/import). Captures the raw
/// process output since these operations have no structured result objects.
/// </summary>
public sealed record CliOperationResult(bool Succeeded, int ExitCode, string Output, string Error)
{
    public string ExitCodeHex => $"0x{ExitCode:X8}";
}
