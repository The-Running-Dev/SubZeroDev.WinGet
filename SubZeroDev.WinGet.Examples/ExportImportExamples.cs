using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.WinGet.Abstractions;

namespace SubZeroDev.WinGet.Examples;

/// <summary>
/// Export/import examples — machine snapshot and restore. Like pins, these have no COM API and
/// run winget.exe behind <see cref="IWinGetCliClient"/>.
/// </summary>
public static class ExportImportExamples
{
    /// <summary>ExportAsync — snapshot installed packages to a "winget import"-compatible JSON file.</summary>
    public static async Task ExportAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var filePath = args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), $"winget-export-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.ExportAsync(filePath, includeVersions: true, ct);

        if (result.Succeeded)
        {
            Console.WriteLine($"Exported to {filePath} ({new FileInfo(filePath).Length:N0} bytes).");
        }
        else
        {
            // Export exits non-zero when some installed apps aren't available in any source —
            // the file is still written for everything it could map.
            Console.WriteLine($"Export finished with warnings ({result.ExitCodeHex}); file {(File.Exists(filePath) ? "was" : "was NOT")} written.");
        }
    }

    /// <summary>ImportAsync — install everything a previously exported file lists.</summary>
    public static async Task ImportAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: import <file> — INSTALLS every package in the file onto THIS machine.");

            return;
        }

        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.ImportAsync(args[0], ignoreUnavailable: true, ignoreVersions: false, ct);

        Console.WriteLine(result.Succeeded ? "Import completed." : $"Import failed ({result.ExitCodeHex}): {result.Error}");
    }
}
