using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

/// <summary>Failure outcomes and parameter-variant paths for <see cref="PackageSourceService"/>.</summary>
[TestFixture]
public class PackageSourceServiceEdgeTests
{
    private Mock<IWinGetSourceClient> _sourceClient = null!;

    private PackageSourceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _sourceClient = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        _service = new PackageSourceService(_sourceClient.Object, NullLogger<PackageSourceService>.Instance);
    }

    [Test]
    public async Task AddSourceAsync_ReturnsFailureUnchanged()
    {
        var failure = SourceOperationResult.Failure("AccessDenied", unchecked((int)0x80070005));

        _sourceClient
            .Setup(c => c.AddSourceAsync(It.IsAny<AddPackageSourceRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.AddSourceAsync(new AddPackageSourceRequest("contoso", "https://contoso.example"));

        result.Should().Be(failure);
    }

    [Test]
    public async Task RemoveSourceAsync_PassesPreserveDataThrough()
    {
        _sourceClient
            .Setup(c => c.RemoveSourceAsync("contoso", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        (await _service.RemoveSourceAsync("contoso", preserveData: true)).Succeeded.Should().BeTrue();

        _sourceClient.VerifyAll();
    }

    [Test]
    public async Task RefreshSourceAsync_ReturnsFailureUnchanged()
    {
        var failure = SourceOperationResult.Failure("CatalogError");

        _sourceClient
            .Setup(c => c.RefreshSourceAsync("winget", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        (await _service.RefreshSourceAsync("winget")).Should().Be(failure);
    }

    [Test]
    public async Task UpdateSourceAsync_WithExplicitOnly_Delegates()
    {
        _sourceClient
            .Setup(c => c.UpdateSourceAsync("winget", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        (await _service.UpdateSourceAsync("winget", isExplicit: true)).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task UpdateSourceAsync_WithPriorityOnly_Delegates()
    {
        _sourceClient
            .Setup(c => c.UpdateSourceAsync("winget", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        (await _service.UpdateSourceAsync("winget", priority: 10)).Succeeded.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void RemoveRefreshUpdate_WithNoName_ThrowArgumentException(string? name)
    {
        var operations = new Func<Task>[]
        {
            () => _service.RemoveSourceAsync(name!),
            () => _service.RefreshSourceAsync(name!),
            () => _service.UpdateSourceAsync(name!, isExplicit: true),
        };

        foreach (var operation in operations)
        {
            operation.Should().ThrowAsync<ArgumentException>();
        }
    }
}
