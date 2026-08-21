namespace SubZeroDev.WinGet.Com;

internal enum WinGetActivationMode
{
    Projection,
    LocalServer,
    LocalServerLowerTrust
}

/// <summary>
/// Selects one WinGet activation mode for a factory and reuses it for that factory's lifetime.
/// </summary>
internal sealed class WinGetActivationModeSelector
{
    private static readonly WinGetActivationMode[] Modes =
    [
        WinGetActivationMode.Projection,
        WinGetActivationMode.LocalServer,
        WinGetActivationMode.LocalServerLowerTrust
    ];

    private readonly object _gate = new();

    private WinGetActivationMode? _mode;

    internal T Create<T>(Func<WinGetActivationMode, T> create)
    {
        lock (_gate)
        {
            if (_mode is { } mode)
            {
                return create(mode);
            }

            var failures = new List<Exception>();

            foreach (var candidate in Modes)
            {
                try
                {
                    var instance = create(candidate);
                    _mode = candidate;

                    return instance;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            throw new WinGetUnavailableException(
                "Failed to activate the WinGet COM server. Ensure WinGet (App Installer) is installed and up to date " +
                "on this machine. If running elevated or as a Windows service, the COM server may not be registered " +
                "for this context.",
                new AggregateException(failures));
        }
    }
}
