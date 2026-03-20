namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class FileHandleStat
{
    public DateTime UpdateAt { get; private set; }

    public int OpenFiles { get; private set; }

    public int OpenVnodes { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal FileHandleStat()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
    public bool Update()
    {
        OpenFiles = GetSystemControlInt32("kern.num_files");
        OpenVnodes = GetSystemControlInt32("kern.num_vnodes");

        UpdateAt = DateTime.Now;

        return true;
    }
    // ReSharper restore StringLiteralTypo
}
