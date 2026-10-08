namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class FileHandleStat : IDisposable
{
    private readonly int[] numFilesMib;

    private readonly int[] numVnodesMib;

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public int OpenFiles { get; private set; }

    public int OpenVnodes { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
    private FileHandleStat()
    {
        numFilesMib = GetSystemControlMib("kern.num_files");
        numVnodesMib = GetSystemControlMib("kern.num_vnodes");
        Update();
    }
    // ReSharper restore StringLiteralTypo

    internal static FileHandleStat Create() => new();

    public void Dispose()
    {
        disposed = true;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if ((numFilesMib.Length == 0) || (numVnodesMib.Length == 0))
        {
            return false;
        }

        OpenFiles = GetSystemControlInt32(numFilesMib);
        OpenVnodes = GetSystemControlInt32(numVnodesMib);

        UpdateAt = DateTime.Now;

        return true;
    }
}
