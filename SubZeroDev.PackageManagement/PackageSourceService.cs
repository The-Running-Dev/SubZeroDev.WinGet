using Microsoft.Extensions.Logging;

using SubZeroDev.PackageManagement.Abstractions;
using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement;

/// <inheritdoc />
public sealed class PackageSourceService : IPackageSourceService
{
    private readonly IWinGetSourceClient _sourceClient;

    private readonly ILogger<PackageSourceService> _logger;

    public PackageSourceService(IWinGetSourceClient sourceClient, ILogger<PackageSourceService> logger)
    {
        _sourceClient = sourceClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return _sourceClient.GetSourcesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageSource?> GetSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        return _sourceClient.GetSourceAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> AddSourceAsync(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireName(request.Name);

        if (string.IsNullOrWhiteSpace(request.Uri))
        {
            throw new ArgumentException("A source URI is required.", nameof(request));
        }

        _logger.LogInformation("Adding WinGet source {Name} ({Uri})", request.Name, request.Uri);

        var result = await _sourceClient.AddSourceAsync(request, progress, cancellationToken);

        LogOutcome("Add", request.Name, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> RemoveSourceAsync(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        _logger.LogInformation("Removing WinGet source {Name} (preserve data: {PreserveData})", name, preserveData);

        var result = await _sourceClient.RemoveSourceAsync(name, preserveData, progress, cancellationToken);

        LogOutcome("Remove", name, result);

        return result;
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> RefreshSourceAsync(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireName(name);

        _logger.LogInformation("Refreshing WinGet source {Name}", name);

        var result = await _sourceClient.RefreshSourceAsync(name, progress, cancellationToken);

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

        _logger.LogInformation("Updating WinGet source {Name} (explicit: {Explicit}, priority: {Priority})", name, isExplicit, priority);

        var result = await _sourceClient.UpdateSourceAsync(name, isExplicit, priority, cancellationToken);

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
            _logger.LogInformation("{Operation} source succeeded for {Name}", operation, name);
        }
        else
        {
            _logger.LogWarning("{Operation} source failed for {Name}: {Error} (0x{ExtendedErrorCode:X8})", operation, name, result.ErrorMessage, result.ExtendedErrorCode ?? 0);
        }
    }
}
