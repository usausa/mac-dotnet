namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class SwapUsage
{
    public DateTime UpdateAt { get; private set; }

    public ulong TotalBytes { get; private set; }

    public ulong AvailableBytes { get; private set; }

    public ulong UsedBytes { get; private set; }

    public int PageSize { get; private set; }

    public bool IsEncrypted { get; private set; }

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

    // ReSharper disable StringLiteralTypo
    public unsafe bool Update()
    {
        var size = (IntPtr)sizeof(xsw_usage);
        xsw_usage swap;
        if (sysctlbyname("vm.swapusage", &swap, ref size, IntPtr.Zero, 0) != 0)
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
    // ReSharper restore StringLiteralTypo
}
