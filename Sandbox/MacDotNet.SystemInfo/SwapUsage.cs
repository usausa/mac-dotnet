namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class SwapUsage
{
    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>スワップ領域の総容量 (バイト)</summary>
    public ulong TotalBytes { get; private set; }

    /// <summary>スワップ領域の空き容量 (バイト)</summary>
    public ulong AvailableBytes { get; private set; }

    /// <summary>スワップ領域の使用量 (バイト)</summary>
    public ulong UsedBytes { get; private set; }

    /// <summary>スワップのページサイズ (バイト)</summary>
    public int PageSize { get; private set; }

    /// <summary>スワップが暗号化されているかどうか</summary>
    public bool IsEncrypted { get; private set; }

    //public double UsagePercent => TotalBytes > 0 ? 100.0 * UsedBytes / TotalBytes : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal SwapUsage()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        xsw_usage swap;
        var len = (IntPtr)sizeof(xsw_usage);
        if (sysctlbyname("vm.swapusage", &swap, ref len, IntPtr.Zero, 0) != 0)
        {
            return false;
        }

        TotalBytes = swap.xsu_total;
        AvailableBytes = swap.xsu_avail;
        UsedBytes = swap.xsu_used;
        PageSize = swap.xsu_pagesize;
        IsEncrypted = swap.xsu_encrypted != 0;

        UpdateAt = DateTime.Now;

        return true;
    }
}
