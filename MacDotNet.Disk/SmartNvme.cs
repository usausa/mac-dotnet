namespace MacDotNet.Disk;

using static MacDotNet.Disk.Helper;
using static MacDotNet.Disk.NativeMethods;

// NVMe SMARTセッション
// NVMe SMART session.
// IOCreatePlugInInterfaceForServiceで取得したプラグインインターフェースを保持し、
// SMARTReadDataを繰り返し呼び出すことで最新のSMARTデータを取得する。
// Holds the plug-in interface obtained via IOCreatePlugInInterfaceForService
// and retrieves the latest SMART data by repeatedly calling SMARTReadData.
#pragma warning disable CA1806
#pragma warning disable SA1309
internal sealed class SmartNvme : ISmartNvme, IDisposable
{
    private const int SmartDataSize = 512;

    // プラグインインターフェースハンドル (COM-like二重ポインタ)
    // Plug-in interface handle (COM-like double pointer)
    private IntPtr pluginInterface;

    // NVMe SMARTインターフェースハンドル
    // NVMe SMART interface handle
    private IntPtr smartInterface;

    public bool LastUpdate { get; private set; }

    public byte CriticalWarning { get; private set; }

    public short Temperature { get; private set; }

    public byte AvailableSpare { get; private set; }

    public byte AvailableSpareThreshold { get; private set; }

    public byte PercentageUsed { get; private set; }

    public ulong DataUnitRead { get; private set; }

    public ulong DataUnitWritten { get; private set; }

    public ulong HostReadCommands { get; private set; }

    public ulong HostWriteCommands { get; private set; }

    public ulong ControllerBusyTime { get; private set; }

    public ulong PowerCycles { get; private set; }

    public ulong PowerOnHours { get; private set; }

    public ulong UnsafeShutdowns { get; private set; }

    public ulong MediaErrors { get; private set; }

    public ulong ErrorInfoLogEntries { get; private set; }

    public uint WarningCompositeTemperatureTime { get; private set; }

    public uint CriticalCompositeTemperatureTime { get; private set; }

    public short[] TemperatureSensors { get; } = new short[8];

    private SmartNvme(IntPtr pluginInterface, IntPtr smartInterface)
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
    public static unsafe SmartNvme? Open(uint service)
    {
        // NVMeSMARTLib plugin UUID (kIONVMeSMARTUserClientTypeID)
#pragma warning disable SA1117
        var pluginTypeUuid = CFUUIDGetConstantUUIDWithBytes(
            IntPtr.Zero,
            0xAA, 0x0F, 0xA6, 0xF9, 0xC2, 0xD6, 0x45, 0x7F,
            0xB1, 0x0B, 0x59, 0xA1, 0x32, 0x53, 0x29, 0x2F);
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

        // NVMe SMART Interface UUID (kIONVMeSMARTInterfaceID)
        var smartUuid = new CFUUIDBytes
        {
            byte0 = 0xCC, byte1 = 0xD1, byte2 = 0xDB, byte3 = 0x19,
            byte4 = 0xFD, byte5 = 0x9A, byte6 = 0x4D, byte7 = 0xAF,
            byte8 = 0xBF, byte9 = 0x95, byte10 = 0x12, byte11 = 0x45,
            byte12 = 0x4B, byte13 = 0x23, byte14 = 0x0A, byte15 = 0xB6
        };

        var pSmartInterface = IntPtr.Zero;
        var hr = qiFn(ppPlugin, smartUuid, &pSmartInterface);
        if (hr != S_OK || pSmartInterface == IntPtr.Zero)
        {
            ReleasePlugInInterface(ppPlugin);
            return null;
        }

        return new SmartNvme(ppPlugin, pSmartInterface);
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

        // NVMe SMART interface vtable layout (64-bit):
        //   0: _reserved, 8: QI, 16: AddRef, 24: Release
        //   32: version(2) + revision(2) + pad(4)
        //   40: SMARTReadData
        // 注意: このオフセットはApple非公開APIのレイアウトに依存するため、
        //       macOSのアップデートにより変更される可能性がある。
        // Note: This offset depends on the private Apple API layout
        //       and may change with macOS updates.
        var smartVtable = *(IntPtr*)smartInterface;
        var readDataFn = (delegate* unmanaged<IntPtr, byte*, int>)(*(IntPtr*)((byte*)smartVtable + 40));

        var buffer = stackalloc byte[SmartDataSize];
        var kr = readDataFn(smartInterface, buffer);
        if (kr != KERN_SUCCESS)
        {
            LastUpdate = false;
            return false;
        }

        CriticalWarning = buffer[0];
        Temperature = KelvinToCelsius((ushort)(buffer[1] | (buffer[2] << 8)));
        AvailableSpare = buffer[3];
        AvailableSpareThreshold = buffer[4];
        PercentageUsed = buffer[5];
        DataUnitRead = Le128ToUInt64(buffer + 32);
        DataUnitWritten = Le128ToUInt64(buffer + 48);
        HostReadCommands = Le128ToUInt64(buffer + 64);
        HostWriteCommands = Le128ToUInt64(buffer + 80);
        ControllerBusyTime = Le128ToUInt64(buffer + 96);
        PowerCycles = Le128ToUInt64(buffer + 112);
        PowerOnHours = Le128ToUInt64(buffer + 128);
        UnsafeShutdowns = Le128ToUInt64(buffer + 144);
        MediaErrors = Le128ToUInt64(buffer + 160);
        ErrorInfoLogEntries = Le128ToUInt64(buffer + 176);
        WarningCompositeTemperatureTime = *(uint*)(buffer + 192);
        CriticalCompositeTemperatureTime = *(uint*)(buffer + 196);

        for (var i = 0; i < TemperatureSensors.Length; i++)
        {
            TemperatureSensors[i] = KelvinToCelsius(*(ushort*)(buffer + 200 + (i * 2)));
        }

        LastUpdate = true;
        return true;
    }

    // NVMe仕様では一部カウンタが128-bitリトルエンディアンで格納される。
    // 実運用上は下位64-bitに収まるため上位8バイトは無視する。
    // In the NVMe spec, some counters are stored as 128-bit little-endian.
    // In practice they fit in the lower 64 bits, so the upper 8 bytes are ignored.
    private static unsafe ulong Le128ToUInt64(byte* p)
    {
        var v = 0ul;
        for (var i = 7; i >= 0; i--)
        {
            v = (v << 8) | p[i];
        }

        return v;
    }
}
#pragma warning restore SA1309
#pragma warning restore CA1806
