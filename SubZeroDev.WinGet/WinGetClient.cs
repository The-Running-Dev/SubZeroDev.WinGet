using Windows.Foundation;
using Windows.System;

using Microsoft.Management.Deployment;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Com;
using SubZeroDev.WinGet.Models;

using InstallRequest = SubZeroDev.WinGet.Models.InstallRequest;

namespace SubZeroDev.WinGet;

/// <summary>
/// Real implementation of <see cref="IWinGetClient"/> against the WinGet COM API
/// (Microsoft.Management.Deployment). No console output is parsed anywhere in this class.
/// All lookups go through composite catalogs (remote sources merged with local install state),
/// which is how winget itself correlates "installed" with "available" — see the E2E interop
/// tests in the winget-cli repo for the reference pattern.
/// </summary>
public sealed class WinGetClient : IWinGetClient, IDisposable
{
    private readonly WinGetComContext _context;
    private readonly bool _ownsContext;

    public WinGetClient()
        : this(new WinGetComContext(), ownsContext: true)
    {
    }

    internal WinGetClient(WinGetFactory factory)
        : this(new WinGetComContext(factory), ownsContext: true)
    {
    }

    internal WinGetClient(WinGetComContext context)
        : this(context, ownsContext: false)
    {
    }

    private WinGetClient(WinGetComContext context, bool ownsContext)
    {
        _context = context;
        _ownsContext = ownsContext;
    }

    private WinGetFactory Factory => _context.Factory;

    private PackageManager PackageManager => _context.PackageManager;

    /// <inheritdoc />
    public Task<string?> GetWinGetVersion(CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetWinGetVersionCore(cancellationToken), cancellationToken);

    private string? GetWinGetVersionCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return (string?)PackageManager.Version;
        }
        catch
        {
            // Version is contract 13; unavailable on very old WinGet runtimes.
            return null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageInfo>> Search(string query, int limit, string? sourceName = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => SearchCore(query, limit, sourceName, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<PackageInfo>> SearchCore(string query, int limit, string? sourceName, CancellationToken cancellationToken)
    {
        var catalog = await ConnectComposite(CompositeSearchBehavior.RemotePackagesFromAllCatalogs, sourceName, cancellationToken);

        var options = Factory.CreateFindPackagesOptions();
        options.ResultLimit = (uint)limit;

        // winget search matches on any of these fields. Selectors are OR'd by the COM API
        // (Filters are AND'd), so one call with four selectors reproduces the CLI behavior.
        foreach (var field in new[] { PackageMatchField.Id, PackageMatchField.Name, PackageMatchField.Moniker, PackageMatchField.Tag })
        {
            var filter = Factory.CreatePackageMatchFilter();
            filter.Field = field;
            filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
            filter.Value = query;

            options.Selectors.Add(filter);
        }

        var result = await FindPackagesAsync(catalog, options, cancellationToken);

        return ToPackages(result.Matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageInfo>> GetInstalledPackages(CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetInstalledPackagesCore(cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesCore(CancellationToken cancellationToken)
    {
        var catalog = await ConnectComposite(CompositeSearchBehavior.LocalCatalogs, sourceName: null, cancellationToken);

        // No selectors and no filters selects the entire catalog.
        var result = await FindPackagesAsync(catalog, Factory.CreateFindPackagesOptions(), cancellationToken);

        return ToPackages(result.Matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PackageInfo>> GetAvailableUpgrades(CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetAvailableUpgradesCore(cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<PackageInfo>> GetAvailableUpgradesCore(CancellationToken cancellationToken)
    {
        var installed = await GetInstalledPackagesCore(cancellationToken);

        return installed.Where(p => p.IsUpdateAvailable).ToList();
    }

    /// <inheritdoc />
    public Task<PackageInfo?> GetPackage(string packageId, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetPackageCore(packageId, cancellationToken), cancellationToken);

    private async Task<PackageInfo?> GetPackageCore(string packageId, CancellationToken cancellationToken)
    {
        var package = await FindById(packageId, cancellationToken);

        return package is null ? null : ToPackageInfo(package);
    }

    /// <inheritdoc />
    public Task<PackageDetails?> GetPackageDetails(string packageId, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => GetPackageDetailsCore(packageId, cancellationToken), cancellationToken);

    private async Task<PackageDetails?> GetPackageDetailsCore(string packageId, CancellationToken cancellationToken)
    {
        var package = await FindById(packageId, cancellationToken);

        return package is null ? null : ToPackageDetails(package);
    }

    /// <inheritdoc />
    public Task<PackageOperationResult> Install(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => InstallCore(packageId, request, progress, cancellationToken), cancellationToken);

    private async Task<PackageOperationResult> InstallCore(string packageId, InstallRequest? request, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        request ??= new InstallRequest();

        var package = await FindById(packageId, cancellationToken);

        if (package is null)
        {
            return NotFound(packageId);
        }

        var (options, error) = BuildInstallOptions(package, request);

        if (error is not null)
        {
            return error;
        }

        var operation = PackageManager.InstallPackageAsync(package, options);

        operation.Progress = (_, info) => ReportInstallProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(MapStatus(result.Status), result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString(), GetInstallerErrorCode(result));
    }

    /// <inheritdoc />
    public Task<PackageOperationResult> Upgrade(string packageId, InstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => UpgradeCore(packageId, request, progress, cancellationToken), cancellationToken);

    private async Task<PackageOperationResult> UpgradeCore(string packageId, InstallRequest? request, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        request ??= new InstallRequest();

        var package = await FindById(packageId, cancellationToken);

        if (package is null)
        {
            return NotFound(packageId);
        }

        var (options, error) = BuildInstallOptions(package, request);

        if (error is not null)
        {
            return error;
        }

        var operation = PackageManager.UpgradePackageAsync(package, options);

        operation.Progress = (_, info) => ReportInstallProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(MapStatus(result.Status), result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString(), GetInstallerErrorCode(result));
    }

    /// <inheritdoc />
    public Task<PackageOperationResult> Uninstall(string packageId, UninstallRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => UninstallCore(packageId, request, progress, cancellationToken), cancellationToken);

    private async Task<PackageOperationResult> UninstallCore(string packageId, UninstallRequest? request, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        request ??= new UninstallRequest();

        var package = await FindById(packageId, cancellationToken);

        if (package is null)
        {
            return NotFound(packageId);
        }

        var options = Factory.CreateUninstallOptions();
        options.PackageUninstallMode = MapUninstallMode(request.Mode);
        options.PackageUninstallScope = MapUninstallScope(request.Scope);
        options.Force = request.Force;

        if (request.LogOutputPath is not null)
        {
            options.LogOutputPath = request.LogOutputPath;
        }

        if (request.CorrelationData is not null)
        {
            options.CorrelationData = request.CorrelationData;
        }

        var operation = PackageManager.UninstallPackageAsync(package, options);

        operation.Progress = (_, info) => ReportUninstallProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(MapStatus(result.Status), result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString(), result.UninstallerErrorCode);
    }

    /// <inheritdoc />
    public Task<PackageOperationResult> Download(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => DownloadCore(packageId, request, progress, cancellationToken), cancellationToken);

    private async Task<PackageOperationResult> DownloadCore(string packageId, DownloadRequest request, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        var package = await FindById(packageId, cancellationToken);

        if (package is null)
        {
            return NotFound(packageId);
        }

        var options = Factory.CreateDownloadOptions();
        options.DownloadDirectory = request.DownloadDirectory;
        options.Scope = MapScope(request.Scope);
        options.AllowHashMismatch = request.AllowHashMismatch;
        options.SkipDependencies = request.SkipDependencies;
        options.SkipMicrosoftStoreLicense = request.SkipMicrosoftStoreLicense;
        options.AcceptPackageAgreements = request.AcceptPackageAgreements;

        if (request.Architecture != PackageArchitecture.Default)
        {
            options.Architecture = MapArchitecture(request.Architecture);
        }

        if (request.InstallerType != PackageInstallerKind.Default)
        {
            options.InstallerType = MapInstallerType(request.InstallerType);
        }

        if (request.Locale is not null)
        {
            options.Locale = request.Locale;
        }

        if (request.CorrelationData is not null)
        {
            options.CorrelationData = request.CorrelationData;
        }

        if (request.Version is not null)
        {
            var versionId = FindVersionId(package, request.Version);

            if (versionId is null)
            {
                return VersionNotFound(packageId, request.Version);
            }

            options.PackageVersionId = versionId;
        }

        var operation = PackageManager.DownloadPackageAsync(package, options);

        operation.Progress = (_, info) => ReportDownloadProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(MapStatus(result.Status), rebootRequired: false, result.ExtendedErrorCode, result.Status.ToString(), installerErrorCode: null);
    }

    /// <inheritdoc />
    public Task<PackageOperationResult> Repair(string packageId, RepairRequest? request = null, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
        => _context.InvokeAsync(() => RepairCore(packageId, request, progress, cancellationToken), cancellationToken);

    private async Task<PackageOperationResult> RepairCore(string packageId, RepairRequest? request, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken)
    {
        request ??= new RepairRequest();

        var package = await FindById(packageId, cancellationToken);

        if (package is null)
        {
            return NotFound(packageId);
        }

        var options = Factory.CreateRepairOptions();
        options.PackageRepairMode = MapRepairMode(request.Mode);
        options.PackageRepairScope = MapRepairScope(request.Scope);
        options.Force = request.Force;
        options.AllowHashMismatch = request.AllowHashMismatch;
        options.AcceptPackageAgreements = request.AcceptPackageAgreements;

        if (request.LogOutputPath is not null)
        {
            options.LogOutputPath = request.LogOutputPath;
        }

        if (request.CorrelationData is not null)
        {
            options.CorrelationData = request.CorrelationData;
        }

        var operation = PackageManager.RepairPackageAsync(package, options);

        operation.Progress = (_, info) => ReportRepairProgress(progress, info);

        var result = await AwaitOperation(operation, cancellationToken);

        return ToOperationResult(MapStatus(result.Status), result.RebootRequired, result.ExtendedErrorCode, result.Status.ToString(), result.RepairerErrorCode);
    }

    private async Task<CatalogPackage?> FindById(string packageId, CancellationToken cancellationToken)
    {
        // AllCatalogs merges installed state with every remote source, so the returned
        // CatalogPackage is the exact instance WinGet associates with the installed app —
        // required for upgrade/uninstall/repair to resolve correctly.
        var catalog = await ConnectComposite(CompositeSearchBehavior.AllCatalogs, sourceName: null, cancellationToken);

        var options = Factory.CreateFindPackagesOptions();
        options.ResultLimit = 1;

        var filter = Factory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
        filter.Value = packageId;

        options.Selectors.Add(filter);

        var result = await FindPackagesAsync(catalog, options, cancellationToken);

        return result.Matches.Count > 0 ? result.Matches[0].CatalogPackage : null;
    }

    private (InstallOptions Options, PackageOperationResult? Error) BuildInstallOptions(CatalogPackage package, InstallRequest request)
    {
        var options = Factory.CreateInstallOptions();
        options.PackageInstallScope = MapScope(request.Scope);
        options.PackageInstallMode = MapInstallMode(request.Mode);
        options.Force = request.Force;
        options.AllowHashMismatch = request.AllowHashMismatch;
        options.SkipDependencies = request.SkipDependencies;
        options.AllowUpgradeToUnknownVersion = request.AllowUpgradeToUnknownVersion;
        options.AcceptPackageAgreements = request.AcceptPackageAgreements;

        if (request.PreferredInstallLocation is not null)
        {
            options.PreferredInstallLocation = request.PreferredInstallLocation;
        }

        if (request.LogOutputPath is not null)
        {
            options.LogOutputPath = request.LogOutputPath;
        }

        if (request.OverrideArguments is not null)
        {
            options.ReplacementInstallerArguments = request.OverrideArguments;
        }

        if (request.AdditionalArguments is not null)
        {
            options.AdditionalInstallerArguments = request.AdditionalArguments;
        }

        if (request.CorrelationData is not null)
        {
            options.CorrelationData = request.CorrelationData;
        }

        if (request.InstallerType != PackageInstallerKind.Default)
        {
            options.InstallerType = MapInstallerType(request.InstallerType);
        }

        if (request.Architecture != PackageArchitecture.Default)
        {
            options.AllowedArchitectures.Clear();
            options.AllowedArchitectures.Add(MapArchitecture(request.Architecture));
        }

        if (request.Version is not null)
        {
            var versionId = FindVersionId(package, request.Version);

            if (versionId is null)
            {
                return (options, VersionNotFound(package.Id, request.Version));
            }

            options.PackageVersionId = versionId;
        }

        return (options, null);
    }

    private async Task<PackageCatalog> ConnectComposite(CompositeSearchBehavior behavior, string? sourceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var references = GetRemoteCatalogReferences(sourceName);

        var operation = CreateComposite(references, behavior).ConnectAsync();
        using var registration = _context.RegisterCancellation(cancellationToken, operation.Cancel);
        var connectResult = await operation.AsTask();

        // A single unreachable source fails the whole composite connect. Probe each source
        // individually and rebuild from the reachable subset before giving up (the same
        // resilience pattern UniGetUI uses for its installed/updates views).
        if (connectResult.Status != ConnectResultStatus.Ok && references.Count > 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reachable = new List<PackageCatalogReference>();

            foreach (var reference in references)
            {
                try
                {
                    if (reference.Connect().Status == ConnectResultStatus.Ok)
                    {
                        reachable.Add(reference);
                    }
                }
                catch
                {
                    // Unreachable source; skip it.
                }
            }

            if (reachable.Count > 0)
            {
                operation = CreateComposite(reachable, behavior).ConnectAsync();
                using var retryRegistration = _context.RegisterCancellation(cancellationToken, operation.Cancel);
                connectResult = await operation.AsTask();
            }
        }

        if (connectResult.Status != ConnectResultStatus.Ok || connectResult.PackageCatalog is null)
        {
            throw new InvalidOperationException($"Failed to connect to WinGet catalog: {connectResult.Status}.");
        }

        return connectResult.PackageCatalog;
    }

    private List<PackageCatalogReference> GetRemoteCatalogReferences(string? sourceName)
    {
        if (sourceName is not null)
        {
            var named = PackageManager.GetPackageCatalogByName(sourceName)
                ?? throw new InvalidOperationException($"WinGet source '{sourceName}' is not configured.");

            return [named];
        }

        var catalogs = PackageManager.GetPackageCatalogs();
        var references = new List<PackageCatalogReference>(catalogs.Count);

        for (var i = 0; i < catalogs.Count; i++)
        {
            references.Add(catalogs[i]);
        }

        return references;
    }

    private PackageCatalogReference CreateComposite(IReadOnlyList<PackageCatalogReference> references, CompositeSearchBehavior behavior)
    {
        var options = Factory.CreateCompositeCatalogOptions();
        options.CompositeSearchBehavior = behavior;

        for (var i = 0; i < references.Count; i++)
        {
            options.Catalogs.Add(references[i]);
        }

        return PackageManager.CreateCompositePackageCatalog(options);
    }

    private async Task<FindPackagesResult> FindPackagesAsync(PackageCatalog catalog, FindPackagesOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var operation = catalog.FindPackagesAsync(options);
        using var registration = _context.RegisterCancellation(cancellationToken, operation.Cancel);
        var result = await operation.AsTask();

        if (result.Status != FindPackagesResultStatus.Ok)
        {
            throw new InvalidOperationException($"WinGet package search failed: {result.Status}.");
        }

        return result;
    }

    private static PackageVersionId? FindVersionId(CatalogPackage package, string version)
    {
        var versions = package.AvailableVersions;

        for (var i = 0; i < versions.Count; i++)
        {
            if (string.Equals(versions[i].Version, version, StringComparison.OrdinalIgnoreCase))
            {
                return versions[i];
            }
        }

        return null;
    }

    private static PackageOperationResult NotFound(string packageId) =>
        PackageOperationResult.Failure(PackageOperationStatus.PackageNotFound, $"Package '{packageId}' was not found in any configured source.");

    private static PackageOperationResult VersionNotFound(string packageId, string version) =>
        PackageOperationResult.Failure(PackageOperationStatus.InvalidOptions, $"Version '{version}' of package '{packageId}' was not found in any configured source.");

    private static void ReportInstallProgress(IProgress<PackageOperationProgress>? progress, InstallProgress info)
    {
        if (progress is null)
        {
            return;
        }

        // Each operation's progress payload is a distinct WinRT struct with public fields, so
        // each gets its own mapper rather than a shared one.
        var state = info.State switch
        {
            PackageInstallProgressState.Queued => PackageOperationState.Queued,
            PackageInstallProgressState.Downloading => PackageOperationState.Downloading,
            PackageInstallProgressState.Installing => PackageOperationState.Installing,
            PackageInstallProgressState.PostInstall => PackageOperationState.PostOperation,
            PackageInstallProgressState.Finished => PackageOperationState.Completed,
            _ => PackageOperationState.Queued
        };

        var percent = state == PackageOperationState.Downloading ? info.DownloadProgress * 100 : info.InstallationProgress * 100;

        progress.Report(new PackageOperationProgress(state, percent, state.ToString(), info.BytesDownloaded, info.BytesRequired));
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
            PackageUninstallProgressState.Uninstalling => PackageOperationState.Uninstalling,
            PackageUninstallProgressState.PostUninstall => PackageOperationState.PostOperation,
            PackageUninstallProgressState.Finished => PackageOperationState.Completed,
            _ => PackageOperationState.Queued
        };

        progress.Report(new PackageOperationProgress(state, info.UninstallationProgress * 100, state.ToString()));
    }

    private static void ReportDownloadProgress(IProgress<PackageOperationProgress>? progress, PackageDownloadProgress info)
    {
        if (progress is null)
        {
            return;
        }

        var state = info.State switch
        {
            PackageDownloadProgressState.Queued => PackageOperationState.Queued,
            PackageDownloadProgressState.Downloading => PackageOperationState.Downloading,
            PackageDownloadProgressState.Finished => PackageOperationState.Completed,
            _ => PackageOperationState.Queued
        };

        progress.Report(new PackageOperationProgress(state, info.DownloadProgress * 100, state.ToString(), info.BytesDownloaded, info.BytesRequired));
    }

    private static void ReportRepairProgress(IProgress<PackageOperationProgress>? progress, RepairProgress info)
    {
        if (progress is null)
        {
            return;
        }

        var state = info.State switch
        {
            PackageRepairProgressState.Queued => PackageOperationState.Queued,
            PackageRepairProgressState.Repairing => PackageOperationState.Repairing,
            PackageRepairProgressState.PostRepair => PackageOperationState.PostOperation,
            PackageRepairProgressState.Finished => PackageOperationState.Completed,
            _ => PackageOperationState.Queued
        };

        progress.Report(new PackageOperationProgress(state, info.RepairCompletionProgress * 100, state.ToString()));
    }

    private static PackageOperationResult ToOperationResult(PackageOperationStatus status, bool rebootRequired, Exception? extendedError, string statusDescription, uint? installerErrorCode)
    {
        return status == PackageOperationStatus.Ok
            ? PackageOperationResult.Success(rebootRequired)
            : PackageOperationResult.Failure(status, statusDescription, extendedError?.HResult, installerErrorCode);
    }

    private static uint? GetInstallerErrorCode(InstallResult result)
    {
        return result.Status == InstallResultStatus.InstallError ? result.InstallerErrorCode : null;
    }

    private static PackageOperationStatus MapStatus(InstallResultStatus status) => status switch
    {
        InstallResultStatus.Ok => PackageOperationStatus.Ok,
        InstallResultStatus.BlockedByPolicy => PackageOperationStatus.BlockedByPolicy,
        InstallResultStatus.CatalogError => PackageOperationStatus.CatalogError,
        InstallResultStatus.InternalError => PackageOperationStatus.InternalError,
        InstallResultStatus.InvalidOptions => PackageOperationStatus.InvalidOptions,
        InstallResultStatus.DownloadError => PackageOperationStatus.DownloadError,
        InstallResultStatus.InstallError => PackageOperationStatus.InstallError,
        InstallResultStatus.ManifestError => PackageOperationStatus.ManifestError,
        InstallResultStatus.NoApplicableInstallers => PackageOperationStatus.NoApplicableInstallers,
        InstallResultStatus.NoApplicableUpgrade => PackageOperationStatus.NoApplicableUpgrade,
        InstallResultStatus.PackageAgreementsNotAccepted => PackageOperationStatus.PackageAgreementsNotAccepted,
        _ => PackageOperationStatus.Unknown
    };

    private static PackageOperationStatus MapStatus(UninstallResultStatus status) => status switch
    {
        UninstallResultStatus.Ok => PackageOperationStatus.Ok,
        UninstallResultStatus.BlockedByPolicy => PackageOperationStatus.BlockedByPolicy,
        UninstallResultStatus.CatalogError => PackageOperationStatus.CatalogError,
        UninstallResultStatus.InternalError => PackageOperationStatus.InternalError,
        UninstallResultStatus.InvalidOptions => PackageOperationStatus.InvalidOptions,
        UninstallResultStatus.UninstallError => PackageOperationStatus.UninstallError,
        UninstallResultStatus.ManifestError => PackageOperationStatus.ManifestError,
        _ => PackageOperationStatus.Unknown
    };

    private static PackageOperationStatus MapStatus(DownloadResultStatus status) => status switch
    {
        DownloadResultStatus.Ok => PackageOperationStatus.Ok,
        DownloadResultStatus.BlockedByPolicy => PackageOperationStatus.BlockedByPolicy,
        DownloadResultStatus.CatalogError => PackageOperationStatus.CatalogError,
        DownloadResultStatus.InternalError => PackageOperationStatus.InternalError,
        DownloadResultStatus.InvalidOptions => PackageOperationStatus.InvalidOptions,
        DownloadResultStatus.DownloadError => PackageOperationStatus.DownloadError,
        DownloadResultStatus.ManifestError => PackageOperationStatus.ManifestError,
        DownloadResultStatus.NoApplicableInstallers => PackageOperationStatus.NoApplicableInstallers,
        DownloadResultStatus.PackageAgreementsNotAccepted => PackageOperationStatus.PackageAgreementsNotAccepted,
        _ => PackageOperationStatus.Unknown
    };

    private static PackageOperationStatus MapStatus(RepairResultStatus status) => status switch
    {
        RepairResultStatus.Ok => PackageOperationStatus.Ok,
        RepairResultStatus.BlockedByPolicy => PackageOperationStatus.BlockedByPolicy,
        RepairResultStatus.CatalogError => PackageOperationStatus.CatalogError,
        RepairResultStatus.DownloadError => PackageOperationStatus.DownloadError,
        RepairResultStatus.InternalError => PackageOperationStatus.InternalError,
        RepairResultStatus.InvalidOptions => PackageOperationStatus.InvalidOptions,
        RepairResultStatus.RepairError => PackageOperationStatus.RepairError,
        RepairResultStatus.ManifestError => PackageOperationStatus.ManifestError,
        RepairResultStatus.NoApplicableRepairer => PackageOperationStatus.NoApplicableRepairer,
        RepairResultStatus.PackageAgreementsNotAccepted => PackageOperationStatus.PackageAgreementsNotAccepted,
        _ => PackageOperationStatus.Unknown
    };

    private static PackageInstallScope MapScope(PackageScope scope) => scope switch
    {
        PackageScope.User => PackageInstallScope.User,
        PackageScope.System => PackageInstallScope.System,
        PackageScope.UserOrUnknown => PackageInstallScope.UserOrUnknown,
        PackageScope.SystemOrUnknown => PackageInstallScope.SystemOrUnknown,
        _ => PackageInstallScope.Any
    };

    private static PackageInstallMode MapInstallMode(PackageOperationMode mode) => mode switch
    {
        PackageOperationMode.Silent => PackageInstallMode.Silent,
        PackageOperationMode.Interactive => PackageInstallMode.Interactive,
        _ => PackageInstallMode.Default
    };

    private static PackageUninstallMode MapUninstallMode(PackageOperationMode mode) => mode switch
    {
        PackageOperationMode.Silent => PackageUninstallMode.Silent,
        PackageOperationMode.Interactive => PackageUninstallMode.Interactive,
        _ => PackageUninstallMode.Default
    };

    private static PackageUninstallScope MapUninstallScope(PackageScope scope) => scope switch
    {
        PackageScope.User or PackageScope.UserOrUnknown => PackageUninstallScope.User,
        PackageScope.System or PackageScope.SystemOrUnknown => PackageUninstallScope.System,
        _ => PackageUninstallScope.Any
    };

    private static PackageRepairMode MapRepairMode(PackageOperationMode mode) => mode switch
    {
        PackageOperationMode.Silent => PackageRepairMode.Silent,
        PackageOperationMode.Interactive => PackageRepairMode.Interactive,
        _ => PackageRepairMode.Default
    };

    private static PackageRepairScope MapRepairScope(PackageScope scope) => scope switch
    {
        PackageScope.User or PackageScope.UserOrUnknown => PackageRepairScope.User,
        PackageScope.System or PackageScope.SystemOrUnknown => PackageRepairScope.System,
        _ => PackageRepairScope.Any
    };

    private static ProcessorArchitecture MapArchitecture(PackageArchitecture architecture) => architecture switch
    {
        PackageArchitecture.X86 => ProcessorArchitecture.X86,
        PackageArchitecture.X64 => ProcessorArchitecture.X64,
        PackageArchitecture.Arm => ProcessorArchitecture.Arm,
        PackageArchitecture.Arm64 => ProcessorArchitecture.Arm64,
        _ => ProcessorArchitecture.Unknown
    };

    private static PackageInstallerType MapInstallerType(PackageInstallerKind kind) => kind switch
    {
        PackageInstallerKind.Inno => PackageInstallerType.Inno,
        PackageInstallerKind.Wix => PackageInstallerType.Wix,
        PackageInstallerKind.Msi => PackageInstallerType.Msi,
        PackageInstallerKind.Nullsoft => PackageInstallerType.Nullsoft,
        PackageInstallerKind.Zip => PackageInstallerType.Zip,
        PackageInstallerKind.Msix => PackageInstallerType.Msix,
        PackageInstallerKind.Exe => PackageInstallerType.Exe,
        PackageInstallerKind.Burn => PackageInstallerType.Burn,
        PackageInstallerKind.MSStore => PackageInstallerType.MSStore,
        PackageInstallerKind.Portable => PackageInstallerType.Portable,
        PackageInstallerKind.Font => PackageInstallerType.Font,
        _ => PackageInstallerType.Unknown
    };

    // IReadOnlyList<T>'s CsWinRT-projected enumerator throws InvalidCastException ("No such
    // interface supported") when walked via foreach/LINQ (verified empirically against WinGet
    // COM interop 1.29.280) — indexer-based access is the reliable path, so every call site
    // funnels through indexed for loops rather than enumerating WinRT collections directly.
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

    private static PackageDetails ToPackageDetails(CatalogPackage package)
    {
        var installed = package.InstalledVersion;
        var latest = package.DefaultInstallVersion;
        var versionInfo = latest ?? installed;

        CatalogPackageMetadata? metadata = null;

        try
        {
            metadata = versionInfo?.GetCatalogPackageMetadata();
        }
        catch
        {
            // Not all sources provide manifest metadata (e.g. bare ARP entries); fall through
            // with nulls rather than failing the whole details lookup.
        }

        var availableVersions = new List<string>();
        var versionIds = package.AvailableVersions;

        for (var i = 0; i < versionIds.Count; i++)
        {
            var version = versionIds[i].Version;

            if (!string.IsNullOrEmpty(version) && !availableVersions.Contains(version))
            {
                availableVersions.Add(version);
            }
        }

        return new PackageDetails(
            Id: package.Id,
            Name: package.Name,
            Publisher: metadata?.Publisher ?? versionInfo?.Publisher,
            PublisherUrl: metadata?.PublisherUrl,
            PublisherSupportUrl: metadata?.PublisherSupportUrl,
            Author: metadata?.Author,
            ShortDescription: metadata?.ShortDescription,
            Description: metadata?.Description,
            PackageUrl: metadata?.PackageUrl,
            License: metadata?.License,
            LicenseUrl: metadata?.LicenseUrl,
            Copyright: metadata?.Copyright,
            CopyrightUrl: metadata?.CopyrightUrl,
            PrivacyUrl: metadata?.PrivacyUrl,
            ReleaseNotes: metadata?.ReleaseNotes,
            ReleaseNotesUrl: metadata?.ReleaseNotesUrl,
            InstallationNotes: metadata?.InstallationNotes,
            PurchaseUrl: metadata?.PurchaseUrl,
            Tags: CopyStrings(metadata?.Tags),
            Agreements: CopyAgreements(metadata?.Agreements),
            Documentations: CopyDocumentations(metadata?.Documentations),
            Icons: CopyIcons(metadata?.Icons),
            InstalledVersion: installed?.Version,
            AvailableVersion: latest?.Version,
            AvailableVersions: availableVersions,
            IsInstalled: installed is not null,
            IsUpdateAvailable: package.IsUpdateAvailable,
            Source: latest?.PackageCatalog?.Info?.Name ?? installed?.PackageCatalog?.Info?.Name ?? "winget");
    }

    private static List<string> CopyStrings(IReadOnlyList<string>? source)
    {
        var count = source?.Count ?? 0;
        var list = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            list.Add(source![i]);
        }

        return list;
    }

    private static List<PackageAgreementInfo> CopyAgreements(IReadOnlyList<PackageAgreement>? source)
    {
        var count = source?.Count ?? 0;
        var list = new List<PackageAgreementInfo>(count);

        for (var i = 0; i < count; i++)
        {
            var item = source![i];
            list.Add(new PackageAgreementInfo(item.Label, item.Text, item.Url));
        }

        return list;
    }

    private static List<PackageDocumentation> CopyDocumentations(IReadOnlyList<Documentation>? source)
    {
        var count = source?.Count ?? 0;
        var list = new List<PackageDocumentation>(count);

        for (var i = 0; i < count; i++)
        {
            var item = source![i];
            list.Add(new PackageDocumentation(item.DocumentLabel, item.DocumentUrl));
        }

        return list;
    }

    private static List<PackageIconInfo> CopyIcons(IReadOnlyList<Icon>? source)
    {
        var count = source?.Count ?? 0;
        var list = new List<PackageIconInfo>(count);

        for (var i = 0; i < count; i++)
        {
            var item = source![i];
            list.Add(new PackageIconInfo(item.Url, item.FileType.ToString(), item.Resolution.ToString(), item.Theme.ToString()));
        }

        return list;
    }

    private async Task<TResult> AwaitOperation<TResult, TProgress>(
        IAsyncOperationWithProgress<TResult, TProgress> operation,
        CancellationToken cancellationToken)
    {
        using var registration = _context.RegisterCancellation(cancellationToken, operation.Cancel);

        return await operation.AsTask();
    }

    public void Dispose()
    {
        if (_ownsContext)
        {
            _context.Dispose();
        }
    }
}
