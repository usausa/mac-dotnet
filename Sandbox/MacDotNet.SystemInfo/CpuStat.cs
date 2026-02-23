namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// CPU コアごとの累積ティック数。内部で更新されるミュータブルクラス。
/// <para>Cumulative CPU tick counts per core. Mutable class updated internally.</para>
/// </summary>
public sealed class CpuCoreStat
{
    /// <summary>コア番号 (0 始まり)。CpuTotal の場合は -1<br/>Core number (0-based). -1 for the aggregate CpuTotal.</summary>
    public int CpuNumber { get; }

    /// <summary>ユーザーモードで消費した累積ティック数<br/>Cumulative ticks spent in user mode</summary>
    public uint User { get; internal set; }

    /// <summary>カーネルモードで消費した累積ティック数<br/>Cumulative ticks spent in kernel mode</summary>
    public uint System { get; internal set; }

    /// <summary>アイドル状態の累積ティック数<br/>Cumulative ticks spent idle</summary>
    public uint Idle { get; internal set; }

    /// <summary>nice 値で実行されたユーザーモードの累積ティック数<br/>Cumulative ticks spent in user mode with a nice priority</summary>
    public uint Nice { get; internal set; }

    internal CpuCoreStat(int cpuNumber)
    {
        CpuNumber = cpuNumber;
    }
}

/// <summary>
/// ホスト全体および各コアの CPU 累積ティック数を管理するクラス。
/// LinuxDotNet.SystemInfo の SystemStat / CpuStat と同じパターンで、
/// 累積値をそのまま保持し、使用率の計算は呼び出しがわで行う。
/// <para>
/// Manages cumulative CPU tick counts for all cores and the host as a whole.
/// Follows the same pattern as LinuxDotNet.SystemInfo's SystemStat/CpuStat:
/// raw cumulative values are stored as-is, and usage-rate calculation is left to the caller.
/// </para>
/// </summary>
public sealed class CpuStat
{
    private readonly List<CpuCoreStat> cpuCores = [];

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>全コア合計の累積ティック数<br/>Aggregate cumulative tick counts across all cores</summary>
    public CpuCoreStat CpuTotal { get; } = new(-1);

    /// <summary>コアごとの累積ティック数 (インデックスはコア番号に対応)<br/>Per-core cumulative tick counts (index corresponds to core number)</summary>
    public IReadOnlyList<CpuCoreStat> CpuCores => cpuCores;

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private CpuStat()
    {
        Update();
    }

    /// <summary>CPU 統計スナップショットを生成し、初回 Update() を実行する。<br/>Creates a CPU statistics instance and performs the initial Update().</summary>
    public static CpuStat Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// host_processor_info() を呼び出して各コアの累積ティック数を更新する。
    /// 成功時は true、カーネル呼び出し失敗時は false を返す。
    /// <para>
    /// Refreshes cumulative tick counts for each core by calling host_processor_info().
    /// Returns true on success, false if the kernel call fails.
    /// </para>
    /// </summary>
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

            while (cpuCores.Count < processorCount)
            {
                cpuCores.Add(new CpuCoreStat(cpuCores.Count));
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
            _ = vm_deallocate(task_self_trap(), info, (IntPtr)(infoCnt * sizeof(int)));
        }
    }
}
