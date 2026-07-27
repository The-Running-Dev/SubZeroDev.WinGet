using Microsoft.Extensions.Logging;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet;

/// <summary>
/// Business-logic layer over <see cref="IWinGetClient"/> and <see cref="IWinGetCliClient"/>:
/// input validation, structured logging, result normalization, and a small documented
/// auto-retry policy for error codes that real-world WinGet automation (UniGetUI,
/// Winget-AutoUpdate) has shown to be recoverable by adjusting options:
/// <list type="bullet">
/// <item>"Already installed"-family errors on install are normalized to success.</item>
/// <item>NoApplicableInstallers/NoApplicableUpgrade with an architecture, installer-type, or
/// scope constraint retries once unconstrained (the constraint may exclude the only installer
/// the package ships).</item>
/// <item>UPGRADE_VERSION_UNKNOWN (0x8A150050) on update retries once with
/// AllowUpgradeToUnknownVersion enabled.</item>
/// </list>
/// </summary>
public sealed class PackageManagementService(
    IWinGetClient winGetClient,
    IWinGetCliClient cliClient,
    ILogger<PackageManagementService> logger)
    : IPackageManagementService
{
    private const int DefaultSearchLimit = 50;

    /// <inheritdoc />
    public Task<string?> GetWinGetVersion(CancellationToken cancellationToken = default)
    {
        return winGetClient.GetWinGetVersion(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageInfo>> Search(string query, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PackageInfo>();
        }

        logger.LogDebug("Searching for packages matching {Query}", query);

        return await winGetClient.Search(query.Trim(), DefaultSearchLimit, sourceName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageInfo>> GetInstalled(CancellationToken cancellationToken = default)
    {
        return winGetClient.GetInstalledPackages(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageInfo>> GetAvailableUpgrades(CancellationToken cancellationToken = default)
    {
        return winGetClient.GetAvailableUpgrades(cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageInfo?> GetPackage(string packageId, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        return winGetClient.GetPackage(packageId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageDetails?> GetDetails(string packageId, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        return winGetClient.GetPackageDetails(packageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> Install(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);
        request ??= new InstallRequest();

        logger.LogInformation("Installing {PackageId}", packageId);

        var result = await winGetClient.Install(packageId, request, progress, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded && IsAlreadyInstalled(result))
        {
            logger.LogInformation("{PackageId} is already installed; treating install as successful", packageId);

            return PackageOperationResult.Success();
        }

        if (ShouldRetryUnconstrained(result, request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("{PackageId} has no applicable installer under the requested constraints; retrying without architecture/installer-type/scope constraints", packageId);

            result = await winGetClient.Install(packageId, Unconstrained(request), progress, cancellationToken).ConfigureAwait(false);
        }

        LogOutcome("Install", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> Update(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);
        request ??= new InstallRequest();

        logger.LogInformation("Updating {PackageId}", packageId);

        var result = await winGetClient.Upgrade(packageId, request, progress, cancellationToken).ConfigureAwait(false);

        if (ShouldRetryUnconstrained(result, request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("{PackageId} has no applicable upgrade under the requested constraints; retrying without architecture/installer-type/scope constraints", packageId);

            result = await winGetClient.Upgrade(packageId, Unconstrained(request), progress, cancellationToken).ConfigureAwait(false);
        }
        else if (!result.Succeeded && result.ExtendedErrorCode == WinGetErrorCodes.UpgradeVersionUnknown && !request.AllowUpgradeToUnknownVersion)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("{PackageId} reports an unknown installed version; retrying with AllowUpgradeToUnknownVersion", packageId);

            result = await winGetClient.Upgrade(packageId, request with { AllowUpgradeToUnknownVersion = true }, progress, cancellationToken).ConfigureAwait(false);
        }

        LogOutcome("Update", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> Uninstall(string packageId, UninstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        logger.LogInformation("Uninstalling {PackageId}", packageId);

        var result = await winGetClient.Uninstall(packageId, request, progress, cancellationToken).ConfigureAwait(false);

        LogOutcome("Uninstall", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> Download(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        if (string.IsNullOrWhiteSpace(request.DownloadDirectory))
        {
            throw new ArgumentException("A download directory is required.", nameof(request));
        }

        logger.LogInformation("Downloading {PackageId} to {Directory}", packageId, request.DownloadDirectory);

        var result = await winGetClient.Download(packageId, request, progress, cancellationToken).ConfigureAwait(false);

        LogOutcome("Download", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> Repair(string packageId, RepairRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        logger.LogInformation("Repairing {PackageId}", packageId);

        var result = await winGetClient.Repair(packageId, request, progress, cancellationToken).ConfigureAwait(false);

        LogOutcome("Repair", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackagePin>> GetPins(CancellationToken cancellationToken = default)
    {
        return cliClient.GetPins(cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> Pin(string packageId, string? version = null, bool blocking = false, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        logger.LogInformation("Pinning {PackageId} (version: {Version}, blocking: {Blocking})", packageId, version ?? "any", blocking);

        return cliClient.AddPin(packageId, version, blocking, pinInstalledVersion: false, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> Unpin(string packageId, CancellationToken cancellationToken = default)
    {
        RequirePackageId(packageId);

        logger.LogInformation("Unpinning {PackageId}", packageId);

        return cliClient.RemovePin(packageId, pinInstalledVersion: false, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> Export(string filePath, bool includeVersions = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A file path is required.", nameof(filePath));
        }

        logger.LogInformation("Exporting installed packages to {FilePath}", filePath);

        return cliClient.Export(filePath, includeVersions, sourceName: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> Import(string filePath, bool ignoreUnavailable = false, bool ignoreVersions = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A file path is required.", nameof(filePath));
        }

        logger.LogInformation("Importing packages from {FilePath}", filePath);

        return cliClient.Import(filePath, ignoreUnavailable, ignoreVersions, cancellationToken);
    }

    private static void RequirePackageId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Package id is required.", nameof(packageId));
        }
    }

    private static bool IsAlreadyInstalled(PackageOperationResult result) =>
        result.ExtendedErrorCode is WinGetErrorCodes.PackageAlreadyInstalled
            or WinGetErrorCodes.InstallAlreadyInstalled
            or WinGetErrorCodes.InstallDowngrade
            or WinGetErrorCodes.UpgradeVersionNotNewer;

    private static bool ShouldRetryUnconstrained(PackageOperationResult result, InstallRequest request) =>
        !result.Succeeded
        && result.Status is PackageOperationStatus.NoApplicableInstallers or PackageOperationStatus.NoApplicableUpgrade
        && (request.Architecture != PackageArchitecture.Default
            || request.InstallerType != PackageInstallerKind.Default
            || request.Scope != PackageScope.Any);

    private static InstallRequest Unconstrained(InstallRequest request) => request with
    {
        Architecture = PackageArchitecture.Default,
        InstallerType = PackageInstallerKind.Default,
        Scope = PackageScope.Any
    };

    private void LogOutcome(string operation, string packageId, PackageOperationResult result)
    {
        if (result.Succeeded)
        {
            logger.LogInformation("{Operation} succeeded for {PackageId} (reboot required: {RebootRequired})", operation, packageId, result.RebootRequired);
        }
        else
        {
            logger.LogWarning("{Operation} failed for {PackageId}: {Status} {Error} (0x{ExtendedErrorCode:X8})", operation, packageId, result.Status, result.ErrorMessage, result.ExtendedErrorCode ?? 0);
        }
    }
}
