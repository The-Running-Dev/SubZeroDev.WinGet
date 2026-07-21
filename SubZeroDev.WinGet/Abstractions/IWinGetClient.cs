using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// Thin wrapper over the WinGet COM API (Microsoft.Management.Deployment). Kept minimal and
/// interface-based so the service layer can be unit tested without touching the real COM/WinRT
/// layer. Methods are single-attempt translations of the COM calls; retry policy lives in the
/// service layer.
/// </summary>
public interface IWinGetClient
{
    /// <summary>The version of the WinGet backend servicing this client, if available.</summary>
    Task<string?> GetWinGetVersion(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches remote catalogs (matching on Id, Name, Moniker, and Tag), with installed state
    /// correlated into each result. Pass a source name to restrict to one catalog.
    /// </summary>
    Task<IReadOnlyList<PackageInfo>> Search(string query, int limit, string? sourceName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetInstalledPackages(CancellationToken cancellationToken = default);

    /// <summary>Installed packages that have a newer version available in any configured source.</summary>
    Task<IReadOnlyList<PackageInfo>> GetAvailableUpgrades(CancellationToken cancellationToken = default);

    Task<PackageInfo?> GetPackage(string packageId, CancellationToken cancellationToken = default);

    /// <summary>Full manifest metadata (description, license, agreements, icons, versions, …).</summary>
    Task<PackageDetails?> GetPackageDetails(string packageId, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Install(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Upgrade(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Uninstall(string packageId, UninstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Downloads the package's installer without running it.</summary>
    Task<PackageOperationResult> Download(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Repair(string packageId, RepairRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
