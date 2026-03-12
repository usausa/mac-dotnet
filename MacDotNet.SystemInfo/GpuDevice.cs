namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class GpuDevice
{
    public string Name { get; }

    public DateTime UpdateAt { get; private set; }

    // Performance

    public long DeviceUtilization { get; private set; }

    public long RendererUtilization { get; private set; }

    public long TilerUtilization { get; private set; }

    public long AllocSystemMemory { get; private set; }

    public long InUseSystemMemory { get; private set; }

    public long InUseSystemMemoryDriver { get; private set; }

    public long TiledSceneBytes { get; private set; }

    public long AllocatedParameterBufferSize { get; private set; }

    public long RecoveryCount { get; private set; }

    public long SplitSceneCount { get; private set; }

    // Sensor

    public int Temperature { get; private set; }

    public int FanSpeed { get; private set; }

    public int CoreClock { get; private set; }

    public int MemoryClock { get; private set; }

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
        var itPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref itPtr);
        if ((kr != KERN_SUCCESS) || (itPtr == IntPtr.Zero))
        {
            return [];
        }

        var results = new List<GpuDevice>();

        using var it = new IORef(itPtr);
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
        var itPtr = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOAccelerator"), ref itPtr);
        if ((kr != KERN_SUCCESS) || (itPtr == IntPtr.Zero))
        {
            return false;
        }

        using var it = new IORef(itPtr);
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
            DeviceUtilization = perfDict.GetInt64("Device Utilization %");
            RendererUtilization = perfDict.GetInt64("Renderer Utilization %");
            TilerUtilization = perfDict.GetInt64("Tiler Utilization %");
            AllocSystemMemory = perfDict.GetInt64("Alloc system memory");
            InUseSystemMemory = perfDict.GetInt64("In use system memory");
            InUseSystemMemoryDriver = perfDict.GetInt64("In use system memory (driver)");
            TiledSceneBytes = perfDict.GetInt64("TiledSceneBytes");
            AllocatedParameterBufferSize = perfDict.GetInt64("Allocated PB Size");
            RecoveryCount = perfDict.GetInt64("recoveryCount");
            SplitSceneCount = perfDict.GetInt64("SplitSceneCount");

            var rawTemperature = perfDict.GetInt64("Temperature(C)");
            var rawFanSpeed = perfDict.GetInt64("Fan Speed(%)");
            var rawCoreClock = perfDict.GetInt64("Core Clock(MHz)");
            var rawMemoryClock = perfDict.GetInt64("Memory Clock(MHz)");

            Temperature = rawTemperature is > 0 and < 128 ? (int)rawTemperature : 0;
            FanSpeed = rawFanSpeed > 0 ? (int)rawFanSpeed : 0;
            CoreClock = rawCoreClock > 0 ? (int)rawCoreClock : 0;
            MemoryClock = rawMemoryClock > 0 ? (int)rawMemoryClock : 0;
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

            Temperature = 0;
            FanSpeed = 0;
            CoreClock = 0;
            MemoryClock = 0;
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
