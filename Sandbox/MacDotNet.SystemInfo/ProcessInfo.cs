namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed record ProcessInfo
{
    // Basic

    /// <summary>プロセス ID</summary>
    public required int ProcessId { get; init; }

    /// <summary>親プロセス ID</summary>
    public required int ParentProcessId { get; init; }

    /// <summary>プロセス名。最大 16 文字 (proc_bsdinfo.pbi_name)</summary>
    public required string Name { get; init; }

    /// <summary>実行ファイルのフルパス。取得できない場合は空文字列</summary>
    public string Path { get; init; } = string.Empty;

    // Scheduler

    /// <summary>スケジューリング優先度。値が大きいほど優先度が高い</summary>
    public required int Priority { get; init; }

    /// <summary>スケジューリング優先度のオフセット。負の値ほど優先度が高い</summary>
    public required int Nice { get; init; }

    // Thread

    /// <summary>スレッドの総数</summary>
    public required int ThreadCount { get; init; }

    /// <summary>実行中のスレッド数</summary>
    public required int RunningThreadCount { get; init; }

    // CPU

    /// <summary>ユーザーモードで消費した CPU 時間の累積値 (ナノ秒)</summary>
    public required ulong UserTime { get; init; }

    /// <summary>カーネルモードで消費した CPU 時間の累積値 (ナノ秒)</summary>
    public required ulong SystemTime { get; init; }

    /// <summary>プロセスの開始日時</summary>
    public required DateTimeOffset StartTime { get; init; }

    // Memory

    /// <summary>仮想アドレス空間のサイズ (バイト)</summary>
    public required ulong VirtualSize { get; init; }

    /// <summary>実際に使用している物理メモリ量 (バイト。RSS)</summary>
    public required ulong ResidentSize { get; init; }

    // I/O

    /// <summary>ページフォルト数の累積値</summary>
    public required int Faults { get; init; }

    /// <summary>ページイン数の累積値</summary>
    public required int PageIns { get; init; }

    /// <summary>Copy-on-Write フォルト数の累積値</summary>
    public required int CowFaults { get; init; }

    // Context

    /// <summary>コンテキストスイッチ数の累積値</summary>
    public required int ContextSwitches { get; init; }

    /// <summary>Mach システムコール数の累積値</summary>
    public required int SyscallsMach { get; init; }

    /// <summary>Unix システムコール数の累積値</summary>
    public required int SyscallsUnix { get; init; }

    // Identify

    /// <summary>プロセスの実行ユーザー ID</summary>
    public required uint UserId { get; init; }

    /// <summary>プロセスの実行グループ ID</summary>
    public required uint GroupId { get; init; }

    /// <summary>プロセスが開いているファイルディスクリプタの数</summary>
    public required uint OpenFiles { get; init; }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static unsafe ProcessInfo[] GetProcesses()
    {
        var bufferSize = proc_listpids(PROC_ALL_PIDS, 0, null, 0);
        if (bufferSize <= 0)
        {
            return [];
        }

        var pidCount = bufferSize / sizeof(int);
        var pids = new int[pidCount];

        fixed (int* pidPtr = pids)
        {
            var actualSize = proc_listpids(PROC_ALL_PIDS, 0, pidPtr, bufferSize);
            if (actualSize <= 0)
            {
                return [];
            }

            var actualCount = Math.Min(actualSize / sizeof(int), pidCount);
            var result = new List<ProcessInfo>();

            var pathBuffer = stackalloc byte[(int)PROC_PIDPATHINFO_MAXSIZE];

            for (var i = 0; i < actualCount; i++)
            {
                var pid = pids[i];
                if (pid == 0)
                {
                    continue;
                }

                var entry = GetProcessCore(pid, pathBuffer);
                if (entry is not null)
                {
                    result.Add(entry);
                }
            }

            result.Sort((a, b) => a.ProcessId.CompareTo(b.ProcessId));
            return [.. result];
        }
    }

    public static unsafe ProcessInfo? GetProcess(int processId)
    {
        var pathBuffer = stackalloc byte[(int)PROC_PIDPATHINFO_MAXSIZE];
        return GetProcessCore(processId, pathBuffer);
    }

    private static unsafe ProcessInfo? GetProcessCore(int processId, byte* pathBuffer)
    {
        proc_bsdinfo bsdInfo;
        var bsdSize = proc_pidinfo(processId, PROC_PIDTBSDINFO, 0, &bsdInfo, sizeof(proc_bsdinfo));
        if (bsdSize < sizeof(proc_bsdinfo))
        {
            return null;
        }

        proc_taskinfo taskInfo;
        var taskSize = proc_pidinfo(processId, PROC_PIDTASKINFO, 0, &taskInfo, sizeof(proc_taskinfo));
        var hasTaskInfo = taskSize >= sizeof(proc_taskinfo);

        var pathLen = proc_pidpath(processId, pathBuffer, PROC_PIDPATHINFO_MAXSIZE);
        var path = pathLen > 0
            ? Marshal.PtrToStringUTF8((IntPtr)pathBuffer) ?? string.Empty
            : string.Empty;

        var name = Marshal.PtrToStringUTF8((IntPtr)bsdInfo.pbi_name);
        if (string.IsNullOrEmpty(name))
        {
            name = Marshal.PtrToStringUTF8((IntPtr)bsdInfo.pbi_comm) ?? string.Empty;
        }

        var startTime = DateTimeOffset.FromUnixTimeSeconds((long)bsdInfo.pbi_start_tvsec)
            .AddTicks((long)bsdInfo.pbi_start_tvusec * 10);

        return new ProcessInfo
        {
            ProcessId = processId,
            ParentProcessId = (int)bsdInfo.pbi_ppid,
            Name = name,
            Path = path,
            UserId = bsdInfo.pbi_uid,
            GroupId = bsdInfo.pbi_gid,
            Nice = bsdInfo.pbi_nice,
            OpenFiles = bsdInfo.pbi_nfiles,
            StartTime = startTime,
            ThreadCount = hasTaskInfo ? taskInfo.pti_threadnum : 0,
            RunningThreadCount = hasTaskInfo ? taskInfo.pti_numrunning : 0,
            VirtualSize = hasTaskInfo ? taskInfo.pti_virtual_size : 0,
            ResidentSize = hasTaskInfo ? taskInfo.pti_resident_size : 0,
            UserTime = hasTaskInfo ? taskInfo.pti_total_user : 0,
            SystemTime = hasTaskInfo ? taskInfo.pti_total_system : 0,
            Faults = hasTaskInfo ? taskInfo.pti_faults : 0,
            PageIns = hasTaskInfo ? taskInfo.pti_pageins : 0,
            CowFaults = hasTaskInfo ? taskInfo.pti_cow_faults : 0,
            ContextSwitches = hasTaskInfo ? taskInfo.pti_csw : 0,
            SyscallsMach = hasTaskInfo ? taskInfo.pti_syscalls_mach : 0,
            SyscallsUnix = hasTaskInfo ? taskInfo.pti_syscalls_unix : 0,
            Priority = hasTaskInfo ? taskInfo.pti_priority : 0,
        };
    }
}
