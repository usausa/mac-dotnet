namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

[Flags]
public enum MountOption
{
    None = 0,
    ReadOnly = 1 << 0,
    Synchronous = 1 << 1,
    NoExec = 1 << 2,
    NoSuid = 1 << 3,
    NoDevice = 1 << 4,
    Union = 1 << 5,
    Async = 1 << 6,
    Cprotect = 1 << 7,
    Exported = 1 << 8,
    Removable = 1 << 9,
    Quarantine = 1 << 10,
    Local = 1 << 12,
    Quota = 1 << 13,
    RootFs = 1 << 14,
    DoVolfs = 1 << 15,
    DontBrowse = 1 << 20,
    IgnoreOwnership = 1 << 21,
    AutoMounted = 1 << 22,
    Journaled = 1 << 23,
    NoUserExtendedAttribute = 1 << 24,
    DeferredWrite = 1 << 25,
    MultiLabel = 1 << 26,
    NoAccessTime = 1 << 28,
    Snapshot = 1 << 30
}

public sealed record FileSystemInfo
{
    public required string MountPoint { get; init; }

    public required string FileSystem { get; init; }

    public required string DeviceName { get; init; }

    public required uint BlockSize { get; init; }

    public required int IOSize { get; init; }

    // Block

    public required ulong TotalBlocks { get; init; }

    public required ulong FreeBlocks { get; init; }

    public required ulong AvailableBlocks { get; init; }

    // Size

    public ulong TotalSize => TotalBlocks * BlockSize;

    public ulong FreeSize => FreeBlocks * BlockSize;

    public ulong AvailableSize => AvailableBlocks * BlockSize;

    // Files

    public required ulong TotalFiles { get; init; }

    public required ulong FreeFiles { get; init; }

    public required MountOption Flags { get; init; }

    public required uint SubType { get; init; }

    public required uint OwnerUid { get; init; }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    internal static unsafe IReadOnlyList<FileSystemInfo> GetFileSystems(bool includeAll = false)
    {
        var count = getfsstat(null, 0, MNT_NOWAIT);
        if (count <= 0)
        {
            return [];
        }

        var buffer = new statfs[count];
        fixed (statfs* ptr = buffer)
        {
            var actual = getfsstat(ptr, count * sizeof(statfs), MNT_NOWAIT);
            if (actual <= 0)
            {
                return [];
            }

            var resultCount = Math.Min(actual, count);
            var result = new List<FileSystemInfo>(resultCount);
            for (var i = 0; i < resultCount; i++)
            {
                var flags = (MountOption)ptr[i].f_flags;
                if (!includeAll && !(flags.HasFlag(MountOption.Local) && !flags.HasFlag(MountOption.DontBrowse)))
                {
                    continue;
                }

                result.Add(new FileSystemInfo
                {
                    MountPoint = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntonname) ?? string.Empty,
                    FileSystem = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_fstypename) ?? string.Empty,
                    DeviceName = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntfromname) ?? string.Empty,
                    BlockSize = ptr[i].f_bsize,
                    IOSize = ptr[i].f_iosize,
                    TotalBlocks = ptr[i].f_blocks,
                    FreeBlocks = ptr[i].f_bfree,
                    AvailableBlocks = ptr[i].f_bavail,
                    TotalFiles = ptr[i].f_files,
                    FreeFiles = ptr[i].f_ffree,
                    Flags = flags,
                    SubType = ptr[i].f_fssubtype,
                    OwnerUid = ptr[i].f_owner
                });
            }

            return result;
        }
    }
}
