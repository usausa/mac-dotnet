namespace MacDotNet.Disk;

using static MacDotNet.Disk.Helper;
using static MacDotNet.Disk.NativeMethods;

// ATA SMARTセッション
// ATA SMART session.
// IOCreatePlugInInterfaceForServiceで取得したプラグインインターフェースを保持し、
// SMARTReadDataを繰り返し呼び出すことで最新のSMARTデータを取得する。
// Holds the plug-in interface obtained via IOCreatePlugInInterfaceForService
// and retrieves the latest SMART data by repeatedly calling SMARTReadData.
#pragma warning disable CA1806
#pragma warning disable SA1309
internal sealed class SmartGeneric : ISmartGeneric, IDisposable
{
    private const int SmartDataSize = 512;
    private const int MaxAttributes = 30;
    private const int TableOffset = 2;
    private const int EntrySize = 12;

    private readonly byte[] buffer = new byte[SmartDataSize];

    // プラグインインターフェースハンドル
    // Plug-in interface handle
    private IntPtr pluginInterface;

    // ATA SMARTインターフェースハンドル
    // ATA SMART interface handle
    private IntPtr smartInterface;

    public bool LastUpdate { get; private set; }

    private SmartGeneric(IntPtr pluginInterface, IntPtr smartInterface)
    {
        this.pluginInterface = pluginInterface;
        this.smartInterface = smartInterface;
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

    // デバイスサービスからSMARTセッションを開く
    // Opens a SMART session from the device service
    public static unsafe SmartGeneric? Open(uint service)
    {
        // ATASMARTLib plugin UUID (kIOATASMARTUserClientTypeID)
#pragma warning disable SA1117
        var pluginTypeUuid = CFUUIDGetConstantUUIDWithBytes(
            IntPtr.Zero,
            0x24, 0x51, 0x4B, 0x7A, 0x28, 0x04, 0x11, 0xD6,
            0x8A, 0x02, 0x00, 0x30, 0x65, 0x70, 0x48, 0x66);
#pragma warning restore SA1117

        // IOCFPlugInInterface UUID
#pragma warning disable SA1117
        var cfPluginUuid = CFUUIDGetConstantUUIDWithBytes(
            IntPtr.Zero,
            0xC2, 0x44, 0xE8, 0x58, 0x10, 0x9C, 0x11, 0xD4,
            0x91, 0xD4, 0x00, 0x50, 0xE4, 0xC6, 0x42, 0x6F);
#pragma warning restore SA1117

        if (pluginTypeUuid == IntPtr.Zero || cfPluginUuid == IntPtr.Zero)
        {
            return null;
        }

        IntPtr ppPlugin;
        int score;
        var kr = IOCreatePlugInInterfaceForService(
            service, pluginTypeUuid, cfPluginUuid, &ppPlugin, &score);
        if (kr != KERN_SUCCESS || ppPlugin == IntPtr.Zero)
        {
            return null;
        }

        // QueryInterfaceでSMARTインターフェースを取得
        // Obtain the SMART interface via QueryInterface
        var vtable = *(IntPtr*)ppPlugin;
        var qiFn = (delegate* unmanaged<IntPtr, CFUUIDBytes, IntPtr*, int>)(*((IntPtr*)vtable + 1));

        // ATA SMART Interface UUID (kIOATASMARTInterfaceID)
        var smartUuid = new CFUUIDBytes
        {
            byte0 = 0x08, byte1 = 0xAB, byte2 = 0xE2, byte3 = 0x1C,
            byte4 = 0x20, byte5 = 0xD4, byte6 = 0x11, byte7 = 0xD6,
            byte8 = 0x8D, byte9 = 0xF6, byte10 = 0x00, byte11 = 0x03,
            byte12 = 0x93, byte13 = 0x5A, byte14 = 0x76, byte15 = 0xB2
        };

        var pSmartInterface = IntPtr.Zero;
        var hr = qiFn(ppPlugin, smartUuid, &pSmartInterface);
        if (hr != S_OK || pSmartInterface == IntPtr.Zero)
        {
            ReleasePlugInInterface(ppPlugin);
            return null;
        }

        // SMART操作を有効化
        // Enable SMART operations
        // ATA SMART interface vtable layout (64-bit):
        //   0: _reserved, 8: QI, 16: AddRef, 24: Release
        //   32: version(2) + revision(2) + pad(4)
        //   40: SMARTEnableDisableOperations
        // 注意: このオフセットはApple非公開APIのレイアウトに依存するため、
        //       macOSのアップデートにより変更される可能性がある。
        // Note: This offset depends on the private Apple API layout
        //       and may change with macOS updates.
        var smartVtable = *(IntPtr*)pSmartInterface;
        var enableFn = (delegate* unmanaged<IntPtr, byte, int>)(*(IntPtr*)((byte*)smartVtable + 40));
        kr = enableFn(pSmartInterface, 1);
        if (kr != KERN_SUCCESS)
        {
            ReleasePlugInInterface(pSmartInterface);
            ReleasePlugInInterface(ppPlugin);
            return null;
        }

        return new SmartGeneric(ppPlugin, pSmartInterface);
    }

    // SMARTデータ読み取り (繰り返し呼び出し可能)
    // Reads SMART data (can be called repeatedly)
    public unsafe bool Update()
    {
        if (smartInterface == IntPtr.Zero)
        {
            LastUpdate = false;
            return false;
        }

        // ATA SMART interface vtable offset 72: SMARTReadData
        // 注意: このオフセットはApple非公開APIのレイアウトに依存するため、
        //       macOSのアップデートにより変更される可能性がある。
        // Note: This offset depends on the private Apple API layout
        //       and may change with macOS updates.
        var smartVtable = *(IntPtr*)smartInterface;
        var readDataFn = (delegate* unmanaged<IntPtr, byte*, int>)(*(IntPtr*)((byte*)smartVtable + 72));

        fixed (byte* bufPtr = buffer)
        {
            var kr = readDataFn(smartInterface, bufPtr);
            LastUpdate = kr == KERN_SUCCESS;
            return LastUpdate;
        }
    }

    /// <summary>
    /// サポートされているSMART属性ID一覧を取得する。
    /// Returns the list of supported SMART attribute IDs.
    /// </summary>
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

    /// <summary>
    /// 指定IDのSMART属性を取得する。
    /// Retrieves the SMART attribute for the specified ID.
    /// </summary>
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

    // 48ビットRAW値を符号なも64ビット整数に変換する
    // Converts a 48-bit raw value to an unsigned 64-bit integer
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
#pragma warning restore SA1309
#pragma warning restore CA1806
