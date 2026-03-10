namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class CpuCoreStat
{
    public string Name { get; }

    public uint User { get; internal set; }

    public uint System { get; internal set; }

    public uint Idle { get; internal set; }

    public uint Nice { get; internal set; }

    internal CpuCoreStat(string name)
    {
        Name = name;
    }
}

public sealed class CpuStat
{
    private readonly List<CpuCoreStat> cpuCores = [];

    public DateTime UpdateAt { get; private set; }

    public CpuCoreStat CpuTotal { get; } = new("total");

    public IReadOnlyList<CpuCoreStat> CpuCores => cpuCores;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal CpuStat()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        var host = mach_host_self();
        var result = host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, out var processorCount, out var info, out var infoCount);
        if (result != KERN_SUCCESS)
        {
            return false;
        }

        try
        {
            var ptr = (uint*)info;
            var totalUser = 0u;
            var totalSystem = 0u;
            var totalIdle = 0u;
            var totalNice = 0u;

            while (cpuCores.Count < processorCount)
            {
                cpuCores.Add(new CpuCoreStat($"{cpuCores.Count}"));
            }

            for (var i = 0; i < processorCount; i++)
            {
                var offset = i * CPU_STATE_MAX;
                var user = ptr[offset + CPU_STATE_USER];
                var system = ptr[offset + CPU_STATE_SYSTEM];
                var idle = ptr[offset + CPU_STATE_IDLE];
                var nice = ptr[offset + CPU_STATE_NICE];

                cpuCores[i].User = user;
                cpuCores[i].System = system;
                cpuCores[i].Idle = idle;
                cpuCores[i].Nice = nice;

                totalUser += user;
                totalSystem += system;
                totalIdle += idle;
                totalNice += nice;
            }

            CpuTotal.User = totalUser;
            CpuTotal.System = totalSystem;
            CpuTotal.Idle = totalIdle;
            CpuTotal.Nice = totalNice;

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            _ = vm_deallocate(task_self_trap(), info, sizeof(int) * infoCount);
        }
    }
}
