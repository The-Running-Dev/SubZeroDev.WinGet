using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SubZeroDev.WinGet.Abstractions;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// AddPackageManagement must register the full public surface as singletons. Constructing the
/// clients is COM-free (activation is deferred until first use), so resolution is safe here.
/// </summary>
[TestFixture]
public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider Build() =>
        new ServiceCollection()
            .AddLogging()
            .AddPackageManagement()
            .BuildServiceProvider();

    [Test]
    public void AddPackageManagement_RegistersTheFullPublicSurface()
    {
        using var services = Build();

        services.GetService<IWinGetClient>().Should().NotBeNull();
        services.GetService<IWinGetSourceClient>().Should().NotBeNull();
        services.GetService<IWinGetCliClient>().Should().NotBeNull();
        services.GetService<IPackageManagementService>().Should().NotBeNull();
        services.GetService<IPackageSourceService>().Should().NotBeNull();
    }

    [Test]
    public void AddPackageManagement_RegistersSingletons()
    {
        using var services = Build();

        services.GetRequiredService<IPackageManagementService>()
            .Should().BeSameAs(services.GetRequiredService<IPackageManagementService>());

        services.GetRequiredService<IWinGetClient>()
            .Should().BeSameAs(services.GetRequiredService<IWinGetClient>());
    }

    [Test]
    public void AddPackageManagement_ReturnsTheSameCollection_ForChaining()
    {
        var collection = new ServiceCollection();

        collection.AddPackageManagement().Should().BeSameAs(collection);
    }
}
