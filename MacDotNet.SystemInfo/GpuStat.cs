namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class GpuStatEntry
{
    public string Name { get; }

    // Performance

    public long DeviceUtilization { get; internal set; }

    public long RendererUtilization { get; internal set; }

    public long TilerUtilization { get; internal set; }

    public long AllocSystemMemory { get; internal set; }

    public long InUseSystemMemory { get; internal set; }

    public long InUseSystemMemoryDriver { get; internal set; }

    public long TiledSceneBytes { get; internal set; }

    public long AllocatedParameterBufferSize { get; internal set; }

    public long RecoveryCount { get; internal set; }

    public long SplitSceneCount { get; internal set; }

    // Sensor

    public int Temperature { get; internal set; }

    public int FanSpeed { get; internal set; }

    public int CoreClock { get; internal set; }

    public int MemoryClock { get; internal set; }

    public bool PowerState { get; internal set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal GpuStatEntry(string name)
    {
        Name = name;
    }
}

public sealed class GpuStat
{
    private readonly List<GpuStatEntry> devices = [];

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<GpuStatEntry> Devices => devices;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal GpuStat()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        var itPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref itPtr);
        if ((kr != KERN_SUCCESS) || (itPtr == IntPtr.Zero))
        {
            return false;
        }

        using var it = new IORef(itPtr);
        uint raw;
        var index = 0;
        while ((raw = IOIteratorNext(it)) != 0)
        {
            using var entry = new IOObj(raw);

            if (index >= devices.Count)
            {
                var name = entry.GetString("IOClass") ?? "(unknown)";
                devices.Add(new GpuStatEntry(name));
            }

            UpdateEntry(devices[index], entry);

            index++;
        }

        UpdateAt = DateTime.Now;

        return true;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static void UpdateEntry(GpuStatEntry device, IOObj entry)
    {
        using var perfDict = entry.GetDictionary("PerformanceStatistics");
        if (perfDict.IsValid)
        {
            device.DeviceUtilization = perfDict.GetInt64("Device Utilization %");
            device.RendererUtilization = perfDict.GetInt64("Renderer Utilization %");
            device.TilerUtilization = perfDict.GetInt64("Tiler Utilization %");
            device.AllocSystemMemory = perfDict.GetInt64("Alloc system memory");
            device.InUseSystemMemory = perfDict.GetInt64("In use system memory");
            device.InUseSystemMemoryDriver = perfDict.GetInt64("In use system memory (driver)");
            device.TiledSceneBytes = perfDict.GetInt64("TiledSceneBytes");
            device.AllocatedParameterBufferSize = perfDict.GetInt64("Allocated PB Size");
            device.RecoveryCount = perfDict.GetInt64("recoveryCount");
            device.SplitSceneCount = perfDict.GetInt64("SplitSceneCount");

            var rawTemperature = perfDict.GetInt64("Temperature(C)");
            var rawFanSpeed = perfDict.GetInt64("Fan Speed(%)");
            var rawCoreClock = perfDict.GetInt64("Core Clock(MHz)");
            var rawMemory = perfDict.GetInt64("Memory Clock(MHz)");
            device.Temperature = rawTemperature > 0 && rawTemperature < 128 ? (int)rawTemperature : 0;
            device.FanSpeed = rawFanSpeed > 0 ? (int)rawFanSpeed : 0;
            device.CoreClock = rawCoreClock > 0 ? (int)rawCoreClock : 0;
            device.MemoryClock = rawMemory > 0 ? (int)rawMemory : 0;
        }
        else
        {
            device.DeviceUtilization = 0;
            device.RendererUtilization = 0;
            device.TilerUtilization = 0;
            device.AllocSystemMemory = 0;
            device.InUseSystemMemory = 0;
            device.InUseSystemMemoryDriver = 0;
            device.TiledSceneBytes = 0;
            device.AllocatedParameterBufferSize = 0;
            device.RecoveryCount = 0;
            device.SplitSceneCount = 0;

            device.Temperature = 0;
            device.FanSpeed = 0;
            device.CoreClock = 0;
            device.MemoryClock = 0;
        }

        using var agcInfo = entry.GetDictionary("AGCInfo");
        if (agcInfo.IsValid)
        {
            var poweredOff = agcInfo.GetInt64("poweredOffByAGC");
            device.PowerState = poweredOff == 0;
        }
        {
            device.PowerState = false;
        }
    }
}
