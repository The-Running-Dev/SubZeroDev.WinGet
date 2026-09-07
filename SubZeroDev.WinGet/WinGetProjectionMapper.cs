using Windows.System;

using Microsoft.Management.Deployment;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet;

internal static class WinGetProjectionMapper
{
    internal static PackageVersionId? FindVersionId(CatalogPackage package, string version)
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

    internal static PackageOperationResult ToOperationResult(PackageOperationStatus status, bool rebootRequired, Exception? extendedError, string statusDescription, uint? installerErrorCode)
    {
        if (status != PackageOperationStatus.Ok && extendedError?.HResult == WinGetErrorCodes.InstallCancelledByUser)
        {
            status = PackageOperationStatus.Cancelled;
        }

        return status == PackageOperationStatus.Ok
            ? PackageOperationResult.Success(rebootRequired)
            : PackageOperationResult.Failure(status, statusDescription, extendedError?.HResult, installerErrorCode);
    }

    internal static uint? GetInstallerErrorCode(InstallResult result) =>
        result.Status == InstallResultStatus.InstallError ? result.InstallerErrorCode : null;

    internal static PackageOperationStatus MapStatus(InstallResultStatus status) => status switch
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

    internal static PackageOperationStatus MapStatus(UninstallResultStatus status) => status switch
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

    internal static PackageOperationStatus MapStatus(DownloadResultStatus status) => status switch
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

    internal static PackageOperationStatus MapStatus(RepairResultStatus status) => status switch
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

    internal static PackageInstallScope MapScope(PackageScope scope) => scope switch
    {
        PackageScope.User => PackageInstallScope.User,
        PackageScope.System => PackageInstallScope.System,
        PackageScope.UserOrUnknown => PackageInstallScope.UserOrUnknown,
        PackageScope.SystemOrUnknown => PackageInstallScope.SystemOrUnknown,
        _ => PackageInstallScope.Any
    };

    internal static PackageInstallMode MapInstallMode(PackageOperationMode mode) => mode switch
    {
        PackageOperationMode.Silent => PackageInstallMode.Silent,
        PackageOperationMode.Interactive => PackageInstallMode.Interactive,
        _ => PackageInstallMode.Default
    };

    internal static PackageUninstallMode MapUninstallMode(PackageOperationMode mode) => mode switch
    {
        PackageOperationMode.Silent => PackageUninstallMode.Silent,
        PackageOperationMode.Interactive => PackageUninstallMode.Interactive,
        _ => PackageUninstallMode.Default
    };

    internal static PackageUninstallScope MapUninstallScope(PackageScope scope) => scope switch
    {
        PackageScope.User or PackageScope.UserOrUnknown => PackageUninstallScope.User,
        PackageScope.System or PackageScope.SystemOrUnknown => PackageUninstallScope.System,
        _ => PackageUninstallScope.Any
    };

    internal static PackageRepairMode MapRepairMode(PackageOperationMode mode) => mode switch
    {
        PackageOperationMode.Silent => PackageRepairMode.Silent,
        PackageOperationMode.Interactive => PackageRepairMode.Interactive,
        _ => PackageRepairMode.Default
    };

    internal static PackageRepairScope MapRepairScope(PackageScope scope) => scope switch
    {
        PackageScope.User or PackageScope.UserOrUnknown => PackageRepairScope.User,
        PackageScope.System or PackageScope.SystemOrUnknown => PackageRepairScope.System,
        _ => PackageRepairScope.Any
    };

    internal static ProcessorArchitecture MapArchitecture(PackageArchitecture architecture) => architecture switch
    {
        PackageArchitecture.X86 => ProcessorArchitecture.X86,
        PackageArchitecture.X64 => ProcessorArchitecture.X64,
        PackageArchitecture.Arm => ProcessorArchitecture.Arm,
        PackageArchitecture.Arm64 => ProcessorArchitecture.Arm64,
        _ => ProcessorArchitecture.Unknown
    };

    internal static List<string> CopyStrings(IReadOnlyList<string>? source)
    {
        var count = source?.Count ?? 0;
        var list = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            list.Add(source![i]);
        }

        return list;
    }

    internal static DateTimeOffset? ToNullableDate(DateTimeOffset value) =>
        value == default ? null : value;

    internal static PackageInstallerType MapInstallerType(PackageInstallerKind kind) => kind switch
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
    internal static List<PackageInfo> ToPackages(IReadOnlyList<MatchResult> matches)
    {
        var packages = new List<PackageInfo>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
        {
            packages.Add(ToPackageInfo(matches[i].CatalogPackage));
        }

        return packages;
    }

    internal static PackageInfo ToPackageInfo(CatalogPackage package)
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

    internal static PackageDetails ToPackageDetails(CatalogPackage package)
    {
        var installed = package.InstalledVersion;
        var latest = package.DefaultInstallVersion;
        var versionInfo = latest ?? installed;

        var metadata = versionInfo is null
            ? null
            : TryGetCatalogPackageMetadata(versionInfo.GetCatalogPackageMetadata);

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

    // Any exception here means this source did not provide manifest metadata (e.g. a bare ARP
    // entry), not a diagnosable environment fact — the recourse is identical (fall through with
    // nulls) regardless of cause. Cancellation is the one exception this must not treat as
    // "no metadata": it is a caller decision, not the source's, and must propagate.
    internal static CatalogPackageMetadata? TryGetCatalogPackageMetadata(Func<CatalogPackageMetadata?> getMetadata)
    {
        try
        {
            return getMetadata();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    internal static List<PackageAgreementInfo> CopyAgreements(IReadOnlyList<PackageAgreement>? source)
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

    internal static List<PackageDocumentation> CopyDocumentations(IReadOnlyList<Documentation>? source)
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

    internal static List<PackageIconInfo> CopyIcons(IReadOnlyList<Icon>? source)
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

    internal static PackageSource ToPackageSource(PackageCatalogInfo info)
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

    internal static int GetPriority(PackageCatalogInfo info)
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
}
