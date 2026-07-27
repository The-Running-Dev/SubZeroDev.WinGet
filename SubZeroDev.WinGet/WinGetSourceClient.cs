using Microsoft.Management.Deployment;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Com;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet;

/// <summary>
/// Real implementation of <see cref="IWinGetSourceClient"/> against the WinGet COM API
/// (Microsoft.Management.Deployment). Uses the contract 12/28 source-management surface:
/// AddPackageCatalogAsync, RemovePackageCatalogAsync, EditPackageCatalog, and
/// RefreshPackageCatalogAsync.
/// </summary>
/// <remarks>
/// Ownership: as with <see cref="WinGetClient"/>, the public constructor starts a dedicated MTA
/// owner thread and the instance must be disposed. Instances resolved from
/// <c>AddPackageManagement()</c> share the container-owned context and need no disposal.
/// </remarks>
public sealed class WinGetSourceClient : IWinGetSourceClient, IDisposable
{
    private readonly WinGetComContext _context;
    private readonly bool _ownsContext;

    /// <summary>
    /// Creates a client that owns its own COM context and MTA owner thread. Dispose it when
    /// finished. Prefer <c>AddPackageManagement()</c>, which shares one context across all
    /// clients and disposes it with the provider.
    /// </summary>
    public WinGetSourceClient()
        : this(new WinGetComContext(), ownsContext: true)
    {
    }

    internal WinGetSourceClient(WinGetFactory factory)
        : this(new WinGetComContext(factory), ownsContext: true)
    {
    }

    internal WinGetSourceClient(WinGetComContext context)
        : this(context, ownsContext: false)
    {
    }

    private WinGetSourceClient(WinGetComContext context, bool ownsContext)
    {
        _context = context;
        _ownsContext = ownsContext;
    }

    private WinGetFactory Factory => _context.Factory;

    private PackageManager PackageManager => _context.PackageManager;

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageSource>> GetSources(CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetSourcesCore(cancellationToken), cancellationToken);

    private IReadOnlyList<PackageSource> GetSourcesCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalogs = PackageManager.GetPackageCatalogs();
        var sources = new List<PackageSource>(catalogs.Count);

            // Do NOT convert to foreach/LINQ: the CsWinRT-projected list's enumerator throws
            // InvalidCastException ("No such interface supported") on interop 1.29.280.
            // Only indexer access (.Count / [i]) is reliable. Verified by the live
            // GetSources integration test.
        for (var i = 0; i < catalogs.Count; i++)
        {
            sources.Add(ToPackageSource(catalogs[i].Info));
        }

        return sources;
    }

    /// <inheritdoc />
    public Task<PackageSource?> GetSource(string name, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetSourceCore(name, cancellationToken), cancellationToken);

    private PackageSource? GetSourceCore(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var reference = PackageManager.GetPackageCatalogByName(name);

        return reference is null ? null : ToPackageSource(reference.Info);
    }

    /// <inheritdoc />
    public Task<SourceOperationResult> AddSource(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => AddSourceCore(request, progress, cancellationToken), cancellationToken);

    private async Task<SourceOperationResult> AddSourceCore(AddPackageSourceRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var options = Factory.CreateAddPackageCatalogOptions();
        options.Name = request.Name;
        options.SourceUri = request.Uri;
        options.Type = request.Type;
        options.TrustLevel = request.TrustLevel == PackageSourceTrustLevel.Trusted
            ? PackageCatalogTrustLevel.Trusted
            : PackageCatalogTrustLevel.None;
        options.Explicit = request.IsExplicit;
        options.Priority = request.Priority;

        if (request.CustomHeader is not null)
        {
            options.CustomHeader = request.CustomHeader;
        }

        var operation = PackageManager.AddPackageCatalogAsync(options);

        if (progress is not null)
        {
            operation.Progress = (_, percent) => progress.Report(percent);
        }

        using var registration = _context.RegisterCancellation(cancellationToken, operation.Cancel);

        var result = await operation.AsTask();

        return result.Status == AddPackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    /// <inheritdoc />
    public Task<SourceOperationResult> RemoveSource(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => RemoveSourceCore(name, preserveData, progress, cancellationToken), cancellationToken);

    private async Task<SourceOperationResult> RemoveSourceCore(string name, bool preserveData, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var options = Factory.CreateRemovePackageCatalogOptions();
        options.Name = name;
        options.PreserveData = preserveData;

        var operation = PackageManager.RemovePackageCatalogAsync(options);

        if (progress is not null)
        {
            operation.Progress = (_, percent) => progress.Report(percent);
        }

        using var registration = _context.RegisterCancellation(cancellationToken, operation.Cancel);

        var result = await operation.AsTask();

        return result.Status == RemovePackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    /// <inheritdoc />
    public Task<SourceOperationResult> RefreshSource(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => RefreshSourceCore(name, progress, cancellationToken), cancellationToken);

    private async Task<SourceOperationResult> RefreshSourceCore(string name, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var reference = PackageManager.GetPackageCatalogByName(name);

        if (reference is null)
        {
            return SourceOperationResult.Failure($"WinGet source '{name}' is not configured.");
        }

        var operation = reference.RefreshPackageCatalogAsync();

        if (progress is not null)
        {
            operation.Progress = (_, percent) => progress.Report(percent);
        }

        using var registration = _context.RegisterCancellation(cancellationToken, operation.Cancel);

        var result = await operation.AsTask();

        return result.Status == RefreshPackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    /// <inheritdoc />
    public Task<SourceOperationResult> UpdateSource(string name, bool? isExplicit = null, int? priority = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => UpdateSourceCore(name, isExplicit, priority, cancellationToken), cancellationToken);

    private SourceOperationResult UpdateSourceCore(string name, bool? isExplicit, int? priority, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = Factory.CreateEditPackageCatalogOptions();
        options.Name = name;

        if (isExplicit.HasValue)
        {
            options.Explicit = isExplicit.Value;
        }

        if (priority.HasValue)
        {
            options.Priority = priority.Value;
        }

        var result = PackageManager.EditPackageCatalog(options);

        return result.Status == EditPackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    private static PackageSource ToPackageSource(PackageCatalogInfo info)
    {
        return new PackageSource(
            Id: info.Id,
            Name: info.Name,
            Type: info.Type,
            Argument: info.Argument,
            LastUpdated: ToNullableDate(info.LastUpdateTime),
            Origin: info.Origin switch
            {
                PackageCatalogOrigin.Predefined => PackageSourceOrigin.Predefined,
                PackageCatalogOrigin.User => PackageSourceOrigin.User,
                _ => PackageSourceOrigin.Unknown
            },
            TrustLevel: info.TrustLevel == PackageCatalogTrustLevel.Trusted
                ? PackageSourceTrustLevel.Trusted
                : PackageSourceTrustLevel.None,
            IsExplicit: info.Explicit,
            Priority: GetPriority(info));
    }

    private static int GetPriority(PackageCatalogInfo info)
    {
        try
        {
            // Priority is contract 29; guard against older WinGet runtimes.
            return info.Priority;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTimeOffset? ToNullableDate(DateTimeOffset value) =>
        value == default ? null : value;

    public void Dispose()
    {
        if (_ownsContext)
        {
            _context.Dispose();
        }
    }
}
