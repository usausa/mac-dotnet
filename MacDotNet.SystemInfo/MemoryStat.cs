namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class MemoryStat
{
    private readonly uint host;

    public DateTime UpdateAt { get; private set; }

    // Physical Memory

    public ulong PhysicalMemory { get; }

    // Page Size

    public nuint PageSize { get; }

    // Memory Count

    public uint ActiveCount { get; private set; }

    public uint InactiveCount { get; private set; }

    public uint WireCount { get; private set; }

    public uint FreeCount { get; private set; }

    public ulong ZeroFillCount { get; private set; }

    public ulong ReactivatedCount { get; private set; }

    // Faults(Counter)

    public ulong Faults { get; private set; }

    public ulong CopyOnWriteFaults { get; private set; }

    // Page(Counter)

    public ulong PageIn { get; private set; }

    public ulong PageOut { get; private set; }

    // Swap(Counter)

    public ulong SwapIn { get; private set; }

    public ulong SwapOut { get; private set; }

    // Page Cache(Counter)

    public ulong Lookup { get; private set; }

    public ulong Hit { get; private set; }

    // Purgeable

    public uint PurgeableCount { get; private set; }

    // Purge(Counter)

    public ulong Purges { get; private set; }

    // Speculative

    public uint SpeculativeCount { get; private set; }

    // Throttled

    public uint ThrottledCount { get; private set; }

    // Compressor

    public uint CompressorPageCount { get; private set; }

    // Decompression/Compression(Counter)

    public ulong Decompression { get; private set; }

    public ulong Compression { get; private set; }

    // Page counts

    public uint ExternalPageCount { get; private set; }

    public uint InternalPageCount { get; private set; }

    // Compressor stats

    public ulong TotalUncompressedPagesInCompressor { get; private set; }

    // Swapped

    public ulong SwappedCount { get; private set; }

    // Bytes

    public ulong UsedBytes => ((ulong)ActiveCount + WireCount + CompressorPageCount) * PageSize;

    public ulong FreeBytes => PhysicalMemory > UsedBytes ? PhysicalMemory - UsedBytes : 0;

    public ulong ActiveBytes => (ulong)ActiveCount * PageSize;

    public ulong InactiveBytes => (ulong)InactiveCount * PageSize;

    public ulong WiredBytes => (ulong)WireCount * PageSize;

    public ulong CompressorBytes => (ulong)CompressorPageCount * PageSize;

    public ulong AppMemoryBytes => InternalPageCount > PurgeableCount ? ((ulong)InternalPageCount - PurgeableCount) * PageSize : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
    internal MemoryStat()
    {
        PhysicalMemory = GetSystemControlUInt64("hw.memsize");
        host = mach_host_self();
        _ = host_page_size(host, out var pageSize);
        PageSize = pageSize;
        Update();
    }
    // ReSharper restore StringLiteralTypo

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        var count = HOST_VM_INFO64_COUNT;
        vm_statistics64 vmStat;
        var ret = host_statistics64(host, HOST_VM_INFO64, &vmStat, ref count);
        if (ret != KERN_SUCCESS)
        {
            return false;
        }

        ActiveCount = vmStat.active_count;
        InactiveCount = vmStat.inactive_count;
        WireCount = vmStat.wire_count;
        FreeCount = vmStat.free_count;
        ZeroFillCount = vmStat.zero_fill_count;
        ReactivatedCount = vmStat.reactivations;
        Faults = vmStat.faults;
        CopyOnWriteFaults = vmStat.cow_faults;
        PageIn = vmStat.pageins;
        PageOut = vmStat.pageouts;
        SwapIn = vmStat.swapins;
        SwapOut = vmStat.swapouts;
        Lookup = vmStat.lookups;
        Hit = vmStat.hits;
        PurgeableCount = vmStat.purgeable_count;
        Purges = vmStat.purges;
        SpeculativeCount = vmStat.speculative_count;
        ThrottledCount = vmStat.throttled_count;
        CompressorPageCount = vmStat.compressor_page_count;
        Decompression = vmStat.decompressions;
        Compression = vmStat.compressions;
        ExternalPageCount = vmStat.external_page_count;
        InternalPageCount = vmStat.internal_page_count;
        TotalUncompressedPagesInCompressor = vmStat.total_uncompressed_pages_in_compressor;
        SwappedCount = vmStat.swapped_count;

        UpdateAt = DateTime.Now;

        return true;
    }
}
