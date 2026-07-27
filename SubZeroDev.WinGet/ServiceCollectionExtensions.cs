using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Com;

namespace SubZeroDev.WinGet;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackageManagement(this IServiceCollection services)
    {
        services
            // All projected objects share one MTA owner thread; the provider disposes this
            // singleton and therefore stops the dispatcher during provider disposal.
            .AddSingleton<WinGetComContext>()
            .AddSingleton<IWinGetClient>(provider => new WinGetClient(provider.GetRequiredService<WinGetComContext>()))
            .AddSingleton<IWinGetSourceClient>(provider => new WinGetSourceClient(provider.GetRequiredService<WinGetComContext>()))
            .AddSingleton<IWinGetCliClient, WinGetCliClient>()
            .AddSingleton<IPackageManagementService, PackageManagementService>()
            .AddSingleton<IPackageSourceService, PackageSourceService>();

        return services;
    }
}
