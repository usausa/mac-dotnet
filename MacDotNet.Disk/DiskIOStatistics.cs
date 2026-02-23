namespace MacDotNet.Disk;

// I/O統計情報 (IOBlockStorageDriverのStatistics辞書から取得)
// ReSharper disable once InconsistentNaming
public sealed class DiskIOStatistics
{
    // 読み取りバイト数
    public ulong BytesRead { get; internal set; }

    // 書き込みバイト数
    public ulong BytesWritten { get; internal set; }

    // 読み取り操作回数
    public ulong OperationsRead { get; internal set; }

    // 書き込み操作回数
    public ulong OperationsWritten { get; internal set; }

    // 読み取り合計時間 (ナノ秒)
    public ulong TotalTimeRead { get; internal set; }

    // 書き込み合計時間 (ナノ秒)
    public ulong TotalTimeWritten { get; internal set; }

    // 読み取りリトライ回数
    public ulong RetriesRead { get; internal set; }

    // 書き込みリトライ回数
    public ulong RetriesWritten { get; internal set; }

    // 読み取りエラー回数
    public ulong ErrorsRead { get; internal set; }

    // 書き込みエラー回数
    public ulong ErrorsWritten { get; internal set; }

    // 読み取りレイテンシ (ナノ秒)
    public ulong LatencyTimeRead { get; internal set; }

    // 書き込みレイテンシ (ナノ秒)
    public ulong LatencyTimeWritten { get; internal set; }
}
