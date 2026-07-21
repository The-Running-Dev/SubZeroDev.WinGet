using Microsoft.Extensions.Logging;

using SubZeroDev.PackageManagement.Abstractions;
using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement;

/// <inheritdoc />
public sealed class PackageManagementService : IPackageManagementService
{
    private const int DefaultSearchLimit = 50;

    private readonly IWinGetClient _winGetClient;

    private readonly ILogger<PackageManagementService> _logger;

    public PackageManagementService(IWinGetClient winGetClient, ILogger<PackageManagementService> logger)
    {
        _winGetClient = winGetClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PackageInfo>();
        }

        _logger.LogDebug("Searching for packages matching {Query}", query);

        return await _winGetClient.SearchAsync(query.Trim(), DefaultSearchLimit, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        return _winGetClient.GetInstalledPackagesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageInfo?> GetDetailsAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Package id is required.", nameof(packageId));
        }

        return _winGetClient.GetPackageAsync(packageId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> InstallAsync(
        string packageId,
        IProgress<PackageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing {PackageId}", packageId);

        var result = await _winGetClient.InstallAsync(packageId, progress, cancellationToken);

        LogOutcome("Install", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> UpdateAsync(
        string packageId,
        IProgress<PackageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating {PackageId}", packageId);

        var result = await _winGetClient.UpgradeAsync(packageId, progress, cancellationToken);

        LogOutcome("Update", packageId, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> UninstallAsync(
        string packageId,
        IProgress<PackageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uninstalling {PackageId}", packageId);

        var result = await _winGetClient.UninstallAsync(packageId, progress, cancellationToken);

        LogOutcome("Uninstall", packageId, result);

        return result;
    }

    private void LogOutcome(string operation, string packageId, PackageOperationResult result)
    {
        if (result.Succeeded)
        {
            _logger.LogInformation("{Operation} succeeded for {PackageId} (reboot required: {RebootRequired})", operation, packageId, result.RebootRequired);
        }
        else
        {
            _logger.LogWarning("{Operation} failed for {PackageId}: {Error} (0x{ExtendedErrorCode:X8})", operation, packageId, result.ErrorMessage, result.ExtendedErrorCode ?? 0);
        }
    }
}
