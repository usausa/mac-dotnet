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
    ContentProtection = 1 << 7,
    Exported = 1 << 8,
    Removable = 1 << 9,
    Quarantine = 1 << 10,
    Local = 1 << 12,
    Quota = 1 << 13,
    RootFs = 1 << 14,
    DoVolumeFs = 1 << 15,
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

public sealed class FileSystemEntry
{
    internal bool Live { get; set; }

    // Identity

    public string MountPoint { get; }

    public string FileSystem { get; }

    public string DeviceName { get; }

    // Block

    public uint BlockSize { get; internal set; }

    public int IOSize { get; internal set; }

    public ulong TotalBlocks { get; internal set; }

    public ulong FreeBlocks { get; internal set; }

    public ulong AvailableBlocks { get; internal set; }

    // Size

    public ulong TotalSize => TotalBlocks * BlockSize;

    public ulong FreeSize => FreeBlocks * BlockSize;

    public ulong AvailableSize => AvailableBlocks * BlockSize;

    // Files

    public ulong TotalFiles { get; internal set; }

    public ulong FreeFiles { get; internal set; }

    public MountOption Option { get; internal set; }

    public uint SubType { get; internal set; }

    public uint OwnerUid { get; internal set; }

    internal FileSystemEntry(string mountPoint, string fileSystem, string deviceName)
    {
        MountPoint = mountPoint;
        FileSystem = fileSystem;
        DeviceName = deviceName;
    }
}

public sealed class FileSystemStat
{
    private readonly bool includeAll;

    private readonly List<FileSystemEntry> entries = new();

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<FileSystemEntry> Entries => entries;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal FileSystemStat(bool includeAll = false)
    {
        this.includeAll = includeAll;
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        var count = getfsstat(null, 0, MNT_NOWAIT);
        if (count <= 0)
        {
            return false;
        }

        var buffer = new statfs[count];
        fixed (statfs* ptr = buffer)
        {
            var actual = getfsstat(ptr, count * sizeof(statfs), MNT_NOWAIT);
            if (actual <= 0)
            {
                return false;
            }

            foreach (var entry in entries)
            {
                entry.Live = false;
            }

            var added = false;
            count = Math.Min(actual, count);
            for (var i = 0; i < count; i++)
            {
                var option = (MountOption)ptr[i].f_flags;
                if (!includeAll && !(option.HasFlag(MountOption.Local) && !option.HasFlag(MountOption.DontBrowse)))
                {
                    continue;
                }

                var mountPoint = Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntonname) ?? string.Empty;

                var entry = default(FileSystemEntry);
                foreach (var item in entries)
                {
                    if (item.MountPoint == mountPoint)
                    {
                        entry = item;
                        break;
                    }
                }

                if (entry is null)
                {
                    entry = new FileSystemEntry(
                        mountPoint,
                        Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_fstypename) ?? string.Empty,
                        Marshal.PtrToStringUTF8((IntPtr)ptr[i].f_mntfromname) ?? string.Empty);
                    entries.Add(entry);
                    added = true;
                }

                entry.BlockSize = ptr[i].f_bsize;
                entry.IOSize = ptr[i].f_iosize;
                entry.TotalBlocks = ptr[i].f_blocks;
                entry.FreeBlocks = ptr[i].f_bfree;
                entry.AvailableBlocks = ptr[i].f_bavail;
                entry.TotalFiles = ptr[i].f_files;
                entry.FreeFiles = ptr[i].f_ffree;
                entry.Option = option;
                entry.SubType = ptr[i].f_fssubtype;
                entry.OwnerUid = ptr[i].f_owner;

                entry.Live = true;
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (!entries[i].Live)
                {
                    entries.RemoveAt(i);
                }
            }

            if (added)
            {
                entries.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.MountPoint, y.MountPoint));
            }

            UpdateAt = DateTime.Now;

            return true;
        }
    }
}
