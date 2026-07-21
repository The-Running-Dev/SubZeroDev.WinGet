using FluentAssertions;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class PackageOperationResultTests
{
    [Test]
    public void Success_ProducesSucceededResult_WithNoError()
    {
        var result = PackageOperationResult.Success();

        result.Succeeded.Should().BeTrue();
        result.Status.Should().Be(PackageOperationStatus.Ok);
        result.ErrorMessage.Should().BeNull();
        result.ExtendedErrorCode.Should().BeNull();
        result.InstallerErrorCode.Should().BeNull();
        result.RebootRequired.Should().BeFalse();
    }

    [Test]
    public void Success_WithRebootRequired_CarriesTheFlagThrough()
    {
        var result = PackageOperationResult.Success(rebootRequired: true);

        result.Succeeded.Should().BeTrue();
        result.RebootRequired.Should().BeTrue();
    }

    [Test]
    public void Failure_ProducesUnsucceededResult_WithStatusMessageAndCodes()
    {
        var result = PackageOperationResult.Failure(PackageOperationStatus.InstallError, "InstallError", -2147023293, 1603);

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(PackageOperationStatus.InstallError);
        result.ErrorMessage.Should().Be("InstallError");
        result.ExtendedErrorCode.Should().Be(-2147023293);
        result.InstallerErrorCode.Should().Be(1603);
        result.RebootRequired.Should().BeFalse();
    }
}
