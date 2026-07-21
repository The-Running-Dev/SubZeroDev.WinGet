namespace SubZeroDev.WinGet.Examples;

/// <summary>
/// Dispatches <c>dotnet run -- &lt;example&gt; [args]</c> to the matching example. Read-only
/// examples run immediately against the live WinGet catalog; examples that would mutate the
/// machine require explicit arguments and never run by accident.
/// </summary>
public static class ExampleRunner
{
    private sealed record Example(
        string Name,
        string Usage,
        string Description,
        bool Mutating,
        Func<IServiceProvider, string[], CancellationToken, Task> Action);

    private static readonly Example[] Examples =
    [
        // Read-only — safe to run as-is.
        new("version", "version", "Report the installed WinGet version", false, PackageExamples.Version),
        new("search", "search [query]", "Search all sources (default query: vscode)", false, PackageExamples.Search),
        new("search-source", "search-source [query] [source]", "Search one source only (defaults: vscode, winget)", false, PackageExamples.SearchSingleSource),
        new("installed", "installed", "List installed packages with update state", false, PackageExamples.Installed),
        new("upgrades", "upgrades", "List packages with an available upgrade", false, PackageExamples.Upgrades),
        new("get", "get [package-id]", "Get one package by exact id (default: Microsoft.VisualStudioCode)", false, PackageExamples.Get),
        new("details", "details [package-id]", "Full manifest metadata for a package (default: Microsoft.VisualStudioCode)", false, PackageExamples.Details),
        new("sources", "sources", "List configured sources", false, SourceExamples.List),
        new("source-get", "source-get [name]", "Get one source by name (default: winget)", false, SourceExamples.Get),
        new("pins", "pins", "List package pins (CLI-backed; no COM equivalent)", false, PinExamples.List),
        new("export", "export [file]", "Export installed packages to an importable JSON file (default: %TEMP%)", false, ExportImportExamples.Export),
        new("low-level", "low-level [query]", "Use IWinGetClient directly, bypassing the service layer's retry policy", false, PackageExamples.LowLevel),

        // Mutating — require explicit arguments; nothing runs by accident.
        new("install", "install <package-id> [version]", "Install a package (shows progress reporting and full options)", true, PackageExamples.Install),
        new("update", "update <package-id>", "Upgrade an installed package", true, PackageExamples.Update),
        new("uninstall", "uninstall <package-id>", "Uninstall a package", true, PackageExamples.Uninstall),
        new("download", "download <package-id> [directory]", "Download a package's installer without installing", true, PackageExamples.Download),
        new("repair", "repair <package-id>", "Repair an installed package (needs winget >= 1.7)", true, PackageExamples.Repair),
        new("pin", "pin <package-id> [version] [--blocking]", "Pin a package (optionally gate to a version, e.g. 1.2.*)", true, PinExamples.Add),
        new("unpin", "unpin <package-id>", "Remove a package pin", true, PinExamples.Remove),
        new("import", "import <file>", "Install every package listed in an exported JSON file", true, ExportImportExamples.Import),
        new("source-add", "source-add <name> <uri>", "Register a new source (requires elevation)", true, SourceExamples.Add),
        new("source-remove", "source-remove <name>", "Unregister a source (requires elevation)", true, SourceExamples.Remove),
        new("source-refresh", "source-refresh <name>", "Force a source's catalog data to update", true, SourceExamples.Refresh),
        new("source-edit", "source-edit <name> <explicit|priority> <value>", "Edit a source's Explicit flag or Priority", true, SourceExamples.Edit),
    ];

    public static async Task<int> Run(IServiceProvider services, string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            PrintUsage();

            return 0;
        }

        var example = Examples.FirstOrDefault(e => e.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase));

        if (example is null)
        {
            Console.WriteLine($"Unknown example '{args[0]}'.");
            PrintUsage();

            return 1;
        }

        try
        {
            await example.Action(services, args.Skip(1).ToArray(), cancellationToken);

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled.");

            return 2;
        }
        catch (WinGetUnavailableException ex)
        {
            // The typed "WinGet isn't usable here" failure — missing App Installer, too-old
            // WinGet, or COM activation blocked in this process context.
            Console.WriteLine($"WinGet is unavailable: {ex.Message}");

            return 3;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("SubZeroDev.WinGet examples — one per public API.");
        Console.WriteLine();
        Console.WriteLine("usage: dotnet run -- <example> [args]");
        Console.WriteLine();
        Console.WriteLine("Read-only (run live against this machine's WinGet, change nothing):");

        foreach (var e in Examples.Where(e => !e.Mutating))
        {
            Console.WriteLine($"  {e.Usage,-46} {e.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("Mutating (CHANGE THIS MACHINE — required arguments are deliberate):");

        foreach (var e in Examples.Where(e => e.Mutating))
        {
            Console.WriteLine($"  {e.Usage,-46} {e.Description}");
        }
    }
}
