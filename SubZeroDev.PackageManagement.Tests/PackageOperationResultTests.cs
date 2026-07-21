using FluentAssertions;

using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement.Tests;

[TestFixture]
public class PackageOperationResultTests
{
    [Test]
    public void Success_ProducesSucceededResult_WithNoError()
    {
        var result = PackageOperationResult.Success();

        result.Succeeded.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ExtendedErrorCode.Should().BeNull();
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
    public void Failure_ProducesUnsucceededResult_WithMessageAndCode()
    {
        var result = PackageOperationResult.Failure("InstallError", -2147023293);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("InstallError");
        result.ExtendedErrorCode.Should().Be(-2147023293);
        result.RebootRequired.Should().BeFalse();
    }
}
