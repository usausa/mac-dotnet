namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>CPU コアごとの累積ティック数のスナップショット</summary>
public readonly record struct CpuLoadTicks(
    /// <summary>コア番号 (0 始まり)</summary>
    int CpuNumber,
    /// <summary>ユーザーモードで消費したティック数</summary>
    uint User,
    /// <summary>カーネルモードで消費したティック数</summary>
    uint System,
    /// <summary>アイドル状態のティック数</summary>
    uint Idle,
    /// <summary>nice 値で実行されたユーザーモードのティック数</summary>
    uint Nice);

public sealed class CpuUsage
{
    private int[]? previousCpuTicks;
    private uint previousUserTicks;
    private uint previousSystemTicks;
    private uint previousIdleTicks;
    private uint previousNiceTicks;

    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>ユーザーモードの CPU 使用率 (0.0〜1.0)。2 回以上 Update() を呼んだ後に有効</summary>
    public double UserLoad { get; private set; }

    /// <summary>カーネルモードの CPU 使用率 (0.0〜1.0)。2 回以上 Update() を呼んだ後に有効</summary>
    public double SystemLoad { get; private set; }

    /// <summary>アイドル率 (0.0〜1.0)。2 回以上 Update() を呼んだ後に有効</summary>
    public double IdleLoad { get; private set; }

    /// <summary>全体の CPU 使用率 (UserLoad + SystemLoad)。0.0〜1.0</summary>
    public double TotalLoad => UserLoad + SystemLoad;

    /// <summary>前回の Update() 時点でのコアごとの累積ティック数</summary>
    public CpuLoadTicks[] Ticks { get; private set; } = [];

    /// <summary>コアごとの CPU 使用率 (0.0〜1.0)。インデックスはコア番号に対応</summary>
    public double[] UsagePerCore { get; private set; } = [];

    /// <summary>E-core (Efficiency コア) の平均使用率 (0.0〜1.0)。Apple Silicon 以外では null</summary>
    public double? ECoreUsage { get; private set; }

    /// <summary>P-core (Performance コア) の平均使用率 (0.0〜1.0)。Apple Silicon 以外では null</summary>
    public double? PCoreUsage { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private CpuUsage()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static CpuUsage Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        var host = mach_host_self();
        var result = host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, out var processorCount, out var info, out var infoCnt);
        if (result != KERN_SUCCESS)
        {
            return false;
        }

        try
        {
            var ptr = (uint*)info;

            uint totalUser = 0, totalSystem = 0, totalIdle = 0, totalNice = 0;
            var ticks = new CpuLoadTicks[processorCount];
            var usageList = new double[processorCount];
            var currentTicks = new int[processorCount * CPU_STATE_MAX];

            for (var i = 0; i < processorCount; i++)
            {
                var offset = i * CPU_STATE_MAX;
                var user = ptr[offset + CPU_STATE_USER];
                var system = ptr[offset + CPU_STATE_SYSTEM];
                var idle = ptr[offset + CPU_STATE_IDLE];
                var nice = ptr[offset + CPU_STATE_NICE];

                ticks[i] = new CpuLoadTicks(i, user, system, idle, nice);

                currentTicks[offset + CPU_STATE_USER] = (int)user;
                currentTicks[offset + CPU_STATE_SYSTEM] = (int)system;
                currentTicks[offset + CPU_STATE_IDLE] = (int)idle;
                currentTicks[offset + CPU_STATE_NICE] = (int)nice;

                totalUser += user;
                totalSystem += system;
                totalIdle += idle;
                totalNice += nice;

                if (previousCpuTicks is not null)
                {
                    var userDiff = user - (uint)previousCpuTicks[offset + CPU_STATE_USER];
                    var systemDiff = system - (uint)previousCpuTicks[offset + CPU_STATE_SYSTEM];
                    var idleDiff = idle - (uint)previousCpuTicks[offset + CPU_STATE_IDLE];
                    var niceDiff = nice - (uint)previousCpuTicks[offset + CPU_STATE_NICE];
                    var total = userDiff + systemDiff + idleDiff + niceDiff;

                    usageList[i] = total > 0 ? (double)(userDiff + systemDiff + niceDiff) / total : 0;
                }
            }

            if (previousCpuTicks is not null)
            {
                var userDiff = totalUser - previousUserTicks;
                var sysDiff = totalSystem - previousSystemTicks;
                var idleDiff = totalIdle - previousIdleTicks;
                var niceDiff = totalNice - previousNiceTicks;
                var totalDiff = userDiff + sysDiff + idleDiff + niceDiff;

                if (totalDiff > 0)
                {
                    UserLoad = (double)userDiff / totalDiff;
                    SystemLoad = (double)sysDiff / totalDiff;
                    IdleLoad = (double)idleDiff / totalDiff;
                }
            }

            previousCpuTicks = currentTicks;
            previousUserTicks = totalUser;
            previousSystemTicks = totalSystem;
            previousIdleTicks = totalIdle;
            previousNiceTicks = totalNice;
            Ticks = ticks;
            UsagePerCore = usageList;

            CalculateAppleSiliconCoreUsage();

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            _ = vm_deallocate(task_self_trap(), info, (IntPtr)(infoCnt * sizeof(int)));
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private void CalculateAppleSiliconCoreUsage()
    {
        var nperflevels = GetSystemControlInt32("hw.nperflevels");
        if (nperflevels <= 0)
        {
            ECoreUsage = null;
            PCoreUsage = null;
            return;
        }

        var pCoreCount = GetSystemControlInt32("hw.perflevel0.logicalcpu");
        var eCoreCount = GetSystemControlInt32("hw.perflevel1.logicalcpu");

        if (pCoreCount > 0 && UsagePerCore.Length >= pCoreCount)
        {
            var sum = 0.0;
            for (var i = 0; i < pCoreCount; i++)
            {
                sum += UsagePerCore[i];
            }
            PCoreUsage = sum / pCoreCount;
        }

        if (eCoreCount > 0 && UsagePerCore.Length >= pCoreCount + eCoreCount)
        {
            var sum = 0.0;
            for (var i = pCoreCount; i < pCoreCount + eCoreCount; i++)
            {
                sum += UsagePerCore[i];
            }
            ECoreUsage = sum / eCoreCount;
        }
    }
}
