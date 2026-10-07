namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class LoadAverage : IDisposable
{
    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public double Average1 { get; private set; }

    public double Average5 { get; private set; }

    public double Average15 { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private LoadAverage()
    {
        Update();
    }

    internal static LoadAverage Create() => new();

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

        var values = stackalloc double[3];
        var count = getloadavg(values, 3);
        if (count < 3)
        {
            return false;
        }

        Average1 = values[0];
        Average5 = values[1];
        Average15 = values[2];

        UpdateAt = DateTime.Now;

        return true;
    }
}
