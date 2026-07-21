using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.WinGet.Abstractions;

namespace SubZeroDev.WinGet.Examples;

/// <summary>
/// Pin management examples. Pins have NO COM API at any WinGet contract version — these run
/// winget.exe behind <see cref="IWinGetCliClient"/>, the library's single deliberate CLI shim.
/// </summary>
public static class PinExamples
{
    /// <summary>GetPinsAsync — the "winget pin list" equivalent.</summary>
    public static async Task ListAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var packages = services.GetRequiredService<IPackageManagementService>();

        var pins = await packages.GetPinsAsync(ct);

        Console.WriteLine($"{pins.Count} pin(s):");

        foreach (var pin in pins)
        {
            Console.WriteLine($"  {pin.Id,-45} {pin.Version,-15} {pin.Kind} [{pin.Source}]");
        }
    }

    /// <summary>
    /// PinAsync — three pin kinds: plain (skipped by "upgrade all"), gating (version pattern
    /// like 1.2.*), and blocking (cannot upgrade until unpinned).
    /// </summary>
    public static async Task AddAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: pin <package-id> [version] [--blocking] — e.g. pin Git.Git 2.44.* ");

            return;
        }

        var version = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
        var blocking = args.Contains("--blocking");
        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.PinAsync(args[0], version, blocking, ct);

        Console.WriteLine(result.Succeeded ? "Pinned." : $"Pin failed ({result.ExitCodeHex}): {result.Error}");
    }

    /// <summary>UnpinAsync.</summary>
    public static async Task RemoveAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: unpin <package-id>");

            return;
        }

        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.UnpinAsync(args[0], ct);

        Console.WriteLine(result.Succeeded ? "Unpinned." : $"Unpin failed ({result.ExitCodeHex}): {result.Error}");
    }
}
