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
    public async Task SearchAsync_TrimsQuery_AndDelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage() };

        _winGetClient
            .Setup(c => c.SearchAsync("vscode", 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.SearchAsync("  vscode  ");

        result.Should().BeEquivalentTo(expected);
        _winGetClient.VerifyAll();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task SearchAsync_WithNoQuery_ReturnsEmpty_WithoutCallingClient(string? query)
    {
        var result = await _service.SearchAsync(query!);

        result.Should().BeEmpty();
        _winGetClient.Verify(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SearchAsync_PassesSourceNameThrough()
    {
        _winGetClient
            .Setup(c => c.SearchAsync("git", 50, "msstore", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.SearchAsync("git", "msstore");

        _winGetClient.VerifyAll();
    }

    [Test]
    public async Task GetInstalledAsync_DelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage(), MakePackage("7zip.7zip") };

        _winGetClient
            .Setup(c => c.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetInstalledAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetAvailableUpgradesAsync_DelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage() };

        _winGetClient
            .Setup(c => c.GetAvailableUpgradesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetAvailableUpgradesAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetPackageAsync_DelegatesToClient_AndReturnsPackage()
    {
        var expected = MakePackage();

        _winGetClient
            .Setup(c => c.GetPackageAsync("Microsoft.VisualStudioCode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetPackageAsync("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
    }

    [Test]
    public async Task GetPackageAsync_WhenClientFindsNothing_ReturnsNull()
    {
        _winGetClient
            .Setup(c => c.GetPackageAsync("does-not-exist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PackageInfo?)null);

        var result = await _service.GetPackageAsync("does-not-exist");

        result.Should().BeNull();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void GetDetailsAsync_WithNoPackageId_ThrowsArgumentException(string? packageId)
    {
        var act = async () => await _service.GetDetailsAsync(packageId!);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task InstallAsync_DelegatesToClient_AndReturnsResult()
    {
        var expected = PackageOperationResult.Success();

        _winGetClient
            .Setup(c => c.InstallAsync("Microsoft.VisualStudioCode", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.InstallAsync("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
    }

    [Test]
    public async Task InstallAsync_ForwardsProgressReporterToClient()
    {
        IProgress<PackageOperationProgress>? capturedProgress = null;

        _winGetClient
            .Setup(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), It.IsAny<IProgress<PackageOperationProgress>>(), It.IsAny<CancellationToken>()))
            .Callback<string, InstallRequest?, IProgress<PackageOperationProgress>?, CancellationToken>((_, _, p, _) => capturedProgress = p)
            .ReturnsAsync(PackageOperationResult.Success());

        var progress = new Progress<PackageOperationProgress>();

        await _service.InstallAsync("id", progress: progress);

        capturedProgress.Should().BeSameAs(progress);
    }

    [Test]
    public async Task InstallAsync_WhenPackageAlreadyInstalled_NormalizesToSuccess()
    {
        _winGetClient
            .Setup(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.InstallError, "InstallError", WinGetErrorCodes.PackageAlreadyInstalled));

        var result = await _service.InstallAsync("id");

        result.Succeeded.Should().BeTrue();
        _winGetClient.Verify(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InstallAsync_WithNoApplicableInstallerAndConstraints_RetriesUnconstrained()
    {
        var constrained = new InstallRequest { Architecture = PackageArchitecture.X64, Scope = PackageScope.System };

        _winGetClient
            .Setup(c => c.InstallAsync("id", It.Is<InstallRequest>(r => r.Architecture == PackageArchitecture.X64), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.NoApplicableInstallers, "NoApplicableInstallers"));

        _winGetClient
            .Setup(c => c.InstallAsync("id", It.Is<InstallRequest>(r => r.Architecture == PackageArchitecture.Default && r.Scope == PackageScope.Any), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.InstallAsync("id", constrained);

        result.Succeeded.Should().BeTrue();
        _winGetClient.VerifyAll();
    }

    [Test]
    public async Task InstallAsync_WithNoApplicableInstallerAndNoConstraints_DoesNotRetry()
    {
        var failure = PackageOperationResult.Failure(PackageOperationStatus.NoApplicableInstallers, "NoApplicableInstallers");

        _winGetClient
            .Setup(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.InstallAsync("id");

        result.Should().Be(failure);
        _winGetClient.Verify(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_DelegatesToClientUpgrade_AndReturnsResult()
    {
        var expected = PackageOperationResult.Success(rebootRequired: true);

        _winGetClient
            .Setup(c => c.UpgradeAsync("Microsoft.VisualStudioCode", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.UpdateAsync("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
        result.RebootRequired.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_WhenUpgradeVersionUnknown_RetriesWithAllowUnknownVersion()
    {
        _winGetClient
            .Setup(c => c.UpgradeAsync("id", It.Is<InstallRequest>(r => !r.AllowUpgradeToUnknownVersion), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.InternalError, "InternalError", WinGetErrorCodes.UpgradeVersionUnknown));

        _winGetClient
            .Setup(c => c.UpgradeAsync("id", It.Is<InstallRequest>(r => r.AllowUpgradeToUnknownVersion), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.UpdateAsync("id");

        result.Succeeded.Should().BeTrue();
        _winGetClient.VerifyAll();
    }

    [Test]
    public async Task UninstallAsync_DelegatesToClient_AndReturnsResult()
    {
        var expected = PackageOperationResult.Failure(PackageOperationStatus.UninstallError, "UninstallError", -2147023293);

        _winGetClient
            .Setup(c => c.UninstallAsync("Microsoft.VisualStudioCode", It.IsAny<UninstallRequest?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.UninstallAsync("Microsoft.VisualStudioCode");

        result.Succeeded.Should().BeFalse();
        result.Should().Be(expected);
    }

    [Test]
    public async Task DownloadAsync_DelegatesToClient()
    {
        var request = new DownloadRequest(@"C:\temp\downloads");

        _winGetClient
            .Setup(c => c.DownloadAsync("id", request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.DownloadAsync("id", request);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void DownloadAsync_WithNoDirectory_ThrowsArgumentException()
    {
        var act = async () => await _service.DownloadAsync("id", new DownloadRequest(""));

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task RepairAsync_DelegatesToClient()
    {
        _winGetClient
            .Setup(c => c.RepairAsync("id", It.IsAny<RepairRequest?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Success());

        var result = await _service.RepairAsync("id");

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task PinAsync_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.AddPinAsync("id", "1.2.*", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        var result = await _service.PinAsync("id", "1.2.*", blocking: true);

        result.Succeeded.Should().BeTrue();
        _cliClient.VerifyAll();
    }

    [Test]
    public async Task ExportAsync_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.ExportAsync(@"C:\temp\packages.json", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        var result = await _service.ExportAsync(@"C:\temp\packages.json", includeVersions: true);

        result.Succeeded.Should().BeTrue();
        _cliClient.VerifyAll();
    }

    [Test]
    public void ExportAsync_WithNoPath_ThrowsArgumentException()
    {
        var act = async () => await _service.ExportAsync("");

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task InstallAsync_WhenClientThrows_PropagatesException()
    {
        _winGetClient
            .Setup(c => c.InstallAsync("missing", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to connect to WinGet catalog: CatalogError."));

        var act = async () => await _service.InstallAsync("missing");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to connect*");
    }
}
