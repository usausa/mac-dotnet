namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed record FileSystemEntry
{
    /// <summary>マウントポイントのパス。例: "/"、"/Volumes/Storage"</summary>
    public required string MountPoint { get; init; }

    /// <summary>ファイルシステムの種類。例: "apfs"、"devfs"、"autofs"</summary>
    public required string TypeName { get; init; }

    /// <summary>マウントされているデバイスのパスまたは名前。例: "/dev/disk3s1s1"</summary>
    public required string DeviceName { get; init; }

    /// <summary>ファイルシステムの基本ブロックサイズ (バイト)</summary>
    public required uint BlockSize { get; init; }

    /// <summary>最適な I/O 転送サイズ (バイト)</summary>
    public required int IOSize { get; init; }

    /// <summary>総ブロック数</summary>
    public required ulong TotalBlocks { get; init; }

    /// <summary>空きブロック数 (スーパーユーザー向けを含む)</summary>
    public required ulong FreeBlocks { get; init; }

    /// <summary>一般ユーザーが利用可能な空きブロック数</summary>
    public required ulong AvailableBlocks { get; init; }

    /// <summary>総容量 (バイト)</summary>
    public ulong TotalSize => TotalBlocks * BlockSize;

    /// <summary>空き容量 (バイト)。スーパーユーザー向けの予約領域を含む</summary>
    public ulong FreeSize => FreeBlocks * BlockSize;

    /// <summary>一般ユーザーが利用可能な空き容量 (バイト)</summary>
    public ulong AvailableSize => AvailableBlocks * BlockSize;

    /// <summary>ディスク使用率 (0.0〜1.0)。(総ブロック - 利用可能ブロック) / 総ブロック</summary>
    public double UsagePercent => TotalBlocks > 0 ? (double)(TotalBlocks - AvailableBlocks) / TotalBlocks : 0;

    /// <summary>ファイルノード (inode) の最大数</summary>
    public required ulong TotalFiles { get; init; }

    /// <summary>空きファイルノード (inode) の数</summary>
    public required ulong FreeFiles { get; init; }

    /// <summary>マウントフラグのビットフィールド (MNT_RDONLY など)</summary>
    public required uint Flags { get; init; }

    /// <summary>ファイルシステムのサブタイプ識別子</summary>
    public required uint SubType { get; init; }

    /// <summary>ファイルシステムの所有者のユーザー ID</summary>
    public required uint OwnerUid { get; init; }

    /// <summary>読み取り専用でマウントされているかどうか</summary>
    public bool IsReadOnly => (Flags & MNT_RDONLY) != 0;

    /// <summary>ローカルファイルシステムかどうか。false の場合はネットワークまたは仮想</summary>
    public bool IsLocal => (Flags & MNT_LOCAL) != 0;
}

public sealed record DiskVolume
{
    /// <summary>マウントポイントのパス。例: "/"、"/Volumes/Macintosh HD"</summary>
    public required string MountPoint { get; init; }

    /// <summary>ファイルシステムの種類。例: "apfs"、"hfs"、"exfat"</summary>
    public required string TypeName { get; init; }

    /// <summary>マウントされているデバイスのパス。例: "/dev/disk3s1s1"</summary>
    public required string DeviceName { get; init; }

    /// <summary>読み取り専用でマウントされているかどうか</summary>
    public required bool IsReadOnly { get; init; }
}

public static class FileSystemInfo
{
    public static unsafe DiskVolume[] GetDiskVolumes()
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
            var result = new List<DiskVolume>(resultCount);
            for (var i = 0; i < resultCount; i++)
            {
                if ((ptr[i].f_flags & MNT_LOCAL) == 0)
                {
                    continue;
                }

                result.Add(new DiskVolume
                {
                    MountPoint = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntonname) ?? string.Empty,
                    TypeName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_fstypename) ?? string.Empty,
                    DeviceName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntfromname) ?? string.Empty,
                    IsReadOnly = (ptr[i].f_flags & MNT_RDONLY) != 0,
                });
            }

            return [.. result];
        }
    }

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
                result[i] = new FileSystemEntry
                {
                    MountPoint = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntonname) ?? string.Empty,
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
                };
            }

            return result;
        }
    }
}
