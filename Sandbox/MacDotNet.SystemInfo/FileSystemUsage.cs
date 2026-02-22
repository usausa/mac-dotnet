namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class FileSystemUsage
{
    /// <summary>マウントポイントのパス</summary>
    public string MountPoint { get; }

    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>総容量 (バイト)</summary>
    public ulong TotalSize { get; private set; }

    /// <summary>空き容量 (バイト)。スーパーユーザー向けの予約領域を含む</summary>
    public ulong FreeSize { get; private set; }

    /// <summary>一般ユーザーが利用可能な空き容量 (バイト)</summary>
    public ulong AvailableSize { get; private set; }

    /// <summary>ファイルシステムのブロックサイズ (バイト)</summary>
    public ulong BlockSize { get; private set; }

    /// <summary>ファイルノード (inode) の最大数</summary>
    public ulong TotalFiles { get; private set; }

    /// <summary>空きファイルノード (inode) の数</summary>
    public ulong FreeFiles { get; private set; }

    /// <summary>ディスク使用率 (0.0〜1.0)</summary>
    public double UsagePercent => TotalSize > 0 ? (double)(TotalSize - AvailableSize) / TotalSize : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal FileSystemUsage(string mountPoint)
    {
        MountPoint = mountPoint;
        Update();
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static FileSystemUsage Create(string mountPoint) => new(mountPoint);

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
