namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// statfs 構造体の f_flags フィールドに対応するマウントフラグ (sys/mount.h)。
/// <para>Mount flags corresponding to the f_flags field of the statfs structure (sys/mount.h).</para>
/// </summary>
[Flags]
public enum MountFlags : uint
{
    None             = 0,
    ReadOnly         = 1u << 0,   // MNT_RDONLY      — 読み取り専用 / Read-only filesystem
    Synchronous      = 1u << 1,   // MNT_SYNCHRONOUS — 同期書き込み / Synchronous writes
    NoExec           = 1u << 2,   // MNT_NOEXEC      — 実行禁止 / No execution of binaries
    NoSuid           = 1u << 3,   // MNT_NOSUID      — SUID/SGID 無効 / Ignore SUID/SGID bits
    Union            = 1u << 5,   // MNT_UNION       — ユニオンマウント / Union mount
    Async            = 1u << 6,   // MNT_ASYNC       — 非同期書き込み / Asynchronous writes
    Exported         = 1u << 8,   // MNT_EXPORTED    — NFS エクスポート済み / Exported via NFS
    Quarantine       = 1u << 10,  // MNT_QUARANTINE  — 隔離済み / Quarantined
    Local            = 1u << 12,  // MNT_LOCAL       — ローカル FS / Local filesystem (not network)
    Quota            = 1u << 13,  // MNT_QUOTA       — クォータ有効 / Disk quotas enabled
    RootFs           = 1u << 14,  // MNT_ROOTFS      — ルート FS / Root filesystem
    DontBrowse       = 1u << 20,  // MNT_DONTBROWSE  — Finder に非表示 / Hidden from Finder browsing
    IgnoreOwnership  = 1u << 21,  // MNT_IGNORE_OWNERSHIP — 所有者無視 / Ignore file ownership
    AutoMounted      = 1u << 22,  // MNT_AUTOMOUNTED — 自動マウント / Automounted
    Journaled        = 1u << 23,  // MNT_JOURNALED   — ジャーナリング有効 / Journaling enabled
    NoUserXattr      = 1u << 24,  // MNT_NOUSERXATTR — ユーザー拡張属性禁止 / No user extended attributes
    DefWrite         = 1u << 25,  // MNT_DEFWRITE    — 遅延書き込み / Deferred writes
    MultiLabel       = 1u << 26,  // MNT_MULTILABEL  — MAC マルチラベル / MAC multi-label
    NoAtime          = 1u << 28,  // MNT_NOATIME     — atime 更新無効 / Do not update access times
    Snapshot         = 1u << 30,  // MNT_SNAPSHOT    — APFS スナップショット / APFS snapshot mount
}

/// <summary>
/// マウント済みファイルシステムの詳細情報 (statfs 構造体から取得)。
/// <see cref="GetAll"/> でシステム上の全エントリを取得する。
/// <para>
/// Detailed information for a mounted file system (from the statfs structure).
/// Use <see cref="GetAll"/> to retrieve all entries on the system.
/// </para>
/// </summary>
public sealed record FileSystemInfo
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

    /// <summary>マウントフラグ (sys/mount.h の f_flags に対応)<br/>Mount flags corresponding to f_flags in sys/mount.h</summary>
    public required MountFlags Flags { get; init; }

    /// <summary>ファイルシステムのサブタイプ識別子<br/>File system subtype identifier</summary>
    public required uint SubType { get; init; }

    /// <summary>ファイルシステムの所有者のユーザー ID<br/>User ID of the file system owner</summary>
    public required uint OwnerUid { get; init; }

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

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    /// <summary>
    /// getfsstat(2) が返すすべてのマウント済みファイルシステムの情報を返す。
    /// 仮想・ネットワーク・内部ボリュームを含む全エントリが対象。
    /// ユーザーから見えるボリューム ("/" および "/Volumes/*") は <see cref="IsUserVisible"/> が true。
    /// <para>
    /// Returns information for all mounted file systems reported by getfsstat(2).
    /// Includes virtual, network, and internal volumes.
    /// Entries visible to users ("/" and "/Volumes/*") have <see cref="IsUserVisible"/> set to true.
    /// </para>
    /// </summary>
    public static unsafe FileSystemInfo[] GetAll()
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
            var result = new FileSystemInfo[resultCount];

            for (var i = 0; i < resultCount; i++)
            {
                result[i] = new FileSystemInfo
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
                    Flags = (MountFlags)ptr[i].f_flags,
                    SubType = ptr[i].f_fssubtype,
                    OwnerUid = ptr[i].f_owner,
                };
            }

            return result;
        }
    }
}
