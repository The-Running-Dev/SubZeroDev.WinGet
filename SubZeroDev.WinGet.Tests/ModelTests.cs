using FluentAssertions;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// The request records' defaults are part of the public contract: an empty request must
/// reproduce WinGet's own defaults (silent, any scope, agreements accepted).
/// </summary>
[TestFixture]
public class ModelTests
{
    [Test]
    public void InstallRequest_Defaults_MatchWinGetDefaults()
    {
        var request = new InstallRequest();

        request.Version.Should().BeNull();
        request.Scope.Should().Be(PackageScope.Any);
        request.Mode.Should().Be(PackageOperationMode.Silent);
        request.Architecture.Should().Be(PackageArchitecture.Default);
        request.InstallerType.Should().Be(PackageInstallerKind.Default);
        request.PreferredInstallLocation.Should().BeNull();
        request.LogOutputPath.Should().BeNull();
        request.OverrideArguments.Should().BeNull();
        request.AdditionalArguments.Should().BeNull();
        request.Force.Should().BeFalse();
        request.AllowHashMismatch.Should().BeFalse();
        request.SkipDependencies.Should().BeFalse();
        request.AllowUpgradeToUnknownVersion.Should().BeFalse();
        request.AcceptPackageAgreements.Should().BeTrue();
        request.CorrelationData.Should().BeNull();
    }

    [Test]
    public void InstallRequest_With_ProducesModifiedCopy_WithoutMutatingOriginal()
    {
        var original = new InstallRequest { Scope = PackageScope.User };

        var modified = original with { Scope = PackageScope.System, Force = true };

        original.Scope.Should().Be(PackageScope.User);
        original.Force.Should().BeFalse();
        modified.Scope.Should().Be(PackageScope.System);
        modified.Force.Should().BeTrue();
    }

    [Test]
    public void UninstallRequest_Defaults_AreSilentAnyScope()
    {
        var request = new UninstallRequest();

        request.Mode.Should().Be(PackageOperationMode.Silent);
        request.Scope.Should().Be(PackageScope.Any);
        request.Force.Should().BeFalse();
        request.LogOutputPath.Should().BeNull();
        request.CorrelationData.Should().BeNull();
    }

    [Test]
    public void DownloadRequest_RequiresDirectory_AndDefaultsMatchWinGet()
    {
        var request = new DownloadRequest(@"C:\Downloads");

        request.DownloadDirectory.Should().Be(@"C:\Downloads");
        request.Version.Should().BeNull();
        request.Architecture.Should().Be(PackageArchitecture.Default);
        request.InstallerType.Should().Be(PackageInstallerKind.Default);
        request.Scope.Should().Be(PackageScope.Any);
        request.Locale.Should().BeNull();
        request.AllowHashMismatch.Should().BeFalse();
        request.SkipDependencies.Should().BeFalse();
        request.SkipMicrosoftStoreLicense.Should().BeFalse();
        request.AcceptPackageAgreements.Should().BeTrue();
    }

    [Test]
    public void RepairRequest_Defaults_AreSilentAnyScope()
    {
        var request = new RepairRequest();

        request.Mode.Should().Be(PackageOperationMode.Silent);
        request.Scope.Should().Be(PackageScope.Any);
        request.Force.Should().BeFalse();
        request.AllowHashMismatch.Should().BeFalse();
        request.AcceptPackageAgreements.Should().BeTrue();
        request.LogOutputPath.Should().BeNull();
        request.CorrelationData.Should().BeNull();
    }

    [Test]
    public void AddPackageSourceRequest_Defaults_ArePreIndexedUntrusted()
    {
        var request = new AddPackageSourceRequest("contoso", "https://contoso.example/source");

        request.Name.Should().Be("contoso");
        request.Uri.Should().Be("https://contoso.example/source");
        request.Type.Should().Be("Microsoft.PreIndexed.Package");
        request.TrustLevel.Should().Be(PackageSourceTrustLevel.None);
        request.CustomHeader.Should().BeNull();
        request.IsExplicit.Should().BeFalse();
        request.Priority.Should().Be(0);
    }

    [Test]
    public void SourceOperationResult_SuccessAndFailure_FactoryMethods()
    {
        SourceOperationResult.Success().Should().Be(new SourceOperationResult(true, null, null));
        SourceOperationResult.Failure("boom", 42).Should().Be(new SourceOperationResult(false, "boom", 42));
    }

    [TestCase(0, "0x00000000")]
    [TestCase(-1978335209, "0x8A150017")]
    [TestCase(1, "0x00000001")]
    public void CliOperationResult_ExitCodeHex_FormatsAsHResult(int exitCode, string expected)
    {
        new CliOperationResult(exitCode == 0, exitCode, "", "").ExitCodeHex.Should().Be(expected);
    }

    [Test]
    public void PackageOperationProgress_CarriesStatePercentAndMessage()
    {
        var progress = new PackageOperationProgress(PackageOperationState.Downloading, 42.5, "Downloading");

        progress.State.Should().Be(PackageOperationState.Downloading);
        progress.PercentComplete.Should().Be(42.5);
        progress.StatusMessage.Should().Be("Downloading");
    }

    [Test]
    public void PackageDetails_Records_SupportValueEquality()
    {
        var agreement = new PackageAgreementInfo("EULA", "text", "https://example.com/eula");
        var doc = new PackageDocumentation("Manual", "https://example.com/docs");
        var icon = new PackageIconInfo("https://example.com/icon.png", "Png", "Square32", "Default");

        agreement.Should().Be(new PackageAgreementInfo("EULA", "text", "https://example.com/eula"));
        doc.Should().Be(new PackageDocumentation("Manual", "https://example.com/docs"));
        icon.Should().Be(new PackageIconInfo("https://example.com/icon.png", "Png", "Square32", "Default"));
    }

    [Test]
    public void PackageSource_CarriesAllMetadata()
    {
        var updated = DateTimeOffset.UtcNow;
        var source = new PackageSource("id", "winget", "Microsoft.PreIndexed.Package", "https://cdn", updated, PackageSourceOrigin.Predefined, PackageSourceTrustLevel.Trusted, false, 5);

        source.Id.Should().Be("id");
        source.Name.Should().Be("winget");
        source.LastUpdated.Should().Be(updated);
        source.Origin.Should().Be(PackageSourceOrigin.Predefined);
        source.TrustLevel.Should().Be(PackageSourceTrustLevel.Trusted);
        source.IsExplicit.Should().BeFalse();
        source.Priority.Should().Be(5);
    }
}
