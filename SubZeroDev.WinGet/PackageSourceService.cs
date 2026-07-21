using Microsoft.Extensions.Logging;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet;

/// <inheritdoc />
public sealed class PackageSourceService(IWinGetSourceClient sourceClient, ILogger<PackageSourceService> logger)
    : IPackageSourceService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<PackageSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return sourceClient.GetSourcesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageSource?> GetSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        return sourceClient.GetSourceAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> AddSourceAsync(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireName(request.Name);

        if (string.IsNullOrWhiteSpace(request.Uri))
        {
            throw new ArgumentException("A source URI is required.", nameof(request));
        }

        logger.LogInformation("Adding WinGet source {Name} ({Uri})", request.Name, request.Uri);

        var result = await sourceClient.AddSourceAsync(request, progress, cancellationToken);

        LogOutcome("Add", request.Name, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> RemoveSourceAsync(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        logger.LogInformation("Removing WinGet source {Name} (preserve data: {PreserveData})", name, preserveData);

        var result = await sourceClient.RemoveSourceAsync(name, preserveData, progress, cancellationToken);

        LogOutcome("Remove", name, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> RefreshSourceAsync(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        logger.LogInformation("Refreshing WinGet source {Name}", name);

        var result = await sourceClient.RefreshSourceAsync(name, progress, cancellationToken);

        LogOutcome("Refresh", name, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> UpdateSourceAsync(string name, bool? isExplicit = null, int? priority = null, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        if (isExplicit is null && priority is null)
        {
            throw new ArgumentException("At least one of isExplicit or priority must be provided.");
        }

        logger.LogInformation("Updating WinGet source {Name} (explicit: {Explicit}, priority: {Priority})", name, isExplicit, priority);

        var result = await sourceClient.UpdateSourceAsync(name, isExplicit, priority, cancellationToken);

        LogOutcome("Update", name, result);

        return result;
    }

    private static void RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A source name is required.", nameof(name));
        }
    }

    private void LogOutcome(string operation, string name, SourceOperationResult result)
    {
        if (result.Succeeded)
        {
            logger.LogInformation("{Operation} source succeeded for {Name}", operation, name);
        }
        else
        {
            logger.LogWarning("{Operation} source failed for {Name}: {Error} (0x{ExtendedErrorCode:X8})", operation, name, result.ErrorMessage, result.ExtendedErrorCode ?? 0);
        }
    }
}
