namespace MacDotNet.Disk;

/// <summary>
/// SMART データに基づく総合的なディスク健全性の判定結果。
/// <para>Overall disk health assessment based on SMART data.</para>
/// </summary>
public enum SmartHealthStatus
{
    /// <summary>
    /// SMART 非対応またはデータ未取得。
    /// <para>SMART is not supported or data has not been retrieved.</para>
    /// </summary>
    Unknown,

    /// <summary>
    /// 問題なし。
    /// <para>No issues detected.</para>
    /// </summary>
    Healthy,

    /// <summary>
    /// 要注意。軽微な問題が検出された。早めのバックアップを推奨する。
    /// <para>Caution recommended. Minor issues detected. Early backup is advised.</para>
    /// </summary>
    Warning,

    /// <summary>
    /// 重大な問題あり。データ損失のリスクがある。即時バックアップを推奨する。
    /// <para>Critical issues detected. Risk of data loss exists. Immediate backup is strongly recommended.</para>
    /// </summary>
    Critical,
}
