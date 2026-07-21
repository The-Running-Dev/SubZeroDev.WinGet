using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SubZeroDev.WinGet;
using SubZeroDev.WinGet.Examples;

// Composition root: AddPackageManagement() registers IPackageManagementService,
// IPackageSourceService, and the lower-level IWinGetClient/IWinGetSourceClient/IWinGetCliClient.
var services = new ServiceCollection()
    .AddLogging(logging => logging
        .AddSimpleConsole(console => console.SingleLine = true)
        .SetMinimumLevel(LogLevel.Information))
    .AddPackageManagement()
    .BuildServiceProvider();

// Ctrl+C cancels the in-flight operation instead of killing the process — every API in the
// library accepts a CancellationToken, and in-flight installs/downloads honor it.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Cancelling...");
};

return await ExampleRunner.Run(services, args, cts.Token);
