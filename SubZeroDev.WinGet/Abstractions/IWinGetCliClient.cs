using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// The deliberately-isolated winget.exe CLI shim. Only features with no COM equivalent in
/// Microsoft.Management.Deployment live here: pin management and export/import. Everything else
/// in this library goes through the COM API and never launches a process.
/// </summary>
public interface IWinGetCliClient
{
    Task<IReadOnlyList<PackagePin>> GetPins(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a package. With <paramref name="version"/> set, gates it to that version (wildcards
    /// like "1.2.*" are allowed); with <paramref name="blocking"/>, blocks upgrades entirely.
    /// </summary>
    Task<CliOperationResult> AddPin(string packageId, string? version = null, bool blocking = false, bool pinInstalledVersion = false, CancellationToken cancellationToken = default);

    Task<CliOperationResult> RemovePin(string packageId, bool pinInstalledVersion = false, CancellationToken cancellationToken = default);

    /// <summary>Exports the installed-package list to a winget import-compatible JSON file.</summary>
    Task<CliOperationResult> Export(string filePath, bool includeVersions = false, string? sourceName = null, CancellationToken cancellationToken = default);

    /// <summary>Installs all packages listed in a winget export JSON file.</summary>
    Task<CliOperationResult> Import(string filePath, bool ignoreUnavailable = false, bool ignoreVersions = false, CancellationToken cancellationToken = default);
}
