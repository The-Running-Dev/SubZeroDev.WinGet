using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class PackageManagementServiceTests
{
    private Mock<IWinGetClient> _winGetClient = null!;

    private Mock<IWinGetCliClient> _cliClient = null!;

    private PackageManagementService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _winGetClient = new Mock<IWinGetClient>(MockBehavior.Strict);
        _cliClient = new Mock<IWinGetCliClient>(MockBehavior.Strict);
        _service = new PackageManagementService(_winGetClient.Object, _cliClient.Object, NullLogger<PackageManagementService>.Instance);
    }

    private static PackageInfo MakePackage(string id = "Microsoft.VisualStudioCode") =>
        new(id, "Visual Studio Code", "Microsoft Corporation", "1.0.0", "1.1.0", true, true, "winget");

    [Test]
    public async Task Search_TrimsQuery_AndDelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage() };

        _winGetClient
            .Setup(c => c.Search("vscode", 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.Search("  vscode  ");

        result.Should().BeEquivalentTo(expected);
        _winGetClient.VerifyAll();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task Search_WithNoQuery_ReturnsEmpty_WithoutCallingClient(string? query)
    {
        var result = await _service.Search(query!);

        result.Should().BeEmpty();
        _winGetClient.Verify(c => c.Search(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Search_PassesSourceNameThrough()
    {
        _winGetClient
            .Setup(c => c.Search("git", 50, "msstore", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.Search("git", "msstore");

        _winGetClient.VerifyAll();
    }

    [Test]
    public async Task GetInstalled_DelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage(), MakePackage("7zip.7zip") };

        _winGetClient
            .Setup(c => c.GetInstalledPackages(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetInstalled();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetAvailableUpgrades_DelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage() };

        _winGetClient
            .Setup(c => c.GetAvailableUpgrades(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetAvailableUpgrades();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetPackage_DelegatesToClient_AndReturnsPackage()
    {
        var expected = MakePackage();

        _winGetClient
            .Setup(c => c.GetPackage("Microsoft.VisualStudioCode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetPackage("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
    }

    [Test]
    public async Task GetPackage_WhenClientFindsNothing_ReturnsNull()
    {
        _winGetClient
            .Setup(c => c.GetPackage("does-not-exist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PackageInfo?)null);

        var result = await _service.GetPackage("does-not-exist");

        result.Should().BeNull();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void GetDetails_WithNoPackageId_ThrowsArgumentException(string? packageId)
    {
        var act = async () => await _service.GetDetails(packageId!);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task Install_DelegatesToClient_AndReturnsResult()
    {
        var expected = PackageOperationResult.Success();

        _winGetClient
            .Setup(c => c.Install("Microsoft.VisualStudioCode", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.Install("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
    }

    [Test]
    public async Task Install_ForwardsProgressReporterToClient()
    {
        IProgress<PackageOperationProgress>? capturedProgress = null;

        _winGetClient
            .Setup(c => c.Install("id", It.IsAny<InstallRequest>(), It.IsAny<IProgress<PackageOperationProgress>>(), It.IsAny<CancellationToken>()))
            .Callback<string, InstallRequest?, IProgress<PackageOperationProgress>?, CancellationToken>((_, _, p, _) => capturedProgress = p)
            .ReturnsAsync(PackageOperationResult.Success());

        var progress = new Progress<PackageOperationProgress>();

        await _service.Install("id", progress: progress);

        capturedProgress.Should().BeSameAs(progress);
    }

    [Test]
    public async Task Install_WhenPackageAlreadyInstalled_NormalizesToSuccess()
    {
        _winGetClient
            .Setup(c => c.Install("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.InstallError, "InstallError", WinGetErrorCodes.PackageAlreadyInstalled));

        var result = await _service.Install("id");

        result.Succeeded.Should().BeTrue();
        _winGetClient.Verify(c => c.Install("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Install_WithNoApplicableInstallerAndConstraints_RetriesUnconstrained()
    {
        var constrained = new InstallRequest { Architecture = PackageArchitecture.X64, Scope = PackageScope.System };

        _winGetClient
            .Setup(c => c.Install("id", It.Is<InstallRequest>(r => r.Architecture == PackageArchitecture.X64), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.NoApplicableInstallers, "NoApplicableInstallers"));

        _winGetClient
            .Setup(c => c.Install("id", It.Is<InstallRequest>(r => r.Architecture == PackageArchitecture.Default && r.Scope == PackageScope.Any), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.Install("id", constrained);

        result.Succeeded.Should().BeTrue();
        _winGetClient.VerifyAll();
    }

    [Test]
    public async Task Install_WithNoApplicableInstallerAndNoConstraints_DoesNotRetry()
    {
        var failure = PackageOperationResult.Failure(PackageOperationStatus.NoApplicableInstallers, "NoApplicableInstallers");

        _winGetClient
            .Setup(c => c.Install("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.Install("id");

        result.Should().Be(failure);
        _winGetClient.Verify(c => c.Install("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Update_DelegatesToClientUpgrade_AndReturnsResult()
    {
        var expected = PackageOperationResult.Success(rebootRequired: true);

        _winGetClient
            .Setup(c => c.Upgrade("Microsoft.VisualStudioCode", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.Update("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
        result.RebootRequired.Should().BeTrue();
    }

    [Test]
    public async Task Update_WhenUpgradeVersionUnknown_RetriesWithAllowUnknownVersion()
    {
        _winGetClient
            .Setup(c => c.Upgrade("id", It.Is<InstallRequest>(r => !r.AllowUpgradeToUnknownVersion), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.InternalError, "InternalError", WinGetErrorCodes.UpgradeVersionUnknown));

        _winGetClient
            .Setup(c => c.Upgrade("id", It.Is<InstallRequest>(r => r.AllowUpgradeToUnknownVersion), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.Update("id");

        result.Succeeded.Should().BeTrue();
        _winGetClient.VerifyAll();
    }

    [Test]
    public async Task Uninstall_DelegatesToClient_AndReturnsResult()
    {
        var expected = PackageOperationResult.Failure(PackageOperationStatus.UninstallError, "UninstallError", -2147023293);

        _winGetClient
            .Setup(c => c.Uninstall("Microsoft.VisualStudioCode", It.IsAny<UninstallRequest?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.Uninstall("Microsoft.VisualStudioCode");

        result.Succeeded.Should().BeFalse();
        result.Should().Be(expected);
    }

    [Test]
    public async Task Download_DelegatesToClient()
    {
        var request = new DownloadRequest(@"C:\temp\downloads");

        _winGetClient
            .Setup(c => c.Download("id", request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.Download("id", request);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void Download_WithNoDirectory_ThrowsArgumentException()
    {
        var act = async () => await _service.Download("id", new DownloadRequest(""));

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task Repair_DelegatesToClient()
    {
        _winGetClient
            .Setup(c => c.Repair("id", It.IsAny<RepairRequest?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.Repair("id");

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Pin_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.AddPin("id", "1.2.*", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        var result = await _service.Pin("id", "1.2.*", blocking: true);

        result.Succeeded.Should().BeTrue();
        _cliClient.VerifyAll();
    }

    [Test]
    public async Task Export_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.Export(@"C:\temp\packages.json", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        var result = await _service.Export(@"C:\temp\packages.json", includeVersions: true);

        result.Succeeded.Should().BeTrue();
        _cliClient.VerifyAll();
    }

    [Test]
    public void Export_WithNoPath_ThrowsArgumentException()
    {
        var act = async () => await _service.Export("");

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task Install_WhenClientThrows_PropagatesException()
    {
        _winGetClient
            .Setup(c => c.Install("missing", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to connect to WinGet catalog: CatalogError."));

        var act = async () => await _service.Install("missing");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to connect*");
    }
}
