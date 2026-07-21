using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement.Abstractions;

/// <summary>
/// Thin wrapper over the WinGet COM API (Microsoft.Management.Deployment). Kept minimal and
/// interface-based specifically so <see cref="PackageManagementService"/> can be unit tested
/// without touching the real COM/WinRT layer.
/// </summary>
public interface IWinGetClient
{
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken);

    Task<PackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken);

    Task<PackageOperationResult> InstallAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken);

    Task<PackageOperationResult> UpgradeAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken);

    Task<PackageOperationResult> UninstallAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken);
}
