namespace MacDotNet.SystemInfo;

using System.Buffers;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class ProcessSummary
{
    public DateTime UpdateAt { get; private set; }

    public int ProcessCount { get; private set; }

    public int ThreadCount { get; private set; }

    public int OpenFileCount { get; private set; }

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

        var pidCount = bufferSize / sizeof(int);
        var pids = ArrayPool<int>.Shared.Rent(pidCount);
        pids.AsSpan().Clear();

        try
        {
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
                var openFile = 0;
                for (var i = 0; i < actualCount; i++)
                {
                    var pid = pids[i];
                    if (pid == 0)
                    {
                        continue;
                    }

                    proc_bsdinfo bsdInfo;
                    var bsdSize = proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, &bsdInfo, sizeof(proc_bsdinfo));
                    if (bsdSize < sizeof(proc_bsdinfo))
                    {
                        continue;
                    }

                    process++;
                    openFile += (int)bsdInfo.pbi_nfiles;

                    proc_taskinfo taskInfo;
                    var taskSize = proc_pidinfo(pid, PROC_PIDTASKINFO, 0, &taskInfo, sizeof(proc_taskinfo));
                    if (taskSize >= sizeof(proc_taskinfo))
                    {
                        thread += taskInfo.pti_threadnum;
                    }
                }

                ProcessCount = process;
                ThreadCount = thread;
                OpenFileCount = openFile;

                UpdateAt = DateTime.Now;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(pids);
        }

        return true;
    }
}
