namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// 特定のマウントポイントのディスク使用量を管理するクラス。
/// Update() を呼ぶたびに statfs(2) で最新値を取得する。
/// <para>
/// Manages disk usage for a specific mount point.
/// Each call to Update() fetches the latest values via statfs(2).
/// </para>
/// </summary>
public sealed class FileSystemUsage
{
    /// <summary>マウントポイントのパス<br/>Mount point path</summary>
    public string MountPoint { get; }

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>総容量 (バイト)<br/>Total capacity in bytes</summary>
    public ulong TotalSize { get; private set; }

    /// <summary>空き容量 (バイト)。スーパーユーザー向けの予約領域を含む<br/>Free capacity in bytes, including superuser-reserved space</summary>
    public ulong FreeSize { get; private set; }

    /// <summary>一般ユーザーが利用可能な空き容量 (バイト)<br/>Available capacity in bytes for non-superuser</summary>
    public ulong AvailableSize { get; private set; }

    /// <summary>ファイルシステムのブロックサイズ (バイト)<br/>File system block size in bytes</summary>
    public ulong BlockSize { get; private set; }

    /// <summary>ファイルノード (inode) の最大数<br/>Maximum number of file nodes (inodes)</summary>
    public ulong TotalFiles { get; private set; }

    /// <summary>空きファイルノード (inode) の数<br/>Number of free file nodes (inodes)</summary>
    public ulong FreeFiles { get; private set; }

    /// <summary>ディスク使用率 (0.0〜1.0)<br/>Disk usage ratio (0.0 to 1.0)</summary>
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

    /// <summary>指定したマウントポイントの FileSystemUsage インスタンスを生成する。<br/>Creates a FileSystemUsage instance for the specified mount point.</summary>
    public static FileSystemUsage Create(string mountPoint) => new(mountPoint);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// statfs(2) を呼び出してディスク使用量を更新する。
    /// 成功時は true、マウントポイントが無効な場合は false を返す。
    /// <para>
    /// Refreshes disk usage by calling statfs(2).
    /// Returns true on success, false if the mount point is invalid.
    /// </para>
    /// </summary>
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
