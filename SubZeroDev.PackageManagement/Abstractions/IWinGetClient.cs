using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement.Abstractions;

/// <summary>
/// Thin wrapper over the WinGet COM API (Microsoft.Management.Deployment). Kept minimal and
/// interface-based so the service layer can be unit tested without touching the real COM/WinRT
/// layer. Methods are single-attempt translations of the COM calls; retry policy lives in the
/// service layer.
/// </summary>
public interface IWinGetClient
{
    /// <summary>The version of the WinGet backend servicing this client, if available.</summary>
    Task<string?> GetWinGetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches remote catalogs (matching on Id, Name, Moniker, and Tag), with installed state
    /// correlated into each result. Pass a source name to restrict to one catalog.
    /// </summary>
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int limit, string? sourceName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Installed packages that have a newer version available in any configured source.</summary>
    Task<IReadOnlyList<PackageInfo>> GetAvailableUpgradesAsync(CancellationToken cancellationToken = default);

    Task<PackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>Full manifest metadata (description, license, agreements, icons, versions, …).</summary>
    Task<PackageDetails?> GetPackageDetailsAsync(string packageId, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> InstallAsync(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> UpgradeAsync(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> UninstallAsync(string packageId, UninstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Downloads the package's installer without running it.</summary>
    Task<PackageOperationResult> DownloadAsync(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> RepairAsync(string packageId, RepairRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
