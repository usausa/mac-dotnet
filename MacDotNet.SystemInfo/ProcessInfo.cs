namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed record ProcessInfo
{
    // Basic

    public required int ProcessId { get; init; }

    public required int ParentProcessId { get; init; }

    public required string Name { get; init; }

    public string ExecutablePath { get; init; } = default!;

    // Scheduler

    public required int Priority { get; init; }

    public required int Nice { get; init; }

    // Thread

    public required int ThreadCount { get; init; }

    public required int RunningThreadCount { get; init; }

    // CPU

    public required ulong UserTime { get; init; }

    public required ulong SystemTime { get; init; }

    public required DateTimeOffset StartTime { get; init; }

    // Memory

    public required ulong VirtualMemorySize { get; init; }

    public required ulong ResidentMemorySize { get; init; }

    // I/O

    public required int Faults { get; init; }

    public required int PageIns { get; init; }

    public required int CowFaults { get; init; }

    // Context

    public required int ContextSwitch { get; init; }

    public required int SysCallsMach { get; init; }

    public required int SysCallsUnix { get; init; }

    // File

    public required uint OpenFiles { get; init; }

    // Identity

    public required uint UserId { get; init; }

    public required uint GroupId { get; init; }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static unsafe IReadOnlyList<ProcessInfo> GetProcesses()
    {
        var size = proc_listpids(PROC_ALL_PIDS, 0, null, 0);
        if (size <= 0)
        {
            return [];
        }

        var count = size / sizeof(int);
        var pids = new int[count];

        fixed (int* pidPtr = pids)
        {
            size = proc_listpids(PROC_ALL_PIDS, 0, pidPtr, size);
            if (size <= 0)
            {
                return [];
            }

            count = Math.Min(size / sizeof(int), count);

            var result = new List<ProcessInfo>();

            for (var i = 0; i < count; i++)
            {
                var pid = pids[i];
                if (pid == 0)
                {
                    continue;
                }

                var entry = GetProcessCore(pid);
                if (entry is not null)
                {
                    result.Add(entry);
                }
            }

            result.Sort(static (x, y) => x.ProcessId.CompareTo(y.ProcessId));

            return result;
        }
    }

    private static unsafe ProcessInfo? GetProcessCore(int processId)
    {
        // BSD info
        proc_bsdinfo bsdInfo;
        var bsdSize = proc_pidinfo(processId, PROC_PIDTBSDINFO, 0, &bsdInfo, sizeof(proc_bsdinfo));
        if (bsdSize < sizeof(proc_bsdinfo))
        {
            return null;
        }

        // Task info
        proc_taskinfo taskInfo;
        var taskSize = proc_pidinfo(processId, PROC_PIDTASKINFO, 0, &taskInfo, sizeof(proc_taskinfo));
        var hasTaskInfo = taskSize >= sizeof(proc_taskinfo);

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
        var startTime = DateTimeOffset.FromUnixTimeSeconds((long)bsdInfo.pbi_start_tvsec).AddTicks((long)bsdInfo.pbi_start_tvusec * 10);

        return new ProcessInfo
        {
            ProcessId = processId,
            ParentProcessId = (int)bsdInfo.pbi_ppid,
            Name = name,
            ExecutablePath = path,
            Priority = hasTaskInfo ? taskInfo.pti_priority : 0,
            Nice = bsdInfo.pbi_nice,
            ThreadCount = hasTaskInfo ? taskInfo.pti_threadnum : 0,
            RunningThreadCount = hasTaskInfo ? taskInfo.pti_numrunning : 0,
            UserTime = hasTaskInfo ? taskInfo.pti_total_user : 0,
            SystemTime = hasTaskInfo ? taskInfo.pti_total_system : 0,
            StartTime = startTime,
            VirtualMemorySize = hasTaskInfo ? taskInfo.pti_virtual_size : 0,
            ResidentMemorySize = hasTaskInfo ? taskInfo.pti_resident_size : 0,
            Faults = hasTaskInfo ? taskInfo.pti_faults : 0,
            PageIns = hasTaskInfo ? taskInfo.pti_pageins : 0,
            CowFaults = hasTaskInfo ? taskInfo.pti_cow_faults : 0,
            ContextSwitch = hasTaskInfo ? taskInfo.pti_csw : 0,
            SysCallsMach = hasTaskInfo ? taskInfo.pti_syscalls_mach : 0,
            SysCallsUnix = hasTaskInfo ? taskInfo.pti_syscalls_unix : 0,
            OpenFiles = bsdInfo.pbi_nfiles,
            UserId = bsdInfo.pbi_uid,
            GroupId = bsdInfo.pbi_gid
        };
    }
}
