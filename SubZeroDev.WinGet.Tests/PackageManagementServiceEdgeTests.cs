using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// Covers the retry-policy edges, validation, and CLI-delegation paths not exercised by the
/// happy-path fixture in <see cref="PackageManagementServiceTests"/>.
/// </summary>
[TestFixture]
public class PackageManagementServiceEdgeTests
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

    [Test]
    public async Task GetWinGetVersionAsync_DelegatesToClient()
    {
        _winGetClient
            .Setup(c => c.GetWinGetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1.29.280");

        (await _service.GetWinGetVersionAsync()).Should().Be("v1.29.280");
    }

    // The four documented "actually fine" codes: package/version already present.
    [TestCase(WinGetErrorCodes.PackageAlreadyInstalled)]
    [TestCase(WinGetErrorCodes.InstallAlreadyInstalled)]
    [TestCase(WinGetErrorCodes.InstallDowngrade)]
    [TestCase(WinGetErrorCodes.UpgradeVersionNotNewer)]
    public async Task InstallAsync_WithAnyAlreadyInstalledCode_NormalizesToSuccess(int errorCode)
    {
        _winGetClient
            .Setup(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.InstallError, "already installed", errorCode));

        var result = await _service.InstallAsync("id");

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task InstallAsync_WithOtherFailure_DoesNotNormalize()
    {
        var failure = PackageOperationResult.Failure(PackageOperationStatus.DownloadError, "network", -1);

        _winGetClient
            .Setup(c => c.InstallAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.InstallAsync("id");

        result.Should().Be(failure);
    }

    [Test]
    public async Task UpdateAsync_WithNoApplicableUpgradeUnderConstraints_RetriesUnconstrained()
    {
        var constrained = new InstallRequest { Architecture = PackageArchitecture.X64, Scope = PackageScope.System };
        var sequence = new List<InstallRequest>();

        _winGetClient
            .Setup(c => c.UpgradeAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .Callback<string, InstallRequest, IProgress<PackageOperationProgress>?, CancellationToken>((_, r, _, _) => sequence.Add(r))
            .ReturnsAsync(() => sequence.Count == 1
                ? PackageOperationResult.Failure(PackageOperationStatus.NoApplicableUpgrade, "not applicable")
                : PackageOperationResult.Success());

        var result = await _service.UpdateAsync("id", constrained);

        result.Succeeded.Should().BeTrue();
        sequence.Should().HaveCount(2);
        sequence[1].Architecture.Should().Be(PackageArchitecture.Default);
        sequence[1].InstallerType.Should().Be(PackageInstallerKind.Default);
        sequence[1].Scope.Should().Be(PackageScope.Any);
    }

    [Test]
    public async Task UpdateAsync_WhenUnknownVersionRetryIsAlreadyAllowed_DoesNotRetry()
    {
        var request = new InstallRequest { AllowUpgradeToUnknownVersion = true };
        var failure = PackageOperationResult.Failure(PackageOperationStatus.InstallError, "unknown version", WinGetErrorCodes.UpgradeVersionUnknown);

        _winGetClient
            .Setup(c => c.UpgradeAsync("id", request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.UpdateAsync("id", request);

        result.Should().Be(failure);
        _winGetClient.Verify(c => c.UpgradeAsync("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void AllPackageOperations_WithNoPackageId_ThrowArgumentException(string? packageId)
    {
        var operations = new Func<Task>[]
        {
            () => _service.GetPackageAsync(packageId!),
            () => _service.GetDetailsAsync(packageId!),
            () => _service.InstallAsync(packageId!),
            () => _service.UpdateAsync(packageId!),
            () => _service.UninstallAsync(packageId!),
            () => _service.DownloadAsync(packageId!, new DownloadRequest(@"C:\tmp")),
            () => _service.RepairAsync(packageId!),
            () => _service.PinAsync(packageId!),
            () => _service.UnpinAsync(packageId!),
        };

        foreach (var operation in operations)
        {
            operation.Should().ThrowAsync<ArgumentException>();
        }
    }

    [Test]
    public async Task RepairAsync_ReturnsFailureUnchanged()
    {
        var failure = PackageOperationResult.Failure(PackageOperationStatus.NoApplicableRepairer, "no repairer");

        _winGetClient
            .Setup(c => c.RepairAsync("id", It.IsAny<RepairRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        (await _service.RepairAsync("id")).Should().Be(failure);
    }

    [Test]
    public async Task GetPinsAsync_DelegatesToCliClient()
    {
        var pins = new List<PackagePin> { new("Git.Git", "Git", "2.44.0", PackagePinKind.Pinning, "winget") };

        _cliClient
            .Setup(c => c.GetPinsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);

        (await _service.GetPinsAsync()).Should().BeEquivalentTo(pins);
    }

    [Test]
    public async Task UnpinAsync_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.RemovePinAsync("Git.Git", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        (await _service.UnpinAsync("Git.Git")).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task PinAsync_PassesVersionAndBlockingThrough()
    {
        _cliClient
            .Setup(c => c.AddPinAsync("Git.Git", "2.44.*", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        (await _service.PinAsync("Git.Git", "2.44.*", blocking: true)).Succeeded.Should().BeTrue();

        _cliClient.VerifyAll();
    }

    [Test]
    public async Task ImportAsync_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.ImportAsync(@"C:\pkgs.json", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        (await _service.ImportAsync(@"C:\pkgs.json", ignoreUnavailable: true)).Succeeded.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ImportAsync_WithNoPath_ThrowsArgumentException(string filePath)
    {
        var act = async () => await _service.ImportAsync(filePath);

        act.Should().ThrowAsync<ArgumentException>();
    }
}
