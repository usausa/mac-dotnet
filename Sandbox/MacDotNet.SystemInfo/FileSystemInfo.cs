namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// マウント済みファイルシステムの詳細情報 (statfs 構造体から取得)。
/// <para>Detailed information for a mounted file system (from the statfs structure).</para>
/// </summary>
public sealed record FileSystemEntry
{
    /// <summary>マウントポイントのパス。例: "/"、"/Volumes/Storage"<br/>Mount point path. Example: "/", "/Volumes/Storage"</summary>
    public required string MountPoint { get; init; }

    /// <summary>ファイルシステムの種類。例: "apfs"、"devfs"、"autofs"<br/>File system type name. Example: "apfs", "devfs", "autofs"</summary>
    public required string TypeName { get; init; }

    /// <summary>マウントされているデバイスのパスまたは名前。例: "/dev/disk3s1s1"<br/>Path or name of the mounted device. Example: "/dev/disk3s1s1"</summary>
    public required string DeviceName { get; init; }

    /// <summary>ファイルシステムの基本ブロックサイズ (バイト)<br/>Fundamental file system block size in bytes</summary>
    public required uint BlockSize { get; init; }

    /// <summary>最適な I/O 転送サイズ (バイト)<br/>Optimal I/O transfer size in bytes</summary>
    public required int IOSize { get; init; }

    /// <summary>総ブロック数<br/>Total number of blocks</summary>
    public required ulong TotalBlocks { get; init; }

    /// <summary>空きブロック数 (スーパーユーザー向けを含む)<br/>Number of free blocks (includes superuser-reserved blocks)</summary>
    public required ulong FreeBlocks { get; init; }

    /// <summary>一般ユーザーが利用可能な空きブロック数<br/>Number of free blocks available to non-superuser</summary>
    public required ulong AvailableBlocks { get; init; }

    /// <summary>総容量 (バイト)<br/>Total capacity in bytes</summary>
    public ulong TotalSize => TotalBlocks * BlockSize;

    /// <summary>空き容量 (バイト)。スーパーユーザー向けの予約領域を含む<br/>Free capacity in bytes including superuser-reserved space</summary>
    public ulong FreeSize => FreeBlocks * BlockSize;

    /// <summary>一般ユーザーが利用可能な空き容量 (バイト)<br/>Available capacity in bytes for non-superuser</summary>
    public ulong AvailableSize => AvailableBlocks * BlockSize;

    /// <summary>ディスク使用率 (0.0〜1.0)。(総ブロック - 利用可能ブロック) / 総ブロック<br/>Disk usage ratio (0.0 to 1.0). (TotalBlocks - AvailableBlocks) / TotalBlocks</summary>
    public double UsagePercent => TotalBlocks > 0 ? (double)(TotalBlocks - AvailableBlocks) / TotalBlocks : 0;

    /// <summary>ファイルノード (inode) の最大数<br/>Maximum number of file nodes (inodes)</summary>
    public required ulong TotalFiles { get; init; }

    /// <summary>空きファイルノード (inode) の数<br/>Number of free file nodes (inodes)</summary>
    public required ulong FreeFiles { get; init; }

    /// <summary>マウントフラグのビットフィールド (MNT_RDONLY など)<br/>Mount flags bit field (e.g. MNT_RDONLY)</summary>
    public required uint Flags { get; init; }

    /// <summary>ファイルシステムのサブタイプ識別子<br/>File system subtype identifier</summary>
    public required uint SubType { get; init; }

    /// <summary>ファイルシステムの所有者のユーザー ID<br/>User ID of the file system owner</summary>
    public required uint OwnerUid { get; init; }

    /// <summary>読み取り専用でマウントされているかどうか<br/>Whether the file system is mounted read-only</summary>
    public bool IsReadOnly => (Flags & MNT_RDONLY) != 0;

    /// <summary>ローカルファイルシステムかどうか。false の場合はネットワークまたは仮想<br/>Whether the file system is local. False indicates network or virtual.</summary>
    public bool IsLocal => (Flags & MNT_LOCAL) != 0;

    /// <summary>
    /// ユーザーから見えるボリュームかどうか。
    /// ローカル FS のうち "/" (Macintosh HD) および "/Volumes/*" (隠しディレクトリ除外) のみ true。
    /// "/System/Volumes/*" 等の APFS 内部ボリュームや "/Volumes/.timemachine" 等は false。
    /// <para>
    /// Whether this is a user-visible volume.
    /// True only for local file systems mounted at "/" or "/Volumes/*" (excluding hidden directories).
    /// False for APFS internal volumes such as /System/Volumes/* and hidden mounts like /Volumes/.timemachine.
    /// </para>
    /// </summary>
    public required bool IsUserVisible { get; init; }

    /// <summary>
    /// DeviceName から物理ディスクの BSD 名を返す。
    /// 例: "/dev/disk3s1s1" → "disk3"、取得できない場合は null。
    /// <para>Returns the physical disk BSD name extracted from DeviceName.
    /// Example: "/dev/disk3s1s1" → "disk3". Returns null if unavailable.</para>
    /// </summary>
    public string? PhysicalDiskName
    {
        get
        {
            const string prefix = "/dev/disk";
            if (!DeviceName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            var span = DeviceName.AsSpan(prefix.Length);
            var n = 0;
            while (n < span.Length && char.IsAsciiDigit(span[n]))
            {
                n++;
            }

            return n > 0 ? string.Concat("disk", span[..n]) : null;
        }
    }
}

/// <summary>
/// ファイルシステムの列挙ユーティリティ。
/// <para>Utility class for enumerating file systems.</para>
/// </summary>
public static class FileSystemInfo
{
    /// <summary>
    /// getfsstat(2) が返すすべてのマウント済みファイルシステムの詳細情報を返す。
    /// 仮想・ネットワーク・内部ボリュームを含む全エントリが対象。
    /// ユーザーから見えるボリューム ("/" および "/Volumes/*") は <see cref="FileSystemEntry.IsUserVisible"/> が true。
    /// <para>
    /// Returns detailed information for all mounted file systems reported by getfsstat(2).
    /// Includes virtual, network, and internal volumes.
    /// Entries visible to users ("/" and "/Volumes/*") have <see cref="FileSystemEntry.IsUserVisible"/> set to true.
    /// </para>
    /// </summary>
    public static unsafe FileSystemEntry[] GetFileSystems()
    {
        var count = getfsstat(null, 0, MNT_NOWAIT);
        if (count <= 0)
        {
            return [];
        }

        var buffer = new statfs[count];
        fixed (statfs* ptr = buffer)
        {
            var bufsize = count * sizeof(statfs);
            var actual = getfsstat(ptr, bufsize, MNT_NOWAIT);
            if (actual <= 0)
            {
                return [];
            }

            var resultCount = Math.Min(actual, count);
            var result = new FileSystemEntry[resultCount];

            for (var i = 0; i < resultCount; i++)
            {
                var mountPoint = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntonname) ?? string.Empty;
                var isRoot = mountPoint == "/";
                var isUserVolume = mountPoint.StartsWith("/Volumes/", StringComparison.Ordinal)
                    && !mountPoint["/Volumes/".Length..].StartsWith(".", StringComparison.Ordinal);
                var isUserVisible = (ptr[i].f_flags & MNT_LOCAL) != 0 && (isRoot || isUserVolume);

                result[i] = new FileSystemEntry
                {
                    MountPoint = mountPoint,
                    TypeName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_fstypename) ?? string.Empty,
                    DeviceName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntfromname) ?? string.Empty,
                    BlockSize = ptr[i].f_bsize,
                    IOSize = ptr[i].f_iosize,
                    TotalBlocks = ptr[i].f_blocks,
                    FreeBlocks = ptr[i].f_bfree,
                    AvailableBlocks = ptr[i].f_bavail,
                    TotalFiles = ptr[i].f_files,
                    FreeFiles = ptr[i].f_ffree,
                    Flags = ptr[i].f_flags,
                    SubType = ptr[i].f_fssubtype,
                    OwnerUid = ptr[i].f_owner,
                    IsUserVisible = isUserVisible,
                };
            }

            return result;
        }
    }
}
