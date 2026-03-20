namespace MacDotNet.Disk;

using static MacDotNet.Disk.NativeMethods;

internal sealed class SmartGeneric : ISmartGeneric, IDisposable
{
    private const int SmartDataSize = 512;
    private const int MaxAttributes = 30;
    private const int TableOffset = 2;
    private const int EntrySize = 12;

#pragma warning disable SA1117
    // kIOATASMARTUserClientTypeID
    private static readonly IntPtr PluginTypeUuid = CFUUIDGetConstantUUIDWithBytes(
        IntPtr.Zero,
        0x24, 0x51, 0x4B, 0x7A, 0x28, 0x04, 0x11, 0xD6,
        0x8A, 0x02, 0x00, 0x30, 0x65, 0x70, 0x48, 0x66);

    // IOCFPlugInInterface
    private static readonly IntPtr CfPluginUuid = CFUUIDGetConstantUUIDWithBytes(
        IntPtr.Zero,
        0xC2, 0x44, 0xE8, 0x58, 0x10, 0x9C, 0x11, 0xD4,
        0x91, 0xD4, 0x00, 0x50, 0xE4, 0xC6, 0x42, 0x6F);

    // kIOATASMARTInterfaceID
    private static readonly CFUUIDBytes SmartUuid = new()
    {
        byte0 = 0x08, byte1 = 0xAB, byte2 = 0xE2, byte3 = 0x1C,
        byte4 = 0x20, byte5 = 0xD4, byte6 = 0x11, byte7 = 0xD6,
        byte8 = 0x8D, byte9 = 0xF6, byte10 = 0x00, byte11 = 0x03,
        byte12 = 0x93, byte13 = 0x5A, byte14 = 0x76, byte15 = 0xB2
    };
#pragma warning restore SA1117

    private readonly byte[] buffer = new byte[SmartDataSize];

    private IntPtr pluginInterface;

    private IntPtr smartInterface;

    public bool LastUpdate { get; private set; }

    internal unsafe SmartGeneric(uint service)
    {
        IntPtr ppPlugin;
        int score;
        var kr = IOCreatePlugInInterfaceForService(service, PluginTypeUuid, CfPluginUuid, &ppPlugin, &score);
        if ((kr != KERN_SUCCESS) || (ppPlugin == IntPtr.Zero))
        {
            return;
        }

        pluginInterface = ppPlugin;

        var vtable = *(IntPtr*)ppPlugin;
        var qiFn = (delegate* unmanaged<IntPtr, CFUUIDBytes, IntPtr*, int>)(*((IntPtr*)vtable + 1));

        var pSmartInterface = IntPtr.Zero;
        var hr = qiFn(ppPlugin, SmartUuid, &pSmartInterface);
        if ((hr != S_OK) || (pSmartInterface == IntPtr.Zero))
        {
            return;
        }

        // Enable SMART operations
        var smartVtable = *(IntPtr*)pSmartInterface;
        var enableFn = (delegate* unmanaged<IntPtr, byte, int>)(*(IntPtr*)((byte*)smartVtable + 40));
        kr = enableFn(pSmartInterface, 1);
        if (kr != KERN_SUCCESS)
        {
            ReleasePlugInInterface(pSmartInterface);
            return;
        }

        smartInterface = pSmartInterface;
    }

    public void Dispose()
    {
        if (smartInterface != IntPtr.Zero)
        {
            ReleasePlugInInterface(smartInterface);
            smartInterface = IntPtr.Zero;
        }

        if (pluginInterface != IntPtr.Zero)
        {
            ReleasePlugInInterface(pluginInterface);
            pluginInterface = IntPtr.Zero;
        }
    }

    public unsafe bool Update()
    {
        if (smartInterface == IntPtr.Zero)
        {
            LastUpdate = false;
            return false;
        }

        // Enable SMART operations
        // ATA SMART interface vtable layout (64-bit):
        //   0: _reserved, 8: QI, 16: AddRef, 24: Release
        //   32: version(2) + revision(2) + pad(4)
        //   40: SMARTEnableDisableOperations
        var smartVtable = *(IntPtr*)smartInterface;
        var readDataFn = (delegate* unmanaged<IntPtr, byte*, int>)(*(IntPtr*)((byte*)smartVtable + 72));

        fixed (byte* bufPtr = buffer)
        {
            var kr = readDataFn(smartInterface, bufPtr);
            LastUpdate = kr == KERN_SUCCESS;
            return LastUpdate;
        }
    }

    public IReadOnlyList<SmartId> GetSupportedIds()
    {
        var list = new List<SmartId>();

        for (var i = 0; i < MaxAttributes; i++)
        {
            var offset = TableOffset + (i * EntrySize);
            var id = buffer[offset];
            if (id != 0 && id != 0xff)
            {
                list.Add((SmartId)id);
            }
        }

        return list;
    }

    public SmartAttribute? GetAttribute(SmartId id)
    {
        var target = (byte)id;
        for (var i = 0; i < MaxAttributes; i++)
        {
            var offset = TableOffset + (i * EntrySize);
            if (buffer[offset] == target)
            {
                var rawOffset = offset + 5;
                return new SmartAttribute
                {
                    Id = buffer[offset],
                    Flags = (short)(buffer[offset + 1] | (buffer[offset + 2] << 8)),
                    CurrentValue = buffer[offset + 3],
                    WorstValue = buffer[offset + 4],
                    RawValue = Raw48ToU64(rawOffset)
                };
            }
        }

        return null;
    }

    private ulong Raw48ToU64(int offset)
    {
        var v = 0ul;
        for (var i = 5; i >= 0; i--)
        {
            v = (v << 8) | buffer[offset + i];
        }
        return v;
    }
}
