namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class MemoryStat
{
    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>物理メモリの総量 (バイト) (hw.memsize)</summary>
    public ulong PhysicalMemory { get; private set; }

    /// <summary>システムのページサイズ (バイト)</summary>
    public nuint PageSize { get; private set; }

    /// <summary>アクティブページ数。最近アクセスされ RAM 上にあるページ</summary>
    public uint ActiveCount { get; private set; }

    /// <summary>非アクティブページ数。しばらくアクセスされていないがまだ RAM 上にあるページ</summary>
    public uint InactiveCount { get; private set; }

    /// <summary>ワイヤードページ数。カーネルやドライバがスワップ不可で確保しているページ</summary>
    public uint WireCount { get; private set; }

    /// <summary>空きページ数。即座に使用可能なページ</summary>
    public uint FreeCount { get; private set; }

    /// <summary>ゼロ充填ページの累積数。新規割り当て時にゼロ初期化されたページの総数</summary>
    public ulong ZeroFillCount { get; private set; }

    /// <summary>非アクティブリストから再アクティブ化されたページの累積数</summary>
    public ulong ReactivatedCount { get; private set; }

    /// <summary>ページフォルト発生の累積数。アクセス時にページが RAM 上になかった回数</summary>
    public ulong Faults { get; private set; }

    /// <summary>Copy-on-Write フォルトの累積数。書き込み時コピーが発生した回数</summary>
    public ulong CowFaults { get; private set; }

    /// <summary>ページイン累積数。ディスク (スワップ) から RAM に読み込んだページ数</summary>
    public ulong Pageins { get; private set; }

    /// <summary>ページアウト累積数。RAM からディスク (スワップ) に書き出したページ数</summary>
    public ulong Pageouts { get; private set; }

    /// <summary>スワップイン累積数。圧縮ストアまたはスワップから展開・読み込んだページ数</summary>
    public ulong Swapins { get; private set; }

    /// <summary>スワップアウト累積数。圧縮ストアまたはスワップに書き出したページ数</summary>
    public ulong Swapouts { get; private set; }

    /// <summary>ページキャッシュ検索の累積数</summary>
    public ulong Lookups { get; private set; }

    /// <summary>ページキャッシュヒットの累積数</summary>
    public ulong Hits { get; private set; }

    /// <summary>パージ可能ページ数。アプリが明示的に解放を許可したページ</summary>
    public uint PurgeableCount { get; private set; }

    /// <summary>パージ累積数。パージ可能ページが実際に解放された回数</summary>
    public ulong Purges { get; private set; }

    /// <summary>投機的ページ数。先読みにより確保されたが未アクセスのページ</summary>
    public uint SpeculativeCount { get; private set; }

    /// <summary>スロットルページ数。I/O スロットリング対象のページ</summary>
    public uint ThrottledCount { get; private set; }

    /// <summary>コンプレッサーが保持するページ数。メモリ圧縮によって圧縮されたページ</summary>
    public uint CompressorPageCount { get; private set; }

    /// <summary>コンプレッサーによる展開の累積数</summary>
    public ulong Decompressions { get; private set; }

    /// <summary>コンプレッサーによる圧縮の累積数</summary>
    public ulong Compressions { get; private set; }

    /// <summary>外部ページ数。ファイルキャッシュなどに使用されているページ</summary>
    public uint ExternalPageCount { get; private set; }

    /// <summary>内部ページ数。アプリが使用しているページ (Purgeable を含む)</summary>
    public uint InternalPageCount { get; private set; }

    /// <summary>コンプレッサー内の圧縮前の総ページ数。CompressionRatio 計算に使用</summary>
    public ulong TotalUncompressedPagesInCompressor { get; private set; }

    /// <summary>スワップアウト済みのページ数。圧縮後もスワップに書き出されたページ</summary>
    public ulong SwappedCount { get; private set; }

    /// <summary>使用中のメモリ量 (バイト)。Active + Wired + Compressor の合計</summary>
    public ulong UsedBytes => ((ulong)ActiveCount + WireCount + CompressorPageCount) * PageSize;

    /// <summary>空きメモリ量 (バイト)。PhysicalMemory - UsedBytes</summary>
    public ulong FreeBytes => PhysicalMemory > UsedBytes ? PhysicalMemory - UsedBytes : 0;

    /// <summary>アクティブメモリ量 (バイト)</summary>
    public ulong ActiveBytes => (ulong)ActiveCount * PageSize;

    /// <summary>非アクティブメモリ量 (バイト)</summary>
    public ulong InactiveBytes => (ulong)InactiveCount * PageSize;

    /// <summary>ワイヤードメモリ量 (バイト)。カーネルが固定確保しているメモリ</summary>
    public ulong WiredBytes => (ulong)WireCount * PageSize;

    /// <summary>コンプレッサーが使用するメモリ量 (バイト)</summary>
    public ulong CompressorBytes => (ulong)CompressorPageCount * PageSize;

    /// <summary>アプリが使用するメモリ量 (バイト)。Internal - Purgeable の合計</summary>
    public ulong AppMemoryBytes => InternalPageCount > PurgeableCount
        ? ((ulong)InternalPageCount - PurgeableCount) * PageSize
        : 0;

    /// <summary>メモリ使用率 (0.0〜1.0)</summary>
    public double UsagePercent => PhysicalMemory > 0 ? (double)UsedBytes / PhysicalMemory : 0;

    /// <summary>メモリ圧縮率。圧縮前のページ数 / 圧縮後のページ数。値が大きいほど圧縮効率が高い</summary>
    public double CompressionRatio => CompressorPageCount > 0
        ? (double)TotalUncompressedPagesInCompressor / CompressorPageCount
        : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal MemoryStat()
    {
        PhysicalMemory = GetSystemControlUInt64("hw.memsize");
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        var host = mach_host_self();
        var ret = host_page_size(host, out var pageSize);
        if (ret != KERN_SUCCESS)
        {
            return false;
        }

        var count = HOST_VM_INFO64_COUNT;
        vm_statistics64 vmStat;
        ret = host_statistics64(host, HOST_VM_INFO64, &vmStat, ref count);
        if (ret != KERN_SUCCESS)
        {
            return false;
        }

        PageSize = pageSize;
        ActiveCount = vmStat.active_count;
        InactiveCount = vmStat.inactive_count;
        WireCount = vmStat.wire_count;
        FreeCount = vmStat.free_count;
        ZeroFillCount = vmStat.zero_fill_count;
        ReactivatedCount = vmStat.reactivations;
        Faults = vmStat.faults;
        CowFaults = vmStat.cow_faults;
        Pageins = vmStat.pageins;
        Pageouts = vmStat.pageouts;
        Swapins = vmStat.swapins;
        Swapouts = vmStat.swapouts;
        Lookups = vmStat.lookups;
        Hits = vmStat.hits;
        PurgeableCount = vmStat.purgeable_count;
        Purges = vmStat.purges;
        SpeculativeCount = vmStat.speculative_count;
        ThrottledCount = vmStat.throttled_count;
        CompressorPageCount = vmStat.compressor_page_count;
        Decompressions = vmStat.decompressions;
        Compressions = vmStat.compressions;
        ExternalPageCount = vmStat.external_page_count;
        InternalPageCount = vmStat.internal_page_count;
        TotalUncompressedPagesInCompressor = vmStat.total_uncompressed_pages_in_compressor;
        SwappedCount = vmStat.swapped_count;

        UpdateAt = DateTime.Now;

        return true;
    }
}
