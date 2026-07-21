namespace SubZeroDev.PackageManagement.Models;

/// <summary>
/// The outcome of an install/upgrade/uninstall operation.
/// </summary>
public sealed record PackageOperationResult(
    bool Succeeded,
    string? ErrorMessage,
    int? ExtendedErrorCode,
    bool RebootRequired)
{
    public static PackageOperationResult Success(bool rebootRequired = false) =>
        new(true, null, null, rebootRequired);

    public static PackageOperationResult Failure(string errorMessage, int? extendedErrorCode = null) =>
        new(false, errorMessage, extendedErrorCode, false);
}
