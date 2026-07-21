using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.PackageManagement.Abstractions;
using SubZeroDev.PackageManagement.Com;

namespace SubZeroDev.PackageManagement;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackageManagement(this IServiceCollection services)
    {
        // One shared factory so every COM object uses the same resolved activation mode.
        var factory = new WinGetFactory();

        services
            .AddSingleton<IWinGetClient>(_ => new WinGetClient(factory))
            .AddSingleton<IWinGetSourceClient>(_ => new WinGetSourceClient(factory))
            .AddSingleton<IWinGetCliClient, WinGetCliClient>()
            .AddSingleton<IPackageManagementService, PackageManagementService>()
            .AddSingleton<IPackageSourceService, PackageSourceService>();

        return services;
    }
}
