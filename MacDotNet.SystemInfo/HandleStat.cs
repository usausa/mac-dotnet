namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class HandleStat
{
    public DateTime UpdateAt { get; private set; }

    public int OpenFiles { get; private set; }

    public int OpenVnodes { get; private set; }

    public int OpenSockets { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal HandleStat()
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
        OpenSockets = GetSystemControlInt32("kern.ipc.numopensockets");

        UpdateAt = DateTime.Now;

        return true;
    }
    // ReSharper restore StringLiteralTypo
}
