namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class LoadAverage
{
    public DateTime UpdateAt { get; private set; }

    public double Average1 { get; private set; }

    public double Average5 { get; private set; }

    public double Average15 { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal LoadAverage()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
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
