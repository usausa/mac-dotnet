namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class GpuDevice
{
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

    private GpuDevice(string name)
    {
        Name = name;
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static IReadOnlyList<GpuDevice> GetDevices()
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
            using var entry = new IOObj(raw);

            var name = entry.GetString("IOClass") ?? "(unknown)";
            var device = new GpuDevice(name);
            device.Update(entry);

            results.Add(device);
        }

        return results;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), out var itHandle);
        if ((kr != KERN_SUCCESS) || (itHandle == 0))
        {
            return false;
        }

        using var it = new IORef(itHandle);
        uint raw;
        while ((raw = IOIteratorNext(it)) != 0)
        {
            using var entry = new IOObj(raw);
            var ioClass = entry.GetString("IOClass");
            if (String.Equals(ioClass, Name, StringComparison.Ordinal))
            {
                Update(entry);
                return true;
            }
        }

        return false;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private void Update(IOObj entry)
    {
        using var perfDict = entry.GetDictionary("PerformanceStatistics");
        if (perfDict.IsValid)
        {
            DeviceUtilization = perfDict.GetUInt64("Device Utilization %");
            RendererUtilization = perfDict.GetUInt64("Renderer Utilization %");
            TilerUtilization = perfDict.GetUInt64("Tiler Utilization %");
            AllocSystemMemory = perfDict.GetUInt64("Alloc system memory");
            InUseSystemMemory = perfDict.GetUInt64("In use system memory");
            InUseSystemMemoryDriver = perfDict.GetUInt64("In use system memory (driver)");
            TiledSceneBytes = perfDict.GetUInt64("TiledSceneBytes");
            AllocatedParameterBufferSize = perfDict.GetUInt64("Allocated PB Size");
            RecoveryCount = perfDict.GetUInt64("recoveryCount");
            SplitSceneCount = perfDict.GetUInt64("SplitSceneCount");
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

        using var agcInfo = entry.GetDictionary("AGCInfo");
        if (agcInfo.IsValid)
        {
            var poweredOff = agcInfo.GetInt64("poweredOffByAGC");
            PowerState = poweredOff == 0;
        }
        else
        {
            PowerState = false;
        }

        UpdateAt = DateTime.Now;
    }
}
