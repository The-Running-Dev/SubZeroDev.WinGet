namespace SubZeroDev.PackageManagement.Models;

/// <summary>
/// Options for installing (or upgrading) a package. All properties default to WinGet's own defaults;
/// an empty request reproduces a plain "winget install &lt;id&gt;".
/// </summary>
public sealed record InstallRequest
{
    /// <summary>Specific version to install. Null installs the default (latest applicable) version.</summary>
    public string? Version { get; init; }

    public PackageScope Scope { get; init; } = PackageScope.Any;

    public PackageOperationMode Mode { get; init; } = PackageOperationMode.Silent;

    public PackageArchitecture Architecture { get; init; } = PackageArchitecture.Default;

    public PackageInstallerKind InstallerType { get; init; } = PackageInstallerKind.Default;

    public string? PreferredInstallLocation { get; init; }

    public string? LogOutputPath { get; init; }

    /// <summary>Replaces the installer's arguments entirely (maps to winget's --override).</summary>
    public string? OverrideArguments { get; init; }

    /// <summary>Appended to the installer's default arguments (maps to winget's --custom).</summary>
    public string? AdditionalArguments { get; init; }

    public bool Force { get; init; }

    public bool AllowHashMismatch { get; init; }

    public bool SkipDependencies { get; init; }

    /// <summary>Allow upgrading when the installed version reports as Unknown.</summary>
    public bool AllowUpgradeToUnknownVersion { get; init; }

    public bool AcceptPackageAgreements { get; init; } = true;

    /// <summary>Caller correlation data; must be JSON-encoded if set.</summary>
    public string? CorrelationData { get; init; }
}

/// <summary>Options for uninstalling a package.</summary>
public sealed record UninstallRequest
{
    public PackageOperationMode Mode { get; init; } = PackageOperationMode.Silent;

    /// <summary>Uninstall scope; currently only applicable to MSIX packages.</summary>
    public PackageScope Scope { get; init; } = PackageScope.Any;

    public bool Force { get; init; }

    public string? LogOutputPath { get; init; }

    public string? CorrelationData { get; init; }
}

/// <summary>Options for downloading a package's installer without running it.</summary>
public sealed record DownloadRequest
{
    public DownloadRequest(string downloadDirectory) => DownloadDirectory = downloadDirectory;

    public string DownloadDirectory { get; init; }

    public string? Version { get; init; }

    public PackageArchitecture Architecture { get; init; } = PackageArchitecture.Default;

    public PackageInstallerKind InstallerType { get; init; } = PackageInstallerKind.Default;

    public PackageScope Scope { get; init; } = PackageScope.Any;

    public string? Locale { get; init; }

    public bool AllowHashMismatch { get; init; }

    public bool SkipDependencies { get; init; }

    /// <summary>Skip downloading the license file for Microsoft Store packages.</summary>
    public bool SkipMicrosoftStoreLicense { get; init; }

    public bool AcceptPackageAgreements { get; init; } = true;

    public string? CorrelationData { get; init; }
}

/// <summary>Options for repairing an installed package.</summary>
public sealed record RepairRequest
{
    public PackageOperationMode Mode { get; init; } = PackageOperationMode.Silent;

    public PackageScope Scope { get; init; } = PackageScope.Any;

    public bool Force { get; init; }

    public bool AllowHashMismatch { get; init; }

    public bool AcceptPackageAgreements { get; init; } = true;

    public string? LogOutputPath { get; init; }

    public string? CorrelationData { get; init; }
}
