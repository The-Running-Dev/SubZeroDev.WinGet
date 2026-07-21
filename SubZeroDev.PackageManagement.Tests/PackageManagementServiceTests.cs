using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.PackageManagement.Abstractions;
using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement.Tests;

[TestFixture]
public class PackageManagementServiceTests
{
    private Mock<IWinGetClient> _winGetClient = null!;

    private PackageManagementService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _winGetClient = new Mock<IWinGetClient>(MockBehavior.Strict);
        _service = new PackageManagementService(_winGetClient.Object, NullLogger<PackageManagementService>.Instance);
    }

    private static PackageInfo MakePackage(string id = "Microsoft.VisualStudioCode") =>
        new(id, "Visual Studio Code", "Microsoft Corporation", "1.0.0", "1.1.0", true, true, "winget");

    [Test]
    public async Task SearchAsync_TrimsQuery_AndDelegatesToClient()
    {
        var expected = new List<PackageInfo> { MakePackage() };

        _winGetClient
            .Setup(c => c.SearchAsync("vscode", 50, It.IsAny<CancellationToken>()))
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
        _winGetClient.Verify(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task GetDetailsAsync_DelegatesToClient_AndReturnsPackage()
    {
        var expected = MakePackage();

        _winGetClient
            .Setup(c => c.GetPackageAsync("Microsoft.VisualStudioCode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetDetailsAsync("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
    }

    [Test]
    public async Task GetDetailsAsync_WhenClientFindsNothing_ReturnsNull()
    {
        _winGetClient
            .Setup(c => c.GetPackageAsync("does-not-exist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PackageInfo?)null);

        var result = await _service.GetDetailsAsync("does-not-exist");

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
            .Setup(c => c.InstallAsync("Microsoft.VisualStudioCode", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.InstallAsync("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
    }

    [Test]
    public async Task InstallAsync_ForwardsProgressReporterToClient()
    {
        IProgress<PackageOperationProgress>? capturedProgress = null;

        _winGetClient
            .Setup(c => c.InstallAsync("id", It.IsAny<IProgress<PackageOperationProgress>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IProgress<PackageOperationProgress>?, CancellationToken>((_, p, _) => capturedProgress = p)
            .ReturnsAsync(PackageOperationResult.Success());

        var progress = new Progress<PackageOperationProgress>();

        await _service.InstallAsync("id", progress);

        capturedProgress.Should().BeSameAs(progress);
    }

    [Test]
    public async Task UpdateAsync_DelegatesToClientUpgrade_AndReturnsResult()
    {
        var expected = PackageOperationResult.Success(rebootRequired: true);

        _winGetClient
            .Setup(c => c.UpgradeAsync("Microsoft.VisualStudioCode", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.UpdateAsync("Microsoft.VisualStudioCode");

        result.Should().Be(expected);
        result.RebootRequired.Should().BeTrue();
    }

    [Test]
    public async Task UninstallAsync_DelegatesToClient_AndReturnsResult()
    {
        var expected = PackageOperationResult.Failure("InternalError", -2147023293);

        _winGetClient
            .Setup(c => c.UninstallAsync("Microsoft.VisualStudioCode", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.UninstallAsync("Microsoft.VisualStudioCode");

        result.Succeeded.Should().BeFalse();
        result.Should().Be(expected);
    }

    [Test]
    public async Task InstallAsync_WhenClientThrows_PropagatesException()
    {
        _winGetClient
            .Setup(c => c.InstallAsync("missing", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Package 'missing' was not found in any configured source."));

        var act = async () => await _service.InstallAsync("missing");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found*");
    }
}
