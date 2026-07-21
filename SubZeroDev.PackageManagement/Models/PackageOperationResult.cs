namespace SubZeroDev.PackageManagement.Models;

/// <summary>
/// Normalized status across install/upgrade/uninstall/download/repair operations. Union of the
/// per-operation status enums in Microsoft.Management.Deployment (InstallResultStatus,
/// UninstallResultStatus, DownloadResultStatus, RepairResultStatus).
/// </summary>
public enum PackageOperationStatus
{
    Ok,
    BlockedByPolicy,
    CatalogError,
    InternalError,
    InvalidOptions,
    DownloadError,
    InstallError,
    UninstallError,
    RepairError,
    ManifestError,
    NoApplicableInstallers,
    NoApplicableUpgrade,
    NoApplicableRepairer,
    PackageAgreementsNotAccepted,
    PackageNotFound,
    Cancelled,
    Unknown
}

/// <summary>
/// The outcome of an install/upgrade/uninstall/download/repair operation.
/// </summary>
public sealed record PackageOperationResult(
    bool Succeeded,
    PackageOperationStatus Status,
    string? ErrorMessage,
    int? ExtendedErrorCode,
    uint? InstallerErrorCode,
    bool RebootRequired)
{
    public static PackageOperationResult Success(bool rebootRequired = false) =>
        new(true, PackageOperationStatus.Ok, null, null, null, rebootRequired);

    public static PackageOperationResult Failure(
        PackageOperationStatus status,
        string errorMessage,
        int? extendedErrorCode = null,
        uint? installerErrorCode = null) =>
        new(false, status, errorMessage, extendedErrorCode, installerErrorCode, false);
}
