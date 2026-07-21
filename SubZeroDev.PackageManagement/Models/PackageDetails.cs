namespace SubZeroDev.PackageManagement.Models;

/// <summary>A package agreement (EULA, terms of transaction, etc.) from the catalog manifest.</summary>
public sealed record PackageAgreementInfo(string? Label, string? Text, string? Url);

/// <summary>A documentation link from the catalog manifest.</summary>
public sealed record PackageDocumentation(string? Label, string? Url);

/// <summary>An icon reference from the catalog manifest.</summary>
public sealed record PackageIconInfo(string? Url, string? FileType, string? Resolution, string? Theme);

/// <summary>
/// Rich package metadata, combining catalog manifest localization data
/// (Microsoft.Management.Deployment.CatalogPackageMetadata) with install state.
/// Any field the manifest does not provide is null/empty.
/// </summary>
public sealed record PackageDetails(
    string Id,
    string Name,
    string? Publisher,
    string? PublisherUrl,
    string? PublisherSupportUrl,
    string? Author,
    string? ShortDescription,
    string? Description,
    string? PackageUrl,
    string? License,
    string? LicenseUrl,
    string? Copyright,
    string? CopyrightUrl,
    string? PrivacyUrl,
    string? ReleaseNotes,
    string? ReleaseNotesUrl,
    string? InstallationNotes,
    string? PurchaseUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PackageAgreementInfo> Agreements,
    IReadOnlyList<PackageDocumentation> Documentations,
    IReadOnlyList<PackageIconInfo> Icons,
    string? InstalledVersion,
    string? AvailableVersion,
    IReadOnlyList<string> AvailableVersions,
    bool IsInstalled,
    bool IsUpdateAvailable,
    string Source);
