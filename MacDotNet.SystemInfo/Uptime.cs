namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class Uptime : IDisposable
{
    private readonly int[] bootTimeMib;

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public TimeSpan Elapsed { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private Uptime()
    {
        bootTimeMib = GetSystemControlMib("kern.boottime");
        Update();
    }

    internal static Uptime Create() => new();

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

        if (bootTimeMib.Length == 0)
        {
            return false;
        }

        var time = new timeval { tv_sec = 0, tv_usec = 0 };
        var size = (IntPtr)sizeof(timeval);
        fixed (int* mib = bootTimeMib)
        {
            if (sysctl(mib, (uint)bootTimeMib.Length, &time, ref size, IntPtr.Zero, IntPtr.Zero) != 0)
            {
                return false;
            }
        }

        var boot = DateTimeOffset.FromUnixTimeMilliseconds((time.tv_sec * 1000) + (time.tv_usec / 1000));
        Elapsed = DateTimeOffset.Now - boot;

        UpdateAt = DateTime.Now;

        return true;
    }
}
