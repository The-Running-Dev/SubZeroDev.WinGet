using System.Reflection.PortableExecutable;
using Nuke.Common.IO;

static class PeArchitecture
{
    public static void AssertAnyCpu(AbsolutePath assembly)
    {
        using var stream = File.OpenRead(assembly);
        using var reader = new PEReader(stream);
        var headers = reader.PEHeaders;
        var corFlags = headers.CorHeader?.Flags
            ?? throw new InvalidOperationException($"{assembly} has no CLR header.");

        if (headers.CoffHeader.Machine != Machine.I386 ||
            headers.PEHeader?.Magic != PEMagic.PE32 ||
            !corFlags.HasFlag(CorFlags.ILOnly) ||
            corFlags.HasFlag(CorFlags.Requires32Bit) ||
            corFlags.HasFlag(CorFlags.Prefers32Bit))
        {
            throw new InvalidOperationException(
                $"{assembly} is not IL-only AnyCPU. Machine={headers.CoffHeader.Machine}, " +
                $"Magic={headers.PEHeader?.Magic}, CorFlags={corFlags}.");
        }
    }

    public static void AssertMachine(AbsolutePath assembly, Machine expected)
    {
        using var stream = File.OpenRead(assembly);
        using var reader = new PEReader(stream);
        var actual = reader.PEHeaders.CoffHeader.Machine;

        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"{assembly} has PE machine {actual} (0x{(ushort)actual:X4}); " +
                $"expected {expected} (0x{(ushort)expected:X4}).");
        }
    }
}
