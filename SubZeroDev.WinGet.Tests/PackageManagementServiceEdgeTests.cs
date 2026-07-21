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
    public async Task GetWinGetVersion_DelegatesToClient()
    {
        _winGetClient
            .Setup(c => c.GetWinGetVersion(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1.29.280");

        (await _service.GetWinGetVersion()).Should().Be("v1.29.280");
    }

    // The four documented "actually fine" codes: package/version already present.
    [TestCase(WinGetErrorCodes.PackageAlreadyInstalled)]
    [TestCase(WinGetErrorCodes.InstallAlreadyInstalled)]
    [TestCase(WinGetErrorCodes.InstallDowngrade)]
    [TestCase(WinGetErrorCodes.UpgradeVersionNotNewer)]
    public async Task Install_WithAnyAlreadyInstalledCode_NormalizesToSuccess(int errorCode)
    {
        _winGetClient
            .Setup(c => c.Install("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageOperationResult.Failure(PackageOperationStatus.InstallError, "already installed", errorCode));

        var result = await _service.Install("id");

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Install_WithOtherFailure_DoesNotNormalize()
    {
        var failure = PackageOperationResult.Failure(PackageOperationStatus.DownloadError, "network", -1);

        _winGetClient
            .Setup(c => c.Install("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.Install("id");

        result.Should().Be(failure);
    }

    [Test]
    public async Task Update_WithNoApplicableUpgradeUnderConstraints_RetriesUnconstrained()
    {
        var constrained = new InstallRequest { Architecture = PackageArchitecture.X64, Scope = PackageScope.System };
        var sequence = new List<InstallRequest>();

        _winGetClient
            .Setup(c => c.Upgrade("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()))
            .Callback<string, InstallRequest, IProgress<PackageOperationProgress>?, CancellationToken>((_, r, _, _) => sequence.Add(r))
            .ReturnsAsync(() => sequence.Count == 1
                ? PackageOperationResult.Failure(PackageOperationStatus.NoApplicableUpgrade, "not applicable")
                : PackageOperationResult.Success());

        var result = await _service.Update("id", constrained);

        result.Succeeded.Should().BeTrue();
        sequence.Should().HaveCount(2);
        sequence[1].Architecture.Should().Be(PackageArchitecture.Default);
        sequence[1].InstallerType.Should().Be(PackageInstallerKind.Default);
        sequence[1].Scope.Should().Be(PackageScope.Any);
    }

    [Test]
    public async Task Update_WhenUnknownVersionRetryIsAlreadyAllowed_DoesNotRetry()
    {
        var request = new InstallRequest { AllowUpgradeToUnknownVersion = true };
        var failure = PackageOperationResult.Failure(PackageOperationStatus.InstallError, "unknown version", WinGetErrorCodes.UpgradeVersionUnknown);

        _winGetClient
            .Setup(c => c.Upgrade("id", request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.Update("id", request);

        result.Should().Be(failure);
        _winGetClient.Verify(c => c.Upgrade("id", It.IsAny<InstallRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void AllPackageOperations_WithNoPackageId_ThrowArgumentException(string? packageId)
    {
        var operations = new Func<Task>[]
        {
            () => _service.GetPackage(packageId!),
            () => _service.GetDetails(packageId!),
            () => _service.Install(packageId!),
            () => _service.Update(packageId!),
            () => _service.Uninstall(packageId!),
            () => _service.Download(packageId!, new DownloadRequest(@"C:\tmp")),
            () => _service.Repair(packageId!),
            () => _service.Pin(packageId!),
            () => _service.Unpin(packageId!),
        };

        foreach (var operation in operations)
        {
            operation.Should().ThrowAsync<ArgumentException>();
        }
    }

    [Test]
    public async Task Repair_ReturnsFailureUnchanged()
    {
        var failure = PackageOperationResult.Failure(PackageOperationStatus.NoApplicableRepairer, "no repairer");

        _winGetClient
            .Setup(c => c.Repair("id", It.IsAny<RepairRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        (await _service.Repair("id")).Should().Be(failure);
    }

    [Test]
    public async Task GetPins_DelegatesToCliClient()
    {
        var pins = new List<PackagePin> { new("Git.Git", "Git", "2.44.0", PackagePinKind.Pinning, "winget") };

        _cliClient
            .Setup(c => c.GetPins(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);

        (await _service.GetPins()).Should().BeEquivalentTo(pins);
    }

    [Test]
    public async Task Unpin_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.RemovePin("Git.Git", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        (await _service.Unpin("Git.Git")).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Pin_PassesVersionAndBlockingThrough()
    {
        _cliClient
            .Setup(c => c.AddPin("Git.Git", "2.44.*", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        (await _service.Pin("Git.Git", "2.44.*", blocking: true)).Succeeded.Should().BeTrue();

        _cliClient.VerifyAll();
    }

    [Test]
    public async Task Import_DelegatesToCliClient()
    {
        _cliClient
            .Setup(c => c.Import(@"C:\pkgs.json", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliOperationResult(true, 0, "", ""));

        (await _service.Import(@"C:\pkgs.json", ignoreUnavailable: true)).Succeeded.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Import_WithNoPath_ThrowsArgumentException(string filePath)
    {
        var act = async () => await _service.Import(filePath);

        act.Should().ThrowAsync<ArgumentException>();
    }
}
