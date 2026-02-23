namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class FileSystemUsage
{
    public string MountPoint { get; }

    public DateTime UpdateAt { get; private set; }

    public ulong TotalSize { get; private set; }

    public ulong FreeSize { get; private set; }

    public ulong AvailableSize { get; private set; }

    public ulong BlockSize { get; private set; }

    public ulong TotalFiles { get; private set; }

    public ulong FreeFiles { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal FileSystemUsage(string mountPoint)
    {
        MountPoint = mountPoint;
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        statfs buf;
        if (statfs_path(MountPoint, &buf) != 0)
        {
            return false;
        }

        var blockSize = (ulong)buf.f_bsize;
        TotalSize = buf.f_blocks * blockSize;
        FreeSize = buf.f_bfree * blockSize;
        AvailableSize = buf.f_bavail * blockSize;
        BlockSize = blockSize;
        TotalFiles = buf.f_files;
        FreeFiles = buf.f_ffree;

        UpdateAt = DateTime.Now;

        return true;
    }
}
