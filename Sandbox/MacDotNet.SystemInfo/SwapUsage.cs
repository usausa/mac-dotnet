namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// vm.swapusage sysctl から取得したスワップ領域の使用状況。
/// <para>Swap space usage retrieved from the vm.swapusage sysctl.</para>
/// </summary>
public sealed class SwapUsage
{
    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>スワップ領域の総容量 (バイト)<br/>Total swap space capacity in bytes</summary>
    public ulong TotalBytes { get; private set; }

    /// <summary>スワップ領域の空き容量 (バイト)<br/>Available (free) swap space in bytes</summary>
    public ulong AvailableBytes { get; private set; }

    /// <summary>スワップ領域の使用量 (バイト)<br/>Used swap space in bytes</summary>
    public ulong UsedBytes { get; private set; }

    /// <summary>スワップのページサイズ (バイト)<br/>Swap page size in bytes</summary>
    public int PageSize { get; private set; }

    /// <summary>スワップが暗号化されているかどうか<br/>Whether swap space is encrypted</summary>
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

    /// <summary>
    /// vm.swapusage sysctl を呼び出してスワップ統計を更新する。
    /// 成功時は true、失敗時は false を返す。
    /// <para>
    /// Refreshes swap statistics by calling the vm.swapusage sysctl.
    /// Returns true on success, false on failure.
    /// </para>
    /// </summary>
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
