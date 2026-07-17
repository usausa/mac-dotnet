namespace MacDotNet.Disk;

using System.Buffers.Binary;

using static MacDotNet.Disk.Helper;
using static MacDotNet.Disk.NativeMethods;

internal sealed class SmartNvme : ISmartNvme, IDisposable
{
    private const int SmartDataSize = 512;

#pragma warning disable IDE0055
#pragma warning disable SA1117
    // kIONVMeSMARTUserClientTypeID
    private static readonly IntPtr PluginTypeUuid = CFUUIDGetConstantUUIDWithBytes(
        IntPtr.Zero,
        0xAA, 0x0F, 0xA6, 0xF9, 0xC2, 0xD6, 0x45, 0x7F,
        0xB1, 0x0B, 0x59, 0xA1, 0x32, 0x53, 0x29, 0x2F);

    // IOCFPlugInInterface
    private static readonly IntPtr CfPluginUuid = CFUUIDGetConstantUUIDWithBytes(
        IntPtr.Zero,
        0xC2, 0x44, 0xE8, 0x58, 0x10, 0x9C, 0x11, 0xD4,
        0x91, 0xD4, 0x00, 0x50, 0xE4, 0xC6, 0x42, 0x6F);

    // kIONVMeSMARTInterfaceID
    private static readonly CFUUIDBytes SmartUuid = new()
    {
        byte0 = 0xCC, byte1 = 0xD1, byte2 = 0xDB, byte3 = 0x19,
        byte4 = 0xFD, byte5 = 0x9A, byte6 = 0x4D, byte7 = 0xAF,
        byte8 = 0xBF, byte9 = 0x95, byte10 = 0x12, byte11 = 0x45,
        byte12 = 0x4B, byte13 = 0x23, byte14 = 0x0A, byte15 = 0xB6
    };
#pragma warning restore SA1117
#pragma warning restore IDE0055

    private readonly SafePlugInInterface? pluginInterface;

    private readonly SafePlugInInterface? smartInterface;

    private bool disposed;

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

    internal unsafe SmartNvme(uint service)
    {
        IntPtr ppPlugin;
        int score;
        var kr = IOCreatePlugInInterfaceForService(service, PluginTypeUuid, CfPluginUuid, &ppPlugin, &score);
        if ((kr != KERN_SUCCESS) || (ppPlugin == IntPtr.Zero))
        {
            return;
        }

        pluginInterface = new SafePlugInInterface(ppPlugin);

        var vtable = *(IntPtr*)ppPlugin;
        var qiFn = (delegate* unmanaged<IntPtr, CFUUIDBytes, IntPtr*, int>)(*((IntPtr*)vtable + 1));

        var pSmartInterface = IntPtr.Zero;
        var hr = qiFn(ppPlugin, SmartUuid, &pSmartInterface);
        if ((hr != S_OK) || (pSmartInterface == IntPtr.Zero))
        {
            return;
        }

        smartInterface = new SafePlugInInterface(pSmartInterface);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        smartInterface?.Dispose();
        pluginInterface?.Dispose();
        disposed = true;
    }

    public unsafe bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (smartInterface is null)
        {
            LastUpdate = false;
            return false;
        }

        // NVMe SMART interface vtable layout (64-bit):
        //   0: _reserved, 8: QI, 16: AddRef, 24: Release
        //   32: version(2) + revision(2) + pad(4)
        //   40: SMARTReadData
        var smartVtable = *(IntPtr*)smartInterface.Pointer;
        var readDataFn = (delegate* unmanaged<IntPtr, byte*, int>)(*(IntPtr*)((byte*)smartVtable + 40));

        var buffer = stackalloc byte[SmartDataSize];
        var kr = readDataFn(smartInterface.Pointer, buffer);
        if (kr != KERN_SUCCESS)
        {
            LastUpdate = false;
            return false;
        }

        var span = new ReadOnlySpan<byte>(buffer, SmartDataSize);
        CriticalWarning = span[0];
        Temperature = KelvinToCelsius(BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(1, 2)));
        AvailableSpare = span[3];
        AvailableSpareThreshold = span[4];
        PercentageUsed = span[5];
        DataUnitRead = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(32, 8));
        DataUnitWritten = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(48, 8));
        HostReadCommands = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(64, 8));
        HostWriteCommands = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(80, 8));
        ControllerBusyTime = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(96, 8));
        PowerCycles = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(112, 8));
        PowerOnHours = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(128, 8));
        UnsafeShutdowns = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(144, 8));
        MediaErrors = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(160, 8));
        ErrorInfoLogEntries = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(176, 8));
        WarningCompositeTemperatureTime = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(192, 4));
        CriticalCompositeTemperatureTime = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(196, 4));
        for (var i = 0; i < TemperatureSensors.Length; i++)
        {
            TemperatureSensors[i] = KelvinToCelsius(BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(200 + (i * 2), 2)));
        }

        LastUpdate = true;
        return true;
    }
}
