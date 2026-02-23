namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class ProcessSummary
{
    public DateTime UpdateAt { get; private set; }

    public int ProcessCount { get; private set; }

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

    public unsafe bool Update()
    {
        var bufferSize = proc_listpids(PROC_ALL_PIDS, 0, null, 0);
        if (bufferSize <= 0)
        {
            return false;
        }

        // TODO ArrayPoolの使用
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
