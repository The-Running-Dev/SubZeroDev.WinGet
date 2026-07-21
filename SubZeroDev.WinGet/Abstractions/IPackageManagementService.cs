using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// The package-management surface consumers depend on. Adds input validation, structured
/// logging, and a small documented auto-retry policy for known-recoverable WinGet error codes
/// on top of <see cref="IWinGetClient"/> and <see cref="IWinGetCliClient"/>.
/// </summary>
public interface IPackageManagementService
{
    Task<string?> GetWinGetVersionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, string? sourceName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetAvailableUpgradesAsync(CancellationToken cancellationToken = default);

    Task<PackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken = default);

    Task<PackageDetails?> GetDetailsAsync(string packageId, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> InstallAsync(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> UpdateAsync(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> UninstallAsync(string packageId, UninstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> DownloadAsync(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> RepairAsync(string packageId, RepairRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackagePin>> GetPinsAsync(CancellationToken cancellationToken = default);

    Task<CliOperationResult> PinAsync(string packageId, string? version = null, bool blocking = false, CancellationToken cancellationToken = default);

    Task<CliOperationResult> UnpinAsync(string packageId, CancellationToken cancellationToken = default);

    Task<CliOperationResult> ExportAsync(string filePath, bool includeVersions = false, CancellationToken cancellationToken = default);

    Task<CliOperationResult> ImportAsync(string filePath, bool ignoreUnavailable = false, bool ignoreVersions = false, CancellationToken cancellationToken = default);
}
