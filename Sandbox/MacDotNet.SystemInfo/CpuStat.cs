namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>CPU コアごとの累積ティック数。内部で更新されるミュータブルクラス</summary>
public sealed class CpuCoreStat
{
    /// <summary>コア番号 (0 始まり)。CpuTotal の場合は -1</summary>
    public int CpuNumber { get; }

    /// <summary>ユーザーモードで消費した累積ティック数</summary>
    public uint User { get; internal set; }

    /// <summary>カーネルモードで消費した累積ティック数</summary>
    public uint System { get; internal set; }

    /// <summary>アイドル状態の累積ティック数</summary>
    public uint Idle { get; internal set; }

    /// <summary>nice 値で実行されたユーザーモードの累積ティック数</summary>
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
/// </summary>
public sealed class CpuStat
{
    private readonly List<CpuCoreStat> cpuCores = [];

    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>全コア合計の累積ティック数</summary>
    public CpuCoreStat CpuTotal { get; } = new(-1);

    /// <summary>コアごとの累積ティック数 (インデックスはコア番号に対応)</summary>
    public IReadOnlyList<CpuCoreStat> CpuCores => cpuCores;

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private CpuStat()
    {
        Update();
    }

    public static CpuStat Create() => new();

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
