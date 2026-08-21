using FluentAssertions;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetClientOperationResultTests
{
    [Test]
    public void ToOperationResult_WhenExtendedErrorIsInstallCancelledByUser_ReturnsCancelled()
    {
        var extendedError = new InvalidOperationException("cancelled")
        {
            HResult = WinGetErrorCodes.InstallCancelledByUser
        };

        var result = WinGetClient.ToOperationResult(
            PackageOperationStatus.InstallError,
            rebootRequired: false,
            extendedError,
            statusDescription: "InstallError",
            installerErrorCode: null);

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(PackageOperationStatus.Cancelled);
        result.ExtendedErrorCode.Should().Be(WinGetErrorCodes.InstallCancelledByUser);
    }

    [TestCase((int)PackageOperationStatus.InstallError)]
    [TestCase((int)PackageOperationStatus.BlockedByPolicy)]
    [TestCase((int)PackageOperationStatus.CatalogError)]
    public void ToOperationResult_WithAnyOtherHResult_RetainsMappedStatusAndFailureData(int statusValue)
    {
        var status = (PackageOperationStatus)statusValue;
        var extendedError = new InvalidOperationException("failed")
        {
            HResult = unchecked((int)0x8A150019)
        };

        var result = WinGetClient.ToOperationResult(
            status,
            rebootRequired: false,
            extendedError,
            statusDescription: "SomeStatus",
            installerErrorCode: 42);

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(status);
        result.ExtendedErrorCode.Should().Be(unchecked((int)0x8A150019));
        result.ErrorMessage.Should().Be("SomeStatus");
        result.InstallerErrorCode.Should().Be((uint)42);
    }

    [Test]
    public void ToOperationResult_WithNoExtendedError_RetainsMappedStatus()
    {
        var result = WinGetClient.ToOperationResult(
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
        var result = WinGetClient.ToOperationResult(
            PackageOperationStatus.Ok,
            rebootRequired: true,
            extendedError: null,
            statusDescription: "Ok",
            installerErrorCode: null);

        result.Succeeded.Should().BeTrue();
        result.RebootRequired.Should().BeTrue();
    }

    [Test]
    public void ToOperationResult_RevertingTheHResultClassification_FailsThisRegression()
    {
        // Regression witness for S3.5: this asserts the override branch in
        // WinGetClient.ToOperationResult actually runs, not merely that Cancelled can be
        // constructed. Removing the InstallCancelledByUser check makes this fail because the
        // input status (InstallError) would flow through unchanged.
        var extendedError = new InvalidOperationException("cancelled")
        {
            HResult = WinGetErrorCodes.InstallCancelledByUser
        };

        var result = WinGetClient.ToOperationResult(
            PackageOperationStatus.InstallError,
            rebootRequired: false,
            extendedError,
            statusDescription: "InstallError",
            installerErrorCode: null);

        result.Status.Should().NotBe(PackageOperationStatus.InstallError);
        result.Status.Should().Be(PackageOperationStatus.Cancelled);
    }
}
