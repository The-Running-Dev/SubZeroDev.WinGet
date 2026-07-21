using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement.Abstractions;

/// <summary>
/// The public surface future callers (the web API, a scheduled task, etc.) depend on.
/// </summary>
public interface IPackageManagementService
{
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default);

    Task<PackageInfo?> GetDetailsAsync(string packageId, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> InstallAsync(string packageId, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> UpdateAsync(string packageId, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> UninstallAsync(string packageId, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
