using Microsoft.Management.Deployment;

using Windows.System;

using FluentAssertions;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetProjectionMapperTests
{
    [Test]
    public void MapStatus_MapsEveryInstallResultStatusAndUnknownValue()
    {
        var expected = new Dictionary<InstallResultStatus, PackageOperationStatus>
        {
            [InstallResultStatus.Ok] = PackageOperationStatus.Ok,
            [InstallResultStatus.BlockedByPolicy] = PackageOperationStatus.BlockedByPolicy,
            [InstallResultStatus.CatalogError] = PackageOperationStatus.CatalogError,
            [InstallResultStatus.InternalError] = PackageOperationStatus.InternalError,
            [InstallResultStatus.InvalidOptions] = PackageOperationStatus.InvalidOptions,
            [InstallResultStatus.DownloadError] = PackageOperationStatus.DownloadError,
            [InstallResultStatus.InstallError] = PackageOperationStatus.InstallError,
            [InstallResultStatus.ManifestError] = PackageOperationStatus.ManifestError,
            [InstallResultStatus.NoApplicableInstallers] = PackageOperationStatus.NoApplicableInstallers,
            [InstallResultStatus.NoApplicableUpgrade] = PackageOperationStatus.NoApplicableUpgrade,
            [InstallResultStatus.PackageAgreementsNotAccepted] = PackageOperationStatus.PackageAgreementsNotAccepted
        };

        Enum.GetValues<InstallResultStatus>().Should().OnlyContain(status => expected.ContainsKey(status));
        expected.Should().AllSatisfy(pair => WinGetProjectionMapper.MapStatus(pair.Key).Should().Be(pair.Value));
        WinGetProjectionMapper.MapStatus((InstallResultStatus)int.MaxValue).Should().Be(PackageOperationStatus.Unknown);
    }

    [Test]
    public void MapStatus_MapsEveryUninstallResultStatusAndUnknownValue()
    {
        var expected = new Dictionary<UninstallResultStatus, PackageOperationStatus>
        {
            [UninstallResultStatus.Ok] = PackageOperationStatus.Ok,
            [UninstallResultStatus.BlockedByPolicy] = PackageOperationStatus.BlockedByPolicy,
            [UninstallResultStatus.CatalogError] = PackageOperationStatus.CatalogError,
            [UninstallResultStatus.InternalError] = PackageOperationStatus.InternalError,
            [UninstallResultStatus.InvalidOptions] = PackageOperationStatus.InvalidOptions,
            [UninstallResultStatus.UninstallError] = PackageOperationStatus.UninstallError,
            [UninstallResultStatus.ManifestError] = PackageOperationStatus.ManifestError
        };

        Enum.GetValues<UninstallResultStatus>().Should().OnlyContain(status => expected.ContainsKey(status));
        expected.Should().AllSatisfy(pair => WinGetProjectionMapper.MapStatus(pair.Key).Should().Be(pair.Value));
        WinGetProjectionMapper.MapStatus((UninstallResultStatus)int.MaxValue).Should().Be(PackageOperationStatus.Unknown);
    }

    [Test]
    public void MapStatus_MapsEveryDownloadResultStatusAndUnknownValue()
    {
        var expected = new Dictionary<DownloadResultStatus, PackageOperationStatus>
        {
            [DownloadResultStatus.Ok] = PackageOperationStatus.Ok,
            [DownloadResultStatus.BlockedByPolicy] = PackageOperationStatus.BlockedByPolicy,
            [DownloadResultStatus.CatalogError] = PackageOperationStatus.CatalogError,
            [DownloadResultStatus.InternalError] = PackageOperationStatus.InternalError,
            [DownloadResultStatus.InvalidOptions] = PackageOperationStatus.InvalidOptions,
            [DownloadResultStatus.DownloadError] = PackageOperationStatus.DownloadError,
            [DownloadResultStatus.ManifestError] = PackageOperationStatus.ManifestError,
            [DownloadResultStatus.NoApplicableInstallers] = PackageOperationStatus.NoApplicableInstallers,
            [DownloadResultStatus.PackageAgreementsNotAccepted] = PackageOperationStatus.PackageAgreementsNotAccepted
        };

        Enum.GetValues<DownloadResultStatus>().Should().OnlyContain(status => expected.ContainsKey(status));
        expected.Should().AllSatisfy(pair => WinGetProjectionMapper.MapStatus(pair.Key).Should().Be(pair.Value));
        WinGetProjectionMapper.MapStatus((DownloadResultStatus)int.MaxValue).Should().Be(PackageOperationStatus.Unknown);
    }

    [Test]
    public void MapStatus_MapsEveryRepairResultStatusAndUnknownValue()
    {
        var expected = new Dictionary<RepairResultStatus, PackageOperationStatus>
        {
            [RepairResultStatus.Ok] = PackageOperationStatus.Ok,
            [RepairResultStatus.BlockedByPolicy] = PackageOperationStatus.BlockedByPolicy,
            [RepairResultStatus.CatalogError] = PackageOperationStatus.CatalogError,
            [RepairResultStatus.DownloadError] = PackageOperationStatus.DownloadError,
            [RepairResultStatus.InternalError] = PackageOperationStatus.InternalError,
            [RepairResultStatus.InvalidOptions] = PackageOperationStatus.InvalidOptions,
            [RepairResultStatus.RepairError] = PackageOperationStatus.RepairError,
            [RepairResultStatus.ManifestError] = PackageOperationStatus.ManifestError,
            [RepairResultStatus.NoApplicableRepairer] = PackageOperationStatus.NoApplicableRepairer,
            [RepairResultStatus.PackageAgreementsNotAccepted] = PackageOperationStatus.PackageAgreementsNotAccepted
        };

        Enum.GetValues<RepairResultStatus>().Should().OnlyContain(status => expected.ContainsKey(status));
        expected.Should().AllSatisfy(pair => WinGetProjectionMapper.MapStatus(pair.Key).Should().Be(pair.Value));
        WinGetProjectionMapper.MapStatus((RepairResultStatus)int.MaxValue).Should().Be(PackageOperationStatus.Unknown);
    }

    [TestCase(PackageScope.Any, PackageInstallScope.Any)]
    [TestCase(PackageScope.User, PackageInstallScope.User)]
    [TestCase(PackageScope.System, PackageInstallScope.System)]
    [TestCase(PackageScope.UserOrUnknown, PackageInstallScope.UserOrUnknown)]
    [TestCase(PackageScope.SystemOrUnknown, PackageInstallScope.SystemOrUnknown)]
    public void MapScope_MapsEveryPublicValue(PackageScope input, PackageInstallScope expected) =>
        WinGetProjectionMapper.MapScope(input).Should().Be(expected);

    [TestCase(PackageOperationMode.Default, PackageInstallMode.Default)]
    [TestCase(PackageOperationMode.Silent, PackageInstallMode.Silent)]
    [TestCase(PackageOperationMode.Interactive, PackageInstallMode.Interactive)]
    public void MapInstallMode_MapsEveryPublicValue(PackageOperationMode input, PackageInstallMode expected) =>
        WinGetProjectionMapper.MapInstallMode(input).Should().Be(expected);

    [TestCase(PackageOperationMode.Default, PackageUninstallMode.Default)]
    [TestCase(PackageOperationMode.Silent, PackageUninstallMode.Silent)]
    [TestCase(PackageOperationMode.Interactive, PackageUninstallMode.Interactive)]
    public void MapUninstallMode_MapsEveryPublicValue(PackageOperationMode input, PackageUninstallMode expected) =>
        WinGetProjectionMapper.MapUninstallMode(input).Should().Be(expected);

    [TestCase(PackageScope.Any, PackageUninstallScope.Any)]
    [TestCase(PackageScope.User, PackageUninstallScope.User)]
    [TestCase(PackageScope.System, PackageUninstallScope.System)]
    [TestCase(PackageScope.UserOrUnknown, PackageUninstallScope.User)]
    [TestCase(PackageScope.SystemOrUnknown, PackageUninstallScope.System)]
    public void MapUninstallScope_MapsEveryPublicValue(PackageScope input, PackageUninstallScope expected) =>
        WinGetProjectionMapper.MapUninstallScope(input).Should().Be(expected);

    [TestCase(PackageOperationMode.Default, PackageRepairMode.Default)]
    [TestCase(PackageOperationMode.Silent, PackageRepairMode.Silent)]
    [TestCase(PackageOperationMode.Interactive, PackageRepairMode.Interactive)]
    public void MapRepairMode_MapsEveryPublicValue(PackageOperationMode input, PackageRepairMode expected) =>
        WinGetProjectionMapper.MapRepairMode(input).Should().Be(expected);

    [TestCase(PackageScope.Any, PackageRepairScope.Any)]
    [TestCase(PackageScope.User, PackageRepairScope.User)]
    [TestCase(PackageScope.System, PackageRepairScope.System)]
    [TestCase(PackageScope.UserOrUnknown, PackageRepairScope.User)]
    [TestCase(PackageScope.SystemOrUnknown, PackageRepairScope.System)]
    public void MapRepairScope_MapsEveryPublicValue(PackageScope input, PackageRepairScope expected) =>
        WinGetProjectionMapper.MapRepairScope(input).Should().Be(expected);

    [TestCase(PackageArchitecture.Default, ProcessorArchitecture.Unknown)]
    [TestCase(PackageArchitecture.X86, ProcessorArchitecture.X86)]
    [TestCase(PackageArchitecture.X64, ProcessorArchitecture.X64)]
    [TestCase(PackageArchitecture.Arm, ProcessorArchitecture.Arm)]
    [TestCase(PackageArchitecture.Arm64, ProcessorArchitecture.Arm64)]
    public void MapArchitecture_MapsEveryPublicValue(PackageArchitecture input, ProcessorArchitecture expected) =>
        WinGetProjectionMapper.MapArchitecture(input).Should().Be(expected);

    [TestCase(PackageInstallerKind.Default, PackageInstallerType.Unknown)]
    [TestCase(PackageInstallerKind.Unknown, PackageInstallerType.Unknown)]
    [TestCase(PackageInstallerKind.Inno, PackageInstallerType.Inno)]
    [TestCase(PackageInstallerKind.Wix, PackageInstallerType.Wix)]
    [TestCase(PackageInstallerKind.Msi, PackageInstallerType.Msi)]
    [TestCase(PackageInstallerKind.Nullsoft, PackageInstallerType.Nullsoft)]
    [TestCase(PackageInstallerKind.Zip, PackageInstallerType.Zip)]
    [TestCase(PackageInstallerKind.Msix, PackageInstallerType.Msix)]
    [TestCase(PackageInstallerKind.Exe, PackageInstallerType.Exe)]
    [TestCase(PackageInstallerKind.Burn, PackageInstallerType.Burn)]
    [TestCase(PackageInstallerKind.MSStore, PackageInstallerType.MSStore)]
    [TestCase(PackageInstallerKind.Portable, PackageInstallerType.Portable)]
    [TestCase(PackageInstallerKind.Font, PackageInstallerType.Font)]
    public void MapInstallerType_MapsEveryPublicValue(PackageInstallerKind input, PackageInstallerType expected) =>
        WinGetProjectionMapper.MapInstallerType(input).Should().Be(expected);

    [Test]
    public void CopyStrings_WithNullSource_ReturnsEmptyList() =>
        WinGetProjectionMapper.CopyStrings(null).Should().BeEmpty();

    [Test]
    public void CopyStrings_WithEmptySource_ReturnsEmptyList() =>
        WinGetProjectionMapper.CopyStrings(new List<string>()).Should().BeEmpty();

    [Test]
    public void CopyStrings_WithNonEmptySource_PreservesEveryItemInOrder()
    {
        var source = new List<string> { "beta", "alpha", "beta", "gamma" };

        WinGetProjectionMapper.CopyStrings(source).Should().Equal(source);
    }

    [Test]
    public void ToNullableDate_WithDefaultValue_ReturnsNull() =>
        WinGetProjectionMapper.ToNullableDate(default).Should().BeNull();

    [Test]
    public void ToNullableDate_WithNonDefaultValue_ReturnsSameValue()
    {
        var value = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

        WinGetProjectionMapper.ToNullableDate(value).Should().Be(value);
    }
}
