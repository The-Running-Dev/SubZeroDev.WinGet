using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// The package-management surface consumers depend on. Adds input validation, structured
/// logging, and a small documented auto-retry policy for known-recoverable WinGet error codes
/// on top of <see cref="IWinGetClient"/> and <see cref="IWinGetCliClient"/>.
/// </summary>
public interface IPackageManagementService
{
    Task<string?> GetWinGetVersion(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> Search(string query, string? sourceName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetInstalled(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackageInfo>> GetAvailableUpgrades(CancellationToken cancellationToken = default);

    Task<PackageInfo?> GetPackage(string packageId, CancellationToken cancellationToken = default);

    Task<PackageDetails?> GetDetails(string packageId, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Install(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Update(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Uninstall(string packageId, UninstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Download(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<PackageOperationResult> Repair(string packageId, RepairRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PackagePin>> GetPins(CancellationToken cancellationToken = default);

    Task<CliOperationResult> Pin(string packageId, string? version = null, bool blocking = false, CancellationToken cancellationToken = default);

    Task<CliOperationResult> Unpin(string packageId, CancellationToken cancellationToken = default);

    Task<CliOperationResult> Export(string filePath, bool includeVersions = false, CancellationToken cancellationToken = default);

    Task<CliOperationResult> Import(string filePath, bool ignoreUnavailable = false, bool ignoreVersions = false, CancellationToken cancellationToken = default);
}
