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
public sealed class WinGetSourceClient : IWinGetSourceClient
{
    private readonly WinGetFactory _factory;

    private readonly Lazy<PackageManager> _packageManager;

    public WinGetSourceClient()
        : this(new WinGetFactory())
    {
    }

    internal WinGetSourceClient(WinGetFactory factory)
    {
        _factory = factory;
        _packageManager = new Lazy<PackageManager>(factory.CreatePackageManager);
    }

    private PackageManager PackageManager => _packageManager.Value;

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run<IReadOnlyList<PackageSource>>(() =>
        {
            var catalogs = PackageManager.GetPackageCatalogs();
            var sources = new List<PackageSource>(catalogs.Count);

            // Do NOT convert to foreach/LINQ: the CsWinRT-projected list's enumerator throws
            // InvalidCastException ("No such interface supported") on interop 1.29.280.
            // Only indexer access (.Count / [i]) is reliable. Verified by the live
            // GetSourcesAsync integration test.
            for (var i = 0; i < catalogs.Count; i++)
            {
                sources.Add(ToPackageSource(catalogs[i].Info));
            }

            return sources;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageSource?> GetSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            var reference = PackageManager.GetPackageCatalogByName(name);

            return reference is null ? null : ToPackageSource(reference.Info);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> AddSourceAsync(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var options = _factory.CreateAddPackageCatalogOptions();
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

        await using var registration = cancellationToken.Register(() => operation.Cancel());

        var result = await operation;

        return result.Status == AddPackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> RemoveSourceAsync(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var options = _factory.CreateRemovePackageCatalogOptions();
        options.Name = name;
        options.PreserveData = preserveData;

        var operation = PackageManager.RemovePackageCatalogAsync(options);

        if (progress is not null)
        {
            operation.Progress = (_, percent) => progress.Report(percent);
        }

        await using var registration = cancellationToken.Register(() => operation.Cancel());

        var result = await operation;

        return result.Status == RemovePackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    /// <inheritdoc />
    public async Task<SourceOperationResult> RefreshSourceAsync(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
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

        await using var registration = cancellationToken.Register(() => operation.Cancel());

        var result = await operation;

        return result.Status == RefreshPackageCatalogStatus.Ok
            ? SourceOperationResult.Success()
            : SourceOperationResult.Failure(result.Status.ToString(), result.ExtendedErrorCode?.HResult);
    }

    /// <inheritdoc />
    public Task<SourceOperationResult> UpdateSourceAsync(string name, bool? isExplicit = null, int? priority = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            var options = _factory.CreateEditPackageCatalogOptions();
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
        }, cancellationToken);
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
}
