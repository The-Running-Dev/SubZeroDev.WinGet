namespace SubZeroDev.WinGet.Models;

/// <summary>
/// Required install scope for a package. Maps to Microsoft.Management.Deployment.PackageInstallScope.
/// </summary>
public enum PackageScope
{
    Any,
    User,
    System,
    UserOrUnknown,
    SystemOrUnknown
}

/// <summary>
/// How the installer UI behaves. Maps to PackageInstallMode/PackageUninstallMode/PackageRepairMode.
/// </summary>
public enum PackageOperationMode
{
    Default,
    Silent,
    Interactive
}

/// <summary>
/// Installer architecture preference. Default lets WinGet pick per system applicability rules.
/// </summary>
public enum PackageArchitecture
{
    Default,
    X86,
    X64,
    Arm,
    Arm64
}

/// <summary>
/// Installer technology. Default lets WinGet pick. Maps to Microsoft.Management.Deployment.PackageInstallerType.
/// </summary>
public enum PackageInstallerKind
{
    Default,
    Unknown,
    Inno,
    Wix,
    Msi,
    Nullsoft,
    Zip,
    Msix,
    Exe,
    Burn,
    MSStore,
    Portable,
    Font
}
