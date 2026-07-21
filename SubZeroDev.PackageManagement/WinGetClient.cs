using Windows.Foundation;

using Microsoft.Management.Deployment;

using SubZeroDev.PackageManagement.Abstractions;
using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement;

/// <summary>
/// Real implementation of <see cref="IWinGetClient"/> against the in-process WinGet COM API
/// (Microsoft.Management.Deployment). No console output is parsed anywhere in this class.
/// </summary>
public sealed class WinGetClient : IWinGetClient
{
    private readonly PackageManager _packageManager = new();

    // winget search matches by name, moniker, id, or tag by default. The COM API's FindPackages
    // ANDs every filter added to a single FindPackagesOptions together (verified empirically —
    // a single call with one filter per field returned zero results for a query that legitimately
    // matches by name alone), so reproducing "match any of these fields" means issuing one query
    // per field and merging/de-duplicating the results in-process instead.
    private static readonly PackageMatchField[] SearchFields =
    {
        PackageMatchField.Name,
        PackageMatchField.Moniker,
        PackageMatchField.Id,
        PackageMatchField.Tag
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var catalog = await ConnectAsync(GetCatalogReference(), cancellationToken);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<PackageInfo>();

        foreach (var field in SearchFields)
        {
            var options = new FindPackagesOptions { ResultLimit = (uint)limit };

            options.Filters.Add(new PackageMatchFilter
            {
                Field = field,
                Option = PackageFieldMatchOption.ContainsCaseInsensitive,
                Value = query
            });

            var findResult = catalog.FindPackages(options);

            foreach (var package in ToPackages(findResult.Matches))
            {
                if (seen.Add(package.Id) && results.Count < limit)
                {
                    results.Add(package);
                }
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken)
    {
        var catalog = await ConnectAsync(GetInstalledCatalogReference(), cancellationToken);

        var findResult = catalog.FindPackages(new FindPackagesOptions());

        return ToPackages(findResult.Matches);
    }

    /// <inheritdoc />
    public async Task<PackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken)
    {
        var package = await FindByIdAsync(packageId, cancellationToken);

        return package is null ? null : ToPackageInfo(package);
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> InstallAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        var package = await FindByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package '{packageId}' was not found in any configured source.");

        var options = new InstallOptions
        {
            PackageInstallScope = PackageInstallScope.Any,
            AcceptPackageAgreements = true
        };

        var operation = _packageManager.InstallPackageAsync(package, options);

        operation.Progress = (_, info) => ReportInstallProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(result.Status == InstallResultStatus.Ok, result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString());
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> UpgradeAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        var package = await FindByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package '{packageId}' was not found in any configured source.");

        var options = new InstallOptions
        {
            PackageInstallScope = PackageInstallScope.Any,
            AcceptPackageAgreements = true
        };

        var operation = _packageManager.UpgradePackageAsync(package, options);

        operation.Progress = (_, info) => ReportInstallProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(result.Status == InstallResultStatus.Ok, result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString());
    }

    /// <inheritdoc />
    public async Task<PackageOperationResult> UninstallAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        var package = await FindByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package '{packageId}' was not found in any configured source.");

        var options = new UninstallOptions();

        var operation = _packageManager.UninstallPackageAsync(package, options);

        operation.Progress = (_, info) => ReportUninstallProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(result.Status == UninstallResultStatus.Ok, result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString());
    }

    private async Task<CatalogPackage?> FindByIdAsync(string packageId, CancellationToken cancellationToken)
    {
        // Installed packages take priority so upgrade/uninstall operate on the exact
        // catalog-package instance WinGet already associates with the installed app.
        var installedCatalog = await ConnectAsync(GetInstalledCatalogReference(), cancellationToken);
        var installedMatches = installedCatalog.FindPackages(IdFilter(packageId)).Matches;

        if (installedMatches.Count > 0)
        {
            return installedMatches[0].CatalogPackage;
        }

        var catalog = await ConnectAsync(GetCatalogReference(), cancellationToken);
        var matches = catalog.FindPackages(IdFilter(packageId)).Matches;

        return matches.Count > 0 ? matches[0].CatalogPackage : null;
    }

    private static FindPackagesOptions IdFilter(string packageId)
    {
        var options = new FindPackagesOptions
        {
            ResultLimit = 1
        };

        options.Filters.Add(new PackageMatchFilter
        {
            Field = PackageMatchField.Id,
            Option = PackageFieldMatchOption.Equals,
            Value = packageId
        });

        return options;
    }

    private PackageCatalogReference GetCatalogReference() =>
        _packageManager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);

    private PackageCatalogReference GetInstalledCatalogReference() =>
        _packageManager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);

    private static async Task<PackageCatalog> ConnectAsync(PackageCatalogReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connectResult = await reference.ConnectAsync();

        if (connectResult.Status != ConnectResultStatus.Ok || connectResult.PackageCatalog is null)
        {
            throw new InvalidOperationException($"Failed to connect to WinGet catalog: {connectResult.Status}.");
        }

        return connectResult.PackageCatalog;
    }

    private static void ReportInstallProgress(IProgress<PackageOperationProgress>? progress, InstallProgress info)
    {
        if (progress is null)
        {
            return;
        }

        // Both InstallProgress and UninstallProgress are plain WinRT structs (public fields,
        // not properties) with different, non-overlapping shapes, so each gets its own mapper
        // rather than a shared one.
        var state = info.State switch
        {
            PackageInstallProgressState.Queued => PackageOperationState.Queued,
            PackageInstallProgressState.Downloading => PackageOperationState.Downloading,
            PackageInstallProgressState.Installing => PackageOperationState.Installing,
            PackageInstallProgressState.PostInstall => PackageOperationState.PostInstall,
            PackageInstallProgressState.Finished => PackageOperationState.Completed,
            _ => PackageOperationState.Queued
        };

        var percent = state == PackageOperationState.Downloading ? info.DownloadProgress * 100 : info.InstallationProgress * 100;

        progress.Report(new PackageOperationProgress(state, percent, state.ToString()));
    }

    private static void ReportUninstallProgress(IProgress<PackageOperationProgress>? progress, UninstallProgress info)
    {
        if (progress is null)
        {
            return;
        }

        var state = info.State switch
        {
            PackageUninstallProgressState.Queued => PackageOperationState.Queued,
            PackageUninstallProgressState.Uninstalling => PackageOperationState.Installing,
            PackageUninstallProgressState.PostUninstall => PackageOperationState.PostInstall,
            PackageUninstallProgressState.Finished => PackageOperationState.Completed,
            _ => PackageOperationState.Queued
        };

        progress.Report(new PackageOperationProgress(state, info.UninstallationProgress * 100, state.ToString()));
    }

    private static PackageOperationResult ToOperationResult(bool succeeded, bool rebootRequired, Exception? extendedError, string statusDescription)
    {
        return succeeded
            ? PackageOperationResult.Success(rebootRequired)
            : PackageOperationResult.Failure(statusDescription, extendedError?.HResult);
    }

    // IReadOnlyList<MatchResult>'s CsWinRT-projected enumerator throws InvalidCastException
    // ("No such interface supported") when walked via foreach/LINQ (verified empirically against
    // WinGet COM interop 1.29.280) — indexer-based access is the reliable path, so every call
    // site funnels through this helper rather than enumerating the WinRT collection directly.
    private static List<PackageInfo> ToPackages(IReadOnlyList<MatchResult> matches)
    {
        var packages = new List<PackageInfo>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
        {
            packages.Add(ToPackageInfo(matches[i].CatalogPackage));
        }

        return packages;
    }

    private static PackageInfo ToPackageInfo(CatalogPackage package)
    {
        var installed = package.InstalledVersion;
        var latest = package.DefaultInstallVersion;

        return new PackageInfo(
            Id: package.Id,
            Name: package.Name,
            Publisher: latest?.Publisher ?? installed?.Publisher,
            InstalledVersion: installed?.Version,
            AvailableVersion: latest?.Version,
            IsInstalled: installed is not null,
            IsUpdateAvailable: package.IsUpdateAvailable,
            Source: latest?.PackageCatalog?.Info?.Name ?? installed?.PackageCatalog?.Info?.Name ?? "winget");
    }

    private static async Task<TResult> AwaitOperation<TResult, TProgress>(
        IAsyncOperationWithProgress<TResult, TProgress> operation,
        CancellationToken cancellationToken)
    {
        await using var registration = cancellationToken.Register(() => operation.Cancel());

        return await operation;
    }
}
