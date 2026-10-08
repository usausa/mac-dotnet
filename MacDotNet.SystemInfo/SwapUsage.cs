namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class SwapUsage : IDisposable
{
    private readonly int[] swapUsageMib;

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public ulong TotalBytes { get; private set; }

    public ulong AvailableBytes { get; private set; }

    public ulong UsedBytes { get; private set; }

    public uint PageSize { get; private set; }

    public bool IsEncrypted { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private SwapUsage()
    {
        swapUsageMib = GetSystemControlMib("vm.swapusage");
        Update();
    }

    internal static SwapUsage Create() => new();

    public void Dispose()
    {
        disposed = true;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (swapUsageMib.Length == 0)
        {
            return false;
        }

        var size = (IntPtr)sizeof(xsw_usage);
        xsw_usage swap;
        fixed (int* mib = swapUsageMib)
        {
            if (sysctl(mib, (uint)swapUsageMib.Length, &swap, ref size, IntPtr.Zero, IntPtr.Zero) != 0)
            {
                return false;
            }
        }

        TotalBytes = swap.xsu_total;
        AvailableBytes = swap.xsu_avail;
        UsedBytes = swap.xsu_used;
        PageSize = (uint)swap.xsu_pagesize;
        IsEncrypted = swap.xsu_encrypted != 0;

        UpdateAt = DateTime.Now;

        return true;
    }
}
