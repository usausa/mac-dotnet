namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// システム全体のプロセス数とスレッド総数をまとめたサマリクラス。
/// Update() を呼ぶたびに proc_listpids(2) と proc_pidinfo(2) で最新値を取得する。
/// <para>
/// Summary of system-wide process and thread counts.
/// Each call to Update() queries proc_listpids(2) and proc_pidinfo(2) for the latest values.
/// </para>
/// </summary>
public sealed class ProcessSummary
{
    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>現在実行中のプロセス数<br/>Number of currently running processes</summary>
    public int ProcessCount { get; private set; }

    /// <summary>全プロセスのスレッド総数<br/>Total thread count across all processes</summary>
    public int ThreadCount { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal ProcessSummary()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// プロセス数とスレッド総数を更新する。成功時は true、失敗時は false を返す。
    /// <para>Refreshes process and thread counts. Returns true on success, false on failure.</para>
    /// </summary>
    public unsafe bool Update()
    {
        var bufferSize = proc_listpids(PROC_ALL_PIDS, 0, null, 0);
        if (bufferSize <= 0)
        {
            return false;
        }

        var pidCount = bufferSize / sizeof(int);
        var pids = new int[pidCount];

        fixed (int* pidPtr = pids)
        {
            var actualSize = proc_listpids(PROC_ALL_PIDS, 0, pidPtr, bufferSize);
            if (actualSize <= 0)
            {
                return false;
            }

            var actualCount = Math.Min(actualSize / sizeof(int), pidCount);
            var process = 0;
            var thread = 0;

            for (var i = 0; i < actualCount; i++)
            {
                var pid = pids[i];
                if (pid == 0)
                {
                    continue;
                }

                process++;

                proc_taskinfo taskInfo;
                var taskSize = proc_pidinfo(pid, PROC_PIDTASKINFO, 0, &taskInfo, sizeof(proc_taskinfo));
                if (taskSize >= sizeof(proc_taskinfo))
                {
                    thread += taskInfo.pti_threadnum;
                }
            }

            ProcessCount = process;
            ThreadCount = thread;
            UpdateAt = DateTime.Now;
        }

        return true;
    }
}
