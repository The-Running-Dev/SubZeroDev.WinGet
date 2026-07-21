---
id: sources
title: Managing Sources
sidebar_position: 2
---

# Managing Sources

`IPackageSourceService` is the `winget source` equivalent, implemented via COM (not CLI parsing).

```csharp
var sources = services.GetRequiredService<IPackageSourceService>();
```

## Listing and reading sources

```csharp
var all = await sources.GetSources();

foreach (var s in all)
{
    Console.WriteLine($"{s.Name}: {s.Argument} ({s.Type}, trust: {s.TrustLevel}, priority: {s.Priority})");
}

var winget = await sources.GetSource("winget");   // null if not configured
```

## Adding a source

:::caution Elevation required
Adding and removing sources changes machine-wide state — WinGet returns `AccessDenied` unless the calling process is elevated.
:::

```csharp
using SubZeroDev.WinGet.Models;

var result = await sources.AddSource(new AddPackageSourceRequest("contoso", "https://pkg.contoso.com/source")
{
    Type = "Microsoft.PreIndexed.Package",   // or "Microsoft.Rest"
    TrustLevel = PackageSourceTrustLevel.None,
    IsExplicit = false,                       // true = hidden unless named explicitly
    Priority = 0,
},
progress: new Progress<double>(p => Console.WriteLine($"{p:F0}%")));
```

Source operations report percentage progress (0–100) rather than staged progress.

## Removing a source

```csharp
// preserveData: false mirrors "winget source remove" (registration + data removed)
// preserveData: true  mirrors "winget source reset"  (registration removed, data kept)
var result = await sources.RemoveSource("contoso", preserveData: false);
```

## Refreshing a source

Force the source's catalog data to update now:

```csharp
var result = await sources.RefreshSource("winget");
```

## Editing a source

Change the `Explicit` flag and/or `Priority` (higher priority sorts first):

```csharp
await sources.UpdateSource("contoso", isExplicit: true);
await sources.UpdateSource("contoso", priority: 10);
```

At least one of the two must be provided; passing neither throws `ArgumentException`.

## Results

Source operations return a `SourceOperationResult` with `Succeeded`, `ErrorMessage`, and the WinGet `ExtendedErrorCode` HRESULT.
