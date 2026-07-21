using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class PackageSourceServiceTests
{
    private Mock<IWinGetSourceClient> _sourceClient = null!;

    private PackageSourceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _sourceClient = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        _service = new PackageSourceService(_sourceClient.Object, NullLogger<PackageSourceService>.Instance);
    }

    private static PackageSource MakeSource(string name = "winget") =>
        new("Microsoft.Winget.Source_8wekyb3d8bbwe", name, "Microsoft.PreIndexed.Package", "https://cdn.winget.microsoft.com/cache", DateTimeOffset.UtcNow, PackageSourceOrigin.Predefined, PackageSourceTrustLevel.Trusted, false, 0);

    [Test]
    public async Task GetSources_DelegatesToClient()
    {
        var expected = new List<PackageSource> { MakeSource(), MakeSource("msstore") };

        _sourceClient
            .Setup(c => c.GetSources(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetSources();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetSource_DelegatesToClient()
    {
        var expected = MakeSource();

        _sourceClient
            .Setup(c => c.GetSource("winget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetSource("winget");

        result.Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void GetSource_WithNoName_ThrowsArgumentException(string? name)
    {
        var act = async () => await _service.GetSource(name!);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task AddSource_DelegatesToClient()
    {
        var request = new AddPackageSourceRequest("contoso", "https://contoso.example/source");

        _sourceClient
            .Setup(c => c.AddSource(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.AddSource(request);

        result.Succeeded.Should().BeTrue();
        _sourceClient.VerifyAll();
    }

    [Test]
    public void AddSource_WithNoUri_ThrowsArgumentException()
    {
        var act = async () => await _service.AddSource(new AddPackageSourceRequest("contoso", ""));

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task RemoveSource_DelegatesToClient()
    {
        _sourceClient
            .Setup(c => c.RemoveSource("contoso", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.RemoveSource("contoso", preserveData: true);

        result.Succeeded.Should().BeTrue();
        _sourceClient.VerifyAll();
    }

    [Test]
    public async Task RefreshSource_DelegatesToClient()
    {
        _sourceClient
            .Setup(c => c.RefreshSource("winget", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.RefreshSource("winget");

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void UpdateSource_WithNothingToChange_ThrowsArgumentException()
    {
        var act = async () => await _service.UpdateSource("winget");

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task UpdateSource_DelegatesToClient()
    {
        _sourceClient
            .Setup(c => c.UpdateSource("winget", true, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.UpdateSource("winget", isExplicit: true, priority: 5);

        result.Succeeded.Should().BeTrue();
        _sourceClient.VerifyAll();
    }
}
