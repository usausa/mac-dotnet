namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// vm_statistics64 から取得したメモリ統計を管理するクラス。
/// Update() を呼ぶたびにカーネルから最新の統計を取得する。
/// <para>
/// Manages memory statistics retrieved from vm_statistics64.
/// Each call to Update() fetches the latest stats from the kernel.
/// </para>
/// </summary>
public sealed class MemoryStat
{
    private readonly uint _host;

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>物理メモリの総量 (バイト) (hw.memsize)<br/>Total physical memory in bytes (hw.memsize)</summary>
    public ulong PhysicalMemory { get; private set; }

    /// <summary>システムのページサイズ (バイト)<br/>System memory page size in bytes</summary>
    public nuint PageSize { get; private set; }

    /// <summary>アクティブページ数。最近アクセスされ RAM 上にあるページ<br/>Number of active pages. Recently accessed pages currently in RAM.</summary>
    public uint ActiveCount { get; private set; }

    /// <summary>非アクティブページ数。しばらくアクセスされていないがまだ RAM 上にあるページ<br/>Number of inactive pages. Not recently accessed but still resident in RAM.</summary>
    public uint InactiveCount { get; private set; }

    /// <summary>ワイヤードページ数。カーネルやドライバがスワップ不可で確保しているページ<br/>Number of wired pages. Pinned in RAM by the kernel or drivers; cannot be swapped out.</summary>
    public uint WireCount { get; private set; }

    /// <summary>空きページ数。即座に使用可能なページ<br/>Number of free pages immediately available for allocation</summary>
    public uint FreeCount { get; private set; }

    /// <summary>ゼロ充填ページの累積数。新規割り当て時にゼロ初期化されたページの総数<br/>Cumulative count of zero-filled pages allocated since boot</summary>
    public ulong ZeroFillCount { get; private set; }

    /// <summary>非アクティブリストから再アクティブ化されたページの累積数<br/>Cumulative count of pages reactivated from the inactive list</summary>
    public ulong ReactivatedCount { get; private set; }

    /// <summary>ページフォルト発生の累積数。アクセス時にページが RAM 上になかった回数<br/>Cumulative page fault count. Number of times a page was not resident in RAM on access.</summary>
    public ulong Faults { get; private set; }

    /// <summary>Copy-on-Write フォルトの累積数。書き込み時コピーが発生した回数<br/>Cumulative copy-on-write fault count</summary>
    public ulong CowFaults { get; private set; }

    /// <summary>ページイン累積数。ディスク (スワップ) から RAM に読み込んだページ数<br/>Cumulative number of pages paged in from disk (swap)</summary>
    public ulong Pageins { get; private set; }

    /// <summary>ページアウト累積数。RAM からディスク (スワップ) に書き出したページ数<br/>Cumulative number of pages paged out to disk (swap)</summary>
    public ulong Pageouts { get; private set; }

    /// <summary>スワップイン累積数。圧縮ストアまたはスワップから展開・読み込んだページ数<br/>Cumulative number of pages swapped in from the compressor or swap</summary>
    public ulong Swapins { get; private set; }

    /// <summary>スワップアウト累積数。圧縮ストアまたはスワップに書き出したページ数<br/>Cumulative number of pages swapped out to the compressor or swap</summary>
    public ulong Swapouts { get; private set; }

    /// <summary>ページキャッシュ検索の累積数<br/>Cumulative number of page cache lookups</summary>
    public ulong Lookups { get; private set; }

    /// <summary>ページキャッシュヒットの累積数<br/>Cumulative number of page cache hits</summary>
    public ulong Hits { get; private set; }

    /// <summary>パージ可能ページ数。アプリが明示的に解放を許可したページ<br/>Number of purgeable pages that apps have explicitly allowed to be freed</summary>
    public uint PurgeableCount { get; private set; }

    /// <summary>パージ累積数。パージ可能ページが実際に解放された回数<br/>Cumulative number of times purgeable pages were actually freed</summary>
    public ulong Purges { get; private set; }

    /// <summary>投機的ページ数。先読みにより確保されたが未アクセスのページ<br/>Number of speculative pages prefetched but not yet accessed</summary>
    public uint SpeculativeCount { get; private set; }

    /// <summary>スロットルページ数。I/O スロットリング対象のページ<br/>Number of pages subject to I/O throttling</summary>
    public uint ThrottledCount { get; private set; }

    /// <summary>コンプレッサーが保持するページ数。メモリ圧縮によって圧縮されたページ<br/>Number of pages held by the memory compressor</summary>
    public uint CompressorPageCount { get; private set; }

    /// <summary>コンプレッサーによる展開の累積数<br/>Cumulative number of decompressions performed by the compressor</summary>
    public ulong Decompressions { get; private set; }

    /// <summary>コンプレッサーによる圧縮の累積数<br/>Cumulative number of compressions performed by the compressor</summary>
    public ulong Compressions { get; private set; }

    /// <summary>外部ページ数。ファイルキャッシュなどに使用されているページ<br/>Number of external pages used for file cache and similar purposes</summary>
    public uint ExternalPageCount { get; private set; }

    /// <summary>内部ページ数。アプリが使用しているページ (Purgeable を含む)<br/>Number of internal pages used by apps (including purgeable pages)</summary>
    public uint InternalPageCount { get; private set; }

    /// <summary>コンプレッサー内の圧縮前の総ページ数。CompressionRatio 計算に使用<br/>Total uncompressed pages inside the compressor. Used for CompressionRatio calculation.</summary>
    public ulong TotalUncompressedPagesInCompressor { get; private set; }

    /// <summary>スワップアウト済みのページ数。圧縮後もスワップに書き出されたページ<br/>Number of pages written to swap even after compression</summary>
    public ulong SwappedCount { get; private set; }

    /// <summary>使用中のメモリ量 (バイト)。Active + Wired + Compressor の合計<br/>Memory in use in bytes. Sum of Active + Wired + Compressor pages.</summary>
    public ulong UsedBytes => ((ulong)ActiveCount + WireCount + CompressorPageCount) * PageSize;

    /// <summary>空きメモリ量 (バイト)。PhysicalMemory - UsedBytes<br/>Free memory in bytes. PhysicalMemory minus UsedBytes.</summary>
    public ulong FreeBytes => PhysicalMemory > UsedBytes ? PhysicalMemory - UsedBytes : 0;

    /// <summary>アクティブメモリ量 (バイト)<br/>Active memory in bytes</summary>
    public ulong ActiveBytes => (ulong)ActiveCount * PageSize;

    /// <summary>非アクティブメモリ量 (バイト)<br/>Inactive memory in bytes</summary>
    public ulong InactiveBytes => (ulong)InactiveCount * PageSize;

    /// <summary>ワイヤードメモリ量 (バイト)。カーネルが固定確保しているメモリ<br/>Wired memory in bytes. Memory pinned by the kernel.</summary>
    public ulong WiredBytes => (ulong)WireCount * PageSize;

    /// <summary>コンプレッサーが使用するメモリ量 (バイト)<br/>Memory used by the compressor in bytes</summary>
    public ulong CompressorBytes => (ulong)CompressorPageCount * PageSize;

    /// <summary>アプリが使用するメモリ量 (バイト)。Internal - Purgeable の合計<br/>App memory in bytes. Internal pages minus purgeable pages.</summary>
    public ulong AppMemoryBytes => InternalPageCount > PurgeableCount
        ? ((ulong)InternalPageCount - PurgeableCount) * PageSize
        : 0;

    /// <summary>メモリ使用率 (0.0〜1.0)<br/>Memory usage ratio (0.0 to 1.0)</summary>
    public double UsagePercent => PhysicalMemory > 0 ? (double)UsedBytes / PhysicalMemory : 0;

    /// <summary>メモリ圧縮率。圧縮前のページ数 / 圧縮後のページ数。値が大きいほど圧縮効率が高い<br/>Memory compression ratio. Uncompressed pages / compressed pages. Higher values indicate better compression.</summary>
    public double CompressionRatio => CompressorPageCount > 0
        ? (double)TotalUncompressedPagesInCompressor / CompressorPageCount
        : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal MemoryStat()
    {
        PhysicalMemory = GetSystemControlUInt64("hw.memsize");
        _host = mach_host_self();
        host_page_size(_host, out var pageSize);
        PageSize = pageSize;
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// host_statistics64() を呼び出してメモリ統計を更新する。
    /// 成功時は true、カーネル呼び出し失敗時は false を返す。
    /// <para>
    /// Refreshes memory statistics by calling host_statistics64().
    /// Returns true on success, false if the kernel call fails.
    /// </para>
    /// </summary>
    public unsafe bool Update()
    {
        var host = _host;
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
