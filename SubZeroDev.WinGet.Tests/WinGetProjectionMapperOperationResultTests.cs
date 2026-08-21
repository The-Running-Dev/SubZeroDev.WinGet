using FluentAssertions;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetProjectionMapperOperationResultTests
{
    [Test]
    public void ToOperationResult_WhenExtendedErrorIsInstallCancelledByUser_ReturnsCancelled()
    {
        var extendedError = new InvalidOperationException("cancelled")
        {
            HResult = WinGetErrorCodes.InstallCancelledByUser
        };

        var result = WinGetProjectionMapper.ToOperationResult(
            PackageOperationStatus.InstallError,
            rebootRequired: false,
            extendedError,
            statusDescription: "InstallError",
            installerErrorCode: null);

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(PackageOperationStatus.Cancelled);
        result.ExtendedErrorCode.Should().Be(WinGetErrorCodes.InstallCancelledByUser);
    }

    [TestCase(PackageOperationStatus.InstallError)]
    [TestCase(PackageOperationStatus.BlockedByPolicy)]
    [TestCase(PackageOperationStatus.CatalogError)]
    public void ToOperationResult_WithAnyOtherHResult_RetainsMappedStatusAndFailureData(PackageOperationStatus status)
    {
        var extendedError = new InvalidOperationException("failed")
        {
            HResult = WinGetErrorCodes.CommandRequiresAdmin
        };

        var result = WinGetProjectionMapper.ToOperationResult(
            status,
            rebootRequired: false,
            extendedError,
            statusDescription: "SomeStatus",
            installerErrorCode: 42);

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(status);
        result.ExtendedErrorCode.Should().Be(WinGetErrorCodes.CommandRequiresAdmin);
        result.ErrorMessage.Should().Be("SomeStatus");
        result.InstallerErrorCode.Should().Be((uint)42);
    }

    [Test]
    public void ToOperationResult_WithNoExtendedError_RetainsMappedStatus()
    {
        var result = WinGetProjectionMapper.ToOperationResult(
            PackageOperationStatus.InstallError,
            rebootRequired: false,
            extendedError: null,
            statusDescription: "InstallError",
            installerErrorCode: null);

        result.Status.Should().Be(PackageOperationStatus.InstallError);
        result.ExtendedErrorCode.Should().BeNull();
    }

    [Test]
    public void ToOperationResult_WhenStatusIsOk_ReturnsSuccessRegardlessOfExtendedError()
    {
        // C15 and S3.1 scope the cancelled classification to a *failed* operation, so an Ok
        // status stays successful even when an extended error carries the cancelled HRESULT —
        // dropping the guard would also discard RebootRequired, which Failure never carries.
        var extendedError = new InvalidOperationException("cancelled")
        {
            HResult = WinGetErrorCodes.InstallCancelledByUser
        };

        var result = WinGetProjectionMapper.ToOperationResult(
            PackageOperationStatus.Ok,
            rebootRequired: true,
            extendedError,
            statusDescription: "Ok",
            installerErrorCode: null);

        result.Succeeded.Should().BeTrue();
        result.Status.Should().Be(PackageOperationStatus.Ok);
        result.RebootRequired.Should().BeTrue();

        var withoutExtendedError = WinGetProjectionMapper.ToOperationResult(
            PackageOperationStatus.Ok,
            rebootRequired: true,
            extendedError: null,
            statusDescription: "Ok",
            installerErrorCode: null);

        withoutExtendedError.Should().Be(result);
    }

    [Test]
    public void ToOperationResult_RevertingTheHResultClassification_FailsThisRegression()
    {
        // Regression witness for S3.5: this asserts the override branch in
        // WinGetProjectionMapper.ToOperationResult actually runs, not merely that Cancelled can be
        // constructed. Removing the InstallCancelledByUser check makes this fail because the
        // input status (InstallError) would flow through unchanged.
        var extendedError = new InvalidOperationException("cancelled")
        {
            HResult = WinGetErrorCodes.InstallCancelledByUser
        };

        var result = WinGetProjectionMapper.ToOperationResult(
            PackageOperationStatus.InstallError,
            rebootRequired: false,
            extendedError,
            statusDescription: "InstallError",
            installerErrorCode: null);

        result.Status.Should().NotBe(PackageOperationStatus.InstallError);
        result.Status.Should().Be(PackageOperationStatus.Cancelled);
    }
}
