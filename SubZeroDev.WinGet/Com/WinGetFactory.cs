using System.Runtime.InteropServices;

using Microsoft.Management.Deployment;

using WinRT;

namespace SubZeroDev.WinGet.Com;

/// <summary>
/// Creates WinGet COM objects with a resilient activation chain. Standard WinRT projection
/// activation (new PackageManager()) is known to fail in some elevated/service process contexts,
/// so this factory falls back to raw CoCreateInstance against the out-of-proc COM server, first
/// with the standard context and then with CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION — the same
/// mitigation the WinGet PowerShell module and UniGetUI use. The first mode that succeeds is
/// cached and reused for every subsequent object so all COM objects share one activation context.
/// </summary>
internal sealed class WinGetFactory
{
    private enum ActivationMode
    {
        Unresolved,
        Projection,
        LocalServer,
        LocalServerLowerTrust
    }

    private const uint ClsctxLocalServer = 0x4;

    private const uint ClsctxAllowLowerTrustRegistration = 0x4000000;

    // Out-of-proc production CLSIDs, from winget-cli's own
    // src/Microsoft.Management.Deployment.Projection/ClassesDefinition.cs.
    private static readonly IReadOnlyDictionary<Type, Guid> Clsids = new Dictionary<Type, Guid>
    {
        [typeof(PackageManager)] = new("C53A4F16-787E-42A4-B304-29EFFB4BF597"),
        [typeof(FindPackagesOptions)] = new("572DED96-9C60-4526-8F92-EE7D91D38C1A"),
        [typeof(CreateCompositePackageCatalogOptions)] = new("526534B8-7E46-47C8-8416-B1685C327D37"),
        [typeof(InstallOptions)] = new("1095F097-EB96-453B-B4E6-1613637F3B14"),
        [typeof(UninstallOptions)] = new("E1D9A11E-9F85-4D87-9C17-2B93143ADB8D"),
        [typeof(DownloadOptions)] = new("4CBABE76-7322-4BE4-9CEA-2589A80682DC"),
        [typeof(RepairOptions)] = new("0498F441-3097-455F-9CAF-148F28293865"),
        [typeof(PackageMatchFilter)] = new("D02C9DAF-99DC-429C-B503-4E504E4AB000"),
        [typeof(AuthenticationArguments)] = new("BA580786-BDE3-4F6C-B8F3-44698AC8711A"),
        [typeof(AddPackageCatalogOptions)] = new("DB9D012D-00D7-47EE-8FB1-606E10AC4F51"),
        [typeof(RemovePackageCatalogOptions)] = new("032B1C58-B975-469B-A013-E632B6ECE8D8"),
        [typeof(EditPackageCatalogOptions)] = new("A9F5E736-68CE-463C-BA6D-DE968F0CCE04")
    };

    // The projection's default interfaces (IPackageManager, IInstallOptions, …) are internal in
    // Microsoft.WindowsPackageManager.ComInterop, so their IIDs are resolved by reflection from
    // the projected class's own assembly ("I" + class name is the WinRT default interface).
    private static Guid GetIid(Type projectedClass) =>
        projectedClass.Assembly.GetType($"{projectedClass.Namespace}.I{projectedClass.Name}")?.GUID
            ?? throw new InvalidOperationException($"Could not resolve the default interface IID for {projectedClass.Name}.");

    private static readonly IReadOnlyDictionary<Type, Func<object>> ProjectionActivators = new Dictionary<Type, Func<object>>
    {
        [typeof(PackageManager)] = () => new PackageManager(),
        [typeof(FindPackagesOptions)] = () => new FindPackagesOptions(),
        [typeof(CreateCompositePackageCatalogOptions)] = () => new CreateCompositePackageCatalogOptions(),
        [typeof(InstallOptions)] = () => new InstallOptions(),
        [typeof(UninstallOptions)] = () => new UninstallOptions(),
        [typeof(DownloadOptions)] = () => new DownloadOptions(),
        [typeof(RepairOptions)] = () => new RepairOptions(),
        [typeof(PackageMatchFilter)] = () => new PackageMatchFilter(),
        [typeof(AuthenticationArguments)] = () => new AuthenticationArguments(),
        [typeof(AddPackageCatalogOptions)] = () => new AddPackageCatalogOptions(),
        [typeof(RemovePackageCatalogOptions)] = () => new RemovePackageCatalogOptions(),
        [typeof(EditPackageCatalogOptions)] = () => new EditPackageCatalogOptions()
    };

    private readonly object _gate = new();

    private ActivationMode _mode = ActivationMode.Unresolved;

    public PackageManager CreatePackageManager() => Create<PackageManager>();

    public FindPackagesOptions CreateFindPackagesOptions() => Create<FindPackagesOptions>();

    public CreateCompositePackageCatalogOptions CreateCompositeCatalogOptions() => Create<CreateCompositePackageCatalogOptions>();

    public InstallOptions CreateInstallOptions() => Create<InstallOptions>();

    public UninstallOptions CreateUninstallOptions() => Create<UninstallOptions>();

    public DownloadOptions CreateDownloadOptions() => Create<DownloadOptions>();

    public RepairOptions CreateRepairOptions() => Create<RepairOptions>();

    public PackageMatchFilter CreatePackageMatchFilter() => Create<PackageMatchFilter>();

    public AddPackageCatalogOptions CreateAddPackageCatalogOptions() => Create<AddPackageCatalogOptions>();

    public RemovePackageCatalogOptions CreateRemovePackageCatalogOptions() => Create<RemovePackageCatalogOptions>();

    public EditPackageCatalogOptions CreateEditPackageCatalogOptions() => Create<EditPackageCatalogOptions>();

    private T Create<T>() where T : class
    {
        lock (_gate)
        {
            if (_mode != ActivationMode.Unresolved)
            {
                return CreateWithMode<T>(_mode);
            }

            var failures = new List<Exception>();

            foreach (var mode in new[] { ActivationMode.Projection, ActivationMode.LocalServer, ActivationMode.LocalServerLowerTrust })
            {
                try
                {
                    var instance = CreateWithMode<T>(mode);
                    _mode = mode;

                    return instance;
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            throw new WinGetUnavailableException(
                "Failed to activate the WinGet COM server. Ensure WinGet (App Installer) is installed and up to date " +
                "on this machine. If running elevated or as a Windows service, the COM server may not be registered " +
                "for this context.",
                new AggregateException(failures));
        }
    }

    private static T CreateWithMode<T>(ActivationMode mode) where T : class
    {
        if (mode == ActivationMode.Projection)
        {
            return (T)ProjectionActivators[typeof(T)]();
        }

        var clsctx = ClsctxLocalServer;

        if (mode == ActivationMode.LocalServerLowerTrust)
        {
            clsctx |= ClsctxAllowLowerTrustRegistration;
        }

        var clsid = Clsids[typeof(T)];
        var iid = GetIid(typeof(T));

        var hr = CoCreateInstance(in clsid, IntPtr.Zero, clsctx, in iid, out var instance);

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            return MarshalGeneric<T>.FromAbi(instance);
        }
        finally
        {
            if (instance != IntPtr.Zero)
            {
                Marshal.Release(instance);
            }
        }
    }

    [DllImport("api-ms-win-core-com-l1-1-0.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CoCreateInstance(in Guid clsid, IntPtr pUnkOuter, uint dwClsContext, in Guid iid, out IntPtr instance);
}
