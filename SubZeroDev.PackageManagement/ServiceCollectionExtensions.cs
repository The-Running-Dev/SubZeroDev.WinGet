using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.PackageManagement.Abstractions;

namespace SubZeroDev.PackageManagement;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackageManagement(this IServiceCollection services)
    {
        services
            .AddSingleton<IWinGetClient, WinGetClient>()
            .AddSingleton<IPackageManagementService, PackageManagementService>();

        return services;
    }
}
