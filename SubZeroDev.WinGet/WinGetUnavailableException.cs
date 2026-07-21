namespace SubZeroDev.WinGet;

/// <summary>
/// Thrown when the WinGet COM server cannot be activated on this machine — typically because
/// WinGet (App Installer) is not installed, is too old for the pinned interop contract, or COM
/// activation is blocked in the current process context (e.g. some elevated/service hosts).
/// </summary>
public sealed class WinGetUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
