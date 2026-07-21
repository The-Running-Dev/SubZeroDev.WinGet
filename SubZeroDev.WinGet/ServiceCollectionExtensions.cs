using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Com;

namespace SubZeroDev.WinGet;

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
