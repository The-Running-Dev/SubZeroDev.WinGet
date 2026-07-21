using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Examples;

/// <summary>
/// Examples for <see cref="IPackageSourceService"/> — the "winget source" equivalent, via COM.
/// Add/remove require an elevated process (WinGet returns AccessDenied otherwise).
/// </summary>
public static class SourceExamples
{
    /// <summary>GetSources — every configured source with its metadata.</summary>
    public static async Task List(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var sources = services.GetRequiredService<IPackageSourceService>();

        var all = await sources.GetSources(ct);

        Console.WriteLine($"{all.Count} configured source(s):");

        foreach (var s in all)
        {
            Console.WriteLine($"  {s.Name,-12} {s.Type,-28} {s.Argument} (origin: {s.Origin}, trust: {s.TrustLevel}, explicit: {s.IsExplicit}, priority: {s.Priority}, updated: {s.LastUpdated:u})");
        }
    }

    /// <summary>GetSource — one source by name.</summary>
    public static async Task Get(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var name = args.FirstOrDefault() ?? "winget";
        var sources = services.GetRequiredService<IPackageSourceService>();

        var source = await sources.GetSource(name, ct);

        Console.WriteLine(source is null ? $"Source '{name}' is not configured." : $"{source.Name}: {source.Argument} ({source.Type})");
    }

    /// <summary>AddSource — register a new source. Elevation required.</summary>
    public static async Task Add(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: source-add <name> <uri> — registers a source machine-wide (elevated).");

            return;
        }

        var sources = services.GetRequiredService<IPackageSourceService>();

        var request = new AddPackageSourceRequest(args[0], args[1])
        {
            Type = "Microsoft.PreIndexed.Package", // or "Microsoft.Rest" for REST sources
            TrustLevel = PackageSourceTrustLevel.None,
            IsExplicit = false,                     // true = hidden unless named explicitly
            Priority = 0,
        };

        // Source operations report percentage progress (0-100) rather than staged progress.
        var result = await sources.AddSource(request, new Progress<double>(p => Console.WriteLine($"  {p:F0}%")), ct);

        Console.WriteLine(result.Succeeded ? "Source added." : $"Add failed: {result.ErrorMessage} (0x{result.ExtendedErrorCode ?? 0:X8})");
    }

    /// <summary>RemoveSource — preserveData:false mirrors "winget source remove"; true mirrors "reset".</summary>
    public static async Task Remove(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: source-remove <name> — unregisters a source machine-wide (elevated).");

            return;
        }

        var sources = services.GetRequiredService<IPackageSourceService>();

        var result = await sources.RemoveSource(args[0], preserveData: false, new Progress<double>(p => Console.WriteLine($"  {p:F0}%")), ct);

        Console.WriteLine(result.Succeeded ? "Source removed." : $"Remove failed: {result.ErrorMessage} (0x{result.ExtendedErrorCode ?? 0:X8})");
    }

    /// <summary>RefreshSource — force the source's catalog data to update now.</summary>
    public static async Task Refresh(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: source-refresh <name>");

            return;
        }

        var sources = services.GetRequiredService<IPackageSourceService>();

        var result = await sources.RefreshSource(args[0], new Progress<double>(p => Console.WriteLine($"  {p:F0}%")), ct);

        Console.WriteLine(result.Succeeded ? "Source refreshed." : $"Refresh failed: {result.ErrorMessage} (0x{result.ExtendedErrorCode ?? 0:X8})");
    }

    /// <summary>UpdateSource — edit the Explicit flag and/or Priority of a source.</summary>
    public static async Task Edit(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length < 3 || (args[1] is not ("explicit" or "priority")))
        {
            Console.WriteLine("usage: source-edit <name> explicit <true|false> | source-edit <name> priority <int>");

            return;
        }

        var sources = services.GetRequiredService<IPackageSourceService>();

        var result = args[1] == "explicit"
            ? await sources.UpdateSource(args[0], isExplicit: bool.Parse(args[2]), priority: null, ct)
            : await sources.UpdateSource(args[0], isExplicit: null, priority: int.Parse(args[2]), ct);

        Console.WriteLine(result.Succeeded ? "Source updated." : $"Edit failed: {result.ErrorMessage} (0x{result.ExtendedErrorCode ?? 0:X8})");
    }
}
