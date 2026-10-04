namespace MacDotNet.SystemInfo;

using System.Buffers;
using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public enum ProcessState
{
    Unknown = 0,
    Idle,
    Running,
    Sleeping,
    Stopped,
    Zombie
}

public sealed record ProcessInfo
{
    private static int maxResourceUsageFlavor = RUSAGE_INFO_V6;

    // Basic

    public required int ProcessId { get; init; }

    public required int ParentProcessId { get; init; }

    public required int ProcessGroupId { get; init; }

    public required string Name { get; init; }

    public string ExecutablePath { get; init; } = default!;

    public required ProcessState Status { get; init; }

    public uint Flags { get; init; }

    public bool IsTranslated => (Flags & PROC_FLAG_TRANSLATED) != 0;

    // Availability

    // BSD details (Nice, start time, open files, full name) are readable
    public bool HasBsdInfo { get; init; }

    // Task counters (Priority, threads, CPU time, memory, faults, context) are readable
    public bool HasTaskInfo { get; init; }

    // Resource usage counters (I/O bytes, footprint, wake-ups, energy) are readable
    public bool HasResourceUsage { get; init; }

    // Scheduler

    public required int Priority { get; init; }

    public required int Nice { get; init; }

    public int Policy { get; init; }

    // Thread

    public required int ThreadCount { get; init; }

    public required int RunningThreadCount { get; init; }

    // CPU

    public required TimeSpan UserTime { get; init; }

    public required TimeSpan SystemTime { get; init; }

    public required DateTimeOffset StartTime { get; init; }

    // Memory

    public required ulong VirtualMemorySize { get; init; }

    public required ulong ResidentMemorySize { get; init; }

    public ulong PhysicalFootprint { get; init; }

    // I/O

    public required int Faults { get; init; }

    public required int PageIns { get; init; }

    public required int CowFaults { get; init; }

    // Context

    public required int ContextSwitch { get; init; }

    public required int SysCallsMach { get; init; }

    public required int SysCallsUnix { get; init; }

    public int MessagesSent { get; init; }

    public int MessagesReceived { get; init; }

    // I/O

    public required ulong ReadBytes { get; init; }

    public required ulong WriteBytes { get; init; }

    // Energy

    public ulong InterruptWakeups { get; init; }

    public ulong PackageIdleWakeups { get; init; }

    // Cumulative energy (nJ), Apple silicon only
    public ulong Energy { get; init; }

    // File

    public required uint OpenFileCount { get; init; }

    // Identity

    public required uint UserId { get; init; }

    public required uint GroupId { get; init; }

    public uint RealUserId { get; init; }

    public uint RealGroupId { get; init; }

    public uint SavedUserId { get; init; }

    public uint SavedGroupId { get; init; }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    // ReSharper disable once RedundantUnsafeContext
    public static unsafe IReadOnlyList<ProcessInfo> GetProcesses()
    {
        var size = proc_listpids(PROC_ALL_PIDS, 0, null, 0);
        if (size <= 0)
        {
            return [];
        }

        var count = size / sizeof(int);
        var pids = ArrayPool<int>.Shared.Rent(count);
        try
        {
            fixed (int* pidPtr = pids)
            {
                size = proc_listpids(PROC_ALL_PIDS, 0, pidPtr, size);
                if (size <= 0)
                {
                    return [];
                }

                count = Math.Min(size / sizeof(int), count);

                var result = new List<ProcessInfo>();

                foreach (var pid in pids.AsSpan(0, count))
                {
                    if (pid == 0)
                    {
                        continue;
                    }

                    var entry = GetProcess(pid);
                    if (entry is not null)
                    {
                        result.Add(entry);
                    }
                }

                result.Sort(static (x, y) => x.ProcessId.CompareTo(y.ProcessId));

                return result;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(pids);
        }
    }

    public static unsafe ProcessInfo? GetProcess(int processId)
    {
        // BSD info
        proc_bsdinfo bsdInfo;
        var bsdSize = proc_pidinfo(processId, PROC_PIDTBSDINFO, 0, &bsdInfo, sizeof(proc_bsdinfo));
        var hasBsdInfo = bsdSize >= sizeof(proc_bsdinfo);
        if (!hasBsdInfo && !ReadShortBsdInfo(processId, &bsdInfo))
        {
            return null;
        }

        // Task info
        proc_taskinfo taskInfo;
        var taskSize = proc_pidinfo(processId, PROC_PIDTASKINFO, 0, &taskInfo, sizeof(proc_taskinfo));
        var hasTaskInfo = taskSize >= sizeof(proc_taskinfo);

        // Resource usage info
        rusage_info_v6 usageInfo;
        var usageVersion = ReadResourceUsage(processId, &usageInfo);
        var hasUsageInfo = usageVersion >= RUSAGE_INFO_V2;

        // Name
        var name = Marshal.PtrToStringUTF8((IntPtr)bsdInfo.pbi_name);
        if (String.IsNullOrEmpty(name))
        {
            name = Marshal.PtrToStringUTF8((IntPtr)bsdInfo.pbi_comm) ?? string.Empty;
        }

        // Path
        var pathBuffer = stackalloc byte[(int)PROC_PIDPATHINFO_MAXSIZE];
        var pathLen = proc_pidpath(processId, pathBuffer, PROC_PIDPATHINFO_MAXSIZE);
        var path = pathLen > 0 ? Marshal.PtrToStringUTF8((IntPtr)pathBuffer) ?? string.Empty : string.Empty;

        // Start time
        var startTime = hasBsdInfo
            ? DateTimeOffset.FromUnixTimeSeconds((long)bsdInfo.pbi_start_tvsec).AddTicks((long)bsdInfo.pbi_start_tvusec * 10)
            : default;

        return new ProcessInfo
        {
            ProcessId = processId,
            ParentProcessId = (int)bsdInfo.pbi_ppid,
            ProcessGroupId = (int)bsdInfo.pbi_pgid,
            Name = name,
            ExecutablePath = path,
            Status = ToProcessState(bsdInfo.pbi_status),
            Flags = bsdInfo.pbi_flags,
            HasBsdInfo = hasBsdInfo,
            HasTaskInfo = hasTaskInfo,
            HasResourceUsage = hasUsageInfo,
            Priority = hasTaskInfo ? taskInfo.pti_priority : 0,
            Nice = bsdInfo.pbi_nice,
            Policy = hasTaskInfo ? taskInfo.pti_policy : 0,
            ThreadCount = hasTaskInfo ? taskInfo.pti_threadnum : 0,
            RunningThreadCount = hasTaskInfo ? taskInfo.pti_numrunning : 0,
            // pti_total_user/system are in Mach absolute time units
            UserTime = hasTaskInfo ? TimeSpan.FromTicks((long)(MachAbsoluteToNanoseconds(taskInfo.pti_total_user) / 100)) : TimeSpan.Zero,
            SystemTime = hasTaskInfo ? TimeSpan.FromTicks((long)(MachAbsoluteToNanoseconds(taskInfo.pti_total_system) / 100)) : TimeSpan.Zero,
            StartTime = startTime,
            VirtualMemorySize = hasTaskInfo ? taskInfo.pti_virtual_size : 0,
            ResidentMemorySize = hasTaskInfo ? taskInfo.pti_resident_size : 0,
            PhysicalFootprint = hasUsageInfo ? usageInfo.ri_phys_footprint : 0,
            Faults = hasTaskInfo ? taskInfo.pti_faults : 0,
            PageIns = hasTaskInfo ? taskInfo.pti_pageins : 0,
            CowFaults = hasTaskInfo ? taskInfo.pti_cow_faults : 0,
            ContextSwitch = hasTaskInfo ? taskInfo.pti_csw : 0,
            SysCallsMach = hasTaskInfo ? taskInfo.pti_syscalls_mach : 0,
            SysCallsUnix = hasTaskInfo ? taskInfo.pti_syscalls_unix : 0,
            MessagesSent = hasTaskInfo ? taskInfo.pti_messages_sent : 0,
            MessagesReceived = hasTaskInfo ? taskInfo.pti_messages_received : 0,
            ReadBytes = hasUsageInfo ? usageInfo.ri_diskio_bytesread : 0,
            WriteBytes = hasUsageInfo ? usageInfo.ri_diskio_byteswritten : 0,
            InterruptWakeups = hasUsageInfo ? usageInfo.ri_interrupt_wkups : 0,
            PackageIdleWakeups = hasUsageInfo ? usageInfo.ri_pkg_idle_wkups : 0,
            Energy = usageVersion >= RUSAGE_INFO_V6 ? usageInfo.ri_energy_nj : 0,
            OpenFileCount = bsdInfo.pbi_nfiles,
            UserId = bsdInfo.pbi_uid,
            GroupId = bsdInfo.pbi_gid,
            RealUserId = bsdInfo.pbi_ruid,
            RealGroupId = bsdInfo.pbi_rgid,
            SavedUserId = bsdInfo.pbi_svuid,
            SavedGroupId = bsdInfo.pbi_svgid
        };
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static unsafe int ReadResourceUsage(int processId, rusage_info_v6* buffer)
    {
        *buffer = default;

        var flavor = maxResourceUsageFlavor;
        while (flavor >= RUSAGE_INFO_V2)
        {
            if (proc_pid_rusage(processId, flavor, buffer) == 0)
            {
                if (flavor < maxResourceUsageFlavor)
                {
                    maxResourceUsageFlavor = flavor;
                }

                return flavor;
            }

            flavor = flavor switch
            {
                RUSAGE_INFO_V6 => RUSAGE_INFO_V4,
                RUSAGE_INFO_V4 => RUSAGE_INFO_V2,
                _ => -1
            };
        }

        return -1;
    }

    private static unsafe bool ReadShortBsdInfo(int processId, proc_bsdinfo* bsdInfo)
    {
        proc_bsdshortinfo shortInfo;
        if (proc_pidinfo(processId, PROC_PIDT_SHORTBSDINFO, 0, &shortInfo, sizeof(proc_bsdshortinfo)) < sizeof(proc_bsdshortinfo))
        {
            return false;
        }

        *bsdInfo = default;
        bsdInfo->pbi_flags = shortInfo.pbsi_flags;
        bsdInfo->pbi_status = shortInfo.pbsi_status;
        bsdInfo->pbi_pid = shortInfo.pbsi_pid;
        bsdInfo->pbi_ppid = shortInfo.pbsi_ppid;
        bsdInfo->pbi_uid = shortInfo.pbsi_uid;
        bsdInfo->pbi_gid = shortInfo.pbsi_gid;
        bsdInfo->pbi_ruid = shortInfo.pbsi_ruid;
        bsdInfo->pbi_rgid = shortInfo.pbsi_rgid;
        bsdInfo->pbi_svuid = shortInfo.pbsi_svuid;
        bsdInfo->pbi_svgid = shortInfo.pbsi_svgid;
        bsdInfo->pbi_pgid = shortInfo.pbsi_pgid;
        new ReadOnlySpan<byte>(shortInfo.pbsi_comm, 16).CopyTo(new Span<byte>(bsdInfo->pbi_comm, 16));
        return true;
    }

    private static ProcessState ToProcessState(uint status) => status switch
    {
        SIDL => ProcessState.Idle,
        SRUN => ProcessState.Running,
        SSLEEP => ProcessState.Sleeping,
        SSTOP => ProcessState.Stopped,
        SZOMB => ProcessState.Zombie,
        _ => ProcessState.Unknown
    };
}
