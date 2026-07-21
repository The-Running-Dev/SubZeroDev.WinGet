namespace SubZeroDev.PackageManagement;

/// <summary>
/// Well-known WinGet HRESULTs (APPINSTALLER_CLI_ERROR_*), from
/// https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md.
/// These appear in <see cref="Models.PackageOperationResult.ExtendedErrorCode"/>.
/// </summary>
public static class WinGetErrorCodes
{
    public const int CommandRequiresAdmin = unchecked((int)0x8A150019);

    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);

    public const int NoManifestFound = unchecked((int)0x8A150017);

    public const int UpgradeVersionNotNewer = unchecked((int)0x8A15004F);

    public const int UpgradeVersionUnknown = unchecked((int)0x8A150050);

    public const int InstallerProhibitsElevation = unchecked((int)0x8A150056);

    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);

    public const int PackageIsPinned = unchecked((int)0x8A150068);

    public const int InstallAlreadyInstalled = unchecked((int)0x8A15010D);

    public const int InstallDowngrade = unchecked((int)0x8A15010E);

    public const int RebootRequiredToFinish = unchecked((int)0x8A150109);

    public const int InstallCancelledByUser = unchecked((int)0x8A15010C);
}
