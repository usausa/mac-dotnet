namespace CpuFrequencySample;

/// <summary>
/// CPUコアの種別。
/// Apple Silicon の Efficiency Core / Performance Core に対応。
/// </summary>
public enum CpuCoreType
{
    /// <summary>高効率コア (E-Core)</summary>
    Efficiency = 0,
    /// <summary>高性能コア (P-Core)</summary>
    Performance = 1,
}

/// <summary>
/// 個々のCPUコアの周波数情報を保持するクラス。
/// IOReport の "ECPU000", "ECPU010", "PCPU000" 等の各チャンネルに対応する。
/// </summary>
public sealed class CpuCoreFrequency
{
    /// <summary>コア番号 (コア種別ごとの0始まり連番)</summary>
    public int Number { get; }

    /// <summary>コア種別 (Efficiency / Performance)</summary>
    public CpuCoreType CoreType { get; }

    /// <summary>現在の周波数 (MHz)。Update() により更新される。</summary>
    public double Frequency { get; internal set; }

    /// <summary>IOReport チャンネル名 (例: "ECPU000", "PCPU100")。初期化時に設定される。</summary>
    internal string ChannelName = string.Empty;

    /// <summary>このコアに対応する周波数テーブル (MHz)。初期化時に設定される。</summary>
    internal int[] FreqTable = [];

    /// <summary>前回サンプルのステート別レジデンシー値。デルタ計算に使用。</summary>
    internal long[] PrevResidencies = [];

    /// <summary>今回サンプルの読み取りバッファ。毎 Update() で上書きして使い回す。</summary>
    internal long[] CurrResidencies = [];

    /// <summary>IDLE/DOWN/OFF 以外の最初のステートのインデックス。初期化時に設定される。</summary>
    internal int ResidencyOffset = -1;

    internal CpuCoreFrequency(int number, CpuCoreType coreType)
    {
        Number = number;
        CoreType = coreType;
    }

    public override string ToString()
        => $"{CoreType} Core {Number}: {Frequency:F1} MHz";
}
