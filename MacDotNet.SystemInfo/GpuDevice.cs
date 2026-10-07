namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class GpuDevice : IDisposable
{
    // ReSharper disable StringLiteralTypo
    private static readonly IntPtr PerformanceStatisticsKey = CFSTR("PerformanceStatistics");
    private static readonly IntPtr DeviceUtilizationKey = CFSTR("Device Utilization %");
    private static readonly IntPtr RendererUtilizationKey = CFSTR("Renderer Utilization %");
    private static readonly IntPtr TilerUtilizationKey = CFSTR("Tiler Utilization %");
    private static readonly IntPtr AllocSystemMemoryKey = CFSTR("Alloc system memory");
    private static readonly IntPtr InUseSystemMemoryKey = CFSTR("In use system memory");
    private static readonly IntPtr InUseSystemMemoryDriverKey = CFSTR("In use system memory (driver)");
    private static readonly IntPtr TiledSceneBytesKey = CFSTR("TiledSceneBytes");
    private static readonly IntPtr AllocatedParameterBufferSizeKey = CFSTR("Allocated PB Size");
    private static readonly IntPtr RecoveryCountKey = CFSTR("recoveryCount");
    private static readonly IntPtr SplitSceneCountKey = CFSTR("SplitSceneCount");
    private static readonly IntPtr AgcInfoKey = CFSTR("AGCInfo");
    private static readonly IntPtr PoweredOffByAgcKey = CFSTR("poweredOffByAGC");
    // ReSharper restore StringLiteralTypo

    // IOAccelerator registry entry (looked up again by RegistryEntryId when it stops working)
    private SafeIOObjectHandle entry;

    private bool disposed;

    internal ulong RegistryEntryId { get; }

    public string Name { get; }

    public DateTime UpdateAt { get; private set; }

    // Performance

    public ulong DeviceUtilization { get; private set; }

    public ulong RendererUtilization { get; private set; }

    public ulong TilerUtilization { get; private set; }

    public ulong AllocSystemMemory { get; private set; }

    public ulong InUseSystemMemory { get; private set; }

    public ulong InUseSystemMemoryDriver { get; private set; }

    public ulong TiledSceneBytes { get; private set; }

    public ulong AllocatedParameterBufferSize { get; private set; }

    public ulong RecoveryCount { get; private set; }

    public ulong SplitSceneCount { get; private set; }

    public bool PowerState { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    // Takes over the entry (released in Dispose)
    private GpuDevice(uint entry, ulong registryEntryId)
    {
        this.entry = new SafeIOObjectHandle(entry);
        RegistryEntryId = registryEntryId;
        Name = GetIOClass(entry) ?? "(unknown)";

        using var perfDict = new CFRef(IORegistryEntryCreateCFProperty(entry, PerformanceStatisticsKey, IntPtr.Zero, 0));
        UpdateCore(perfDict);
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    internal static IReadOnlyList<GpuDevice> GetDevices()
    {
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), out var itHandle);
        if ((kr != KERN_SUCCESS) || (itHandle == 0))
        {
            return [];
        }

        var results = new List<GpuDevice>();

        using var it = new IORef(itHandle);
        uint raw;
        while ((raw = IOIteratorNext(it)) != 0)
        {
            if (IORegistryEntryGetRegistryEntryID(raw, out var entryId) != KERN_SUCCESS)
            {
                _ = IOObjectRelease(raw);
                continue;
            }

            // The device keeps the entry
            results.Add(new GpuDevice(raw, entryId));
        }

        return results;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        entry.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var perf = IORegistryEntryCreateCFProperty(entry.Value, PerformanceStatisticsKey, IntPtr.Zero, 0);
        if (perf == IntPtr.Zero)
        {
            // The held entry may no longer be usable: look it up again by ID once
            var found = IOServiceGetMatchingService(0, IORegistryEntryIDMatching(RegistryEntryId));
            if (found == 0)
            {
                return false;
            }

            entry.Dispose();
            entry = new SafeIOObjectHandle(found);
            perf = IORegistryEntryCreateCFProperty(found, PerformanceStatisticsKey, IntPtr.Zero, 0);
        }

        using var perfDict = new CFRef(perf);
        UpdateCore(perfDict);

        return true;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // Reads from the held entry directly (an IOObj wrapper would release it)
    private void UpdateCore(CFRef perfDict)
    {
        if (perfDict.IsValid && (CFGetTypeID(perfDict) == CFDictionaryGetTypeID()))
        {
            DeviceUtilization = perfDict.GetUInt64(DeviceUtilizationKey);
            RendererUtilization = perfDict.GetUInt64(RendererUtilizationKey);
            TilerUtilization = perfDict.GetUInt64(TilerUtilizationKey);
            AllocSystemMemory = perfDict.GetUInt64(AllocSystemMemoryKey);
            InUseSystemMemory = perfDict.GetUInt64(InUseSystemMemoryKey);
            InUseSystemMemoryDriver = perfDict.GetUInt64(InUseSystemMemoryDriverKey);
            TiledSceneBytes = perfDict.GetUInt64(TiledSceneBytesKey);
            AllocatedParameterBufferSize = perfDict.GetUInt64(AllocatedParameterBufferSizeKey);
            RecoveryCount = perfDict.GetUInt64(RecoveryCountKey);
            SplitSceneCount = perfDict.GetUInt64(SplitSceneCountKey);
        }
        else
        {
            DeviceUtilization = 0;
            RendererUtilization = 0;
            TilerUtilization = 0;
            AllocSystemMemory = 0;
            InUseSystemMemory = 0;
            InUseSystemMemoryDriver = 0;
            TiledSceneBytes = 0;
            AllocatedParameterBufferSize = 0;
            RecoveryCount = 0;
            SplitSceneCount = 0;
        }

        using var agcInfo = new CFRef(IORegistryEntryCreateCFProperty(entry.Value, AgcInfoKey, IntPtr.Zero, 0));
        if (agcInfo.IsValid && (CFGetTypeID(agcInfo) == CFDictionaryGetTypeID()))
        {
            var poweredOff = agcInfo.GetInt64(PoweredOffByAgcKey);
            PowerState = poweredOff == 0;
        }
        else
        {
            PowerState = false;
        }

        UpdateAt = DateTime.Now;
    }

    private static string? GetIOClass(uint entry)
    {
        using var key = CFRef.CreateString("IOClass");
        if (!key.IsValid)
        {
            return null;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(entry, key, IntPtr.Zero, 0));
        return value.IsValid && (CFGetTypeID(value) == CFStringGetTypeID()) ? value.GetString() : null;
    }
}
