namespace MacDotNet.Disk;

// I/O統計情報 (IOBlockStorageDriverのStatistics辞書から取得)
// TODO
// ReSharper disable once InconsistentNaming
public sealed class DiskIOStatistics
{
    // 読み取りバイト数
    public long BytesRead { get; internal set; }

    // 書き込みバイト数
    public long BytesWritten { get; internal set; }

    // 読み取り操作回数
    public long OperationsRead { get; internal set; }

    // 書き込み操作回数
    public long OperationsWritten { get; internal set; }

    // 読み取り合計時間 (ナノ秒)
    public long TotalTimeRead { get; internal set; }

    // 書き込み合計時間 (ナノ秒)
    public long TotalTimeWritten { get; internal set; }

    // 読み取りリトライ回数
    public long RetriesRead { get; internal set; }

    // 書き込みリトライ回数
    public long RetriesWritten { get; internal set; }

    // 読み取りエラー回数
    public long ErrorsRead { get; internal set; }

    // 書き込みエラー回数
    public long ErrorsWritten { get; internal set; }

    // 読み取りレイテンシ (ナノ秒)
    public long LatencyTimeRead { get; internal set; }

    // 書き込みレイテンシ (ナノ秒)
    public long LatencyTimeWritten { get; internal set; }
}
