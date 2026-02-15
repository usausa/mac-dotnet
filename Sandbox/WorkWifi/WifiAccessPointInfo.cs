namespace WorkWifi;

using static WorkWifi.NativeMethods;

/// <summary>
/// Wi-Fi チャンネルの情報を表すレコード.
/// </summary>
/// <param name="ChannelNumber">チャンネル番号 (例: 1, 6, 11, 36, 44, ...).</param>
/// <param name="Band">周波数帯域 (2.4GHz / 5GHz / 6GHz).</param>
/// <param name="Width">チャンネル帯域幅 (20/40/80/160 MHz).</param>
internal sealed record WifiChannelInfo(
    nint ChannelNumber,
    nint Band,
    nint Width)
{
    /// <summary>
    /// 帯域名を文字列で返す.
    /// </summary>
    public string BandName => Band switch
    {
        kCWChannelBand2GHz => "2.4 GHz",
        kCWChannelBand5GHz => "5 GHz",
        kCWChannelBand6GHz => "6 GHz",
        _ => "Unknown",
    };

    /// <summary>
    /// 帯域幅を文字列で返す.
    /// </summary>
    public string WidthName => Width switch
    {
        kCWChannelWidth20MHz => "20 MHz",
        kCWChannelWidth40MHz => "40 MHz",
        kCWChannelWidth80MHz => "80 MHz",
        kCWChannelWidth160MHz => "160 MHz",
        _ => "Unknown",
    };
}

/// <summary>
/// アクセスポイントがサポートするセキュリティプロトコルの情報.
/// </summary>
/// <param name="SecurityTypes">サポートされるセキュリティタイプの一覧.</param>
internal sealed record WifiSecurityInfo(IReadOnlyList<string> SecurityTypes)
{
    /// <summary>
    /// セキュリティタイプをカンマ区切りの文字列で返す.
    /// </summary>
    public string Summary => SecurityTypes.Count == 0 ? "None" : string.Join(", ", SecurityTypes);
}

/// <summary>
/// Wi-Fi アクセスポイントの詳細情報を表すレコード.
/// ManagedNativeWifi の BssNetworkPack に相当する情報を macOS CoreWLAN から取得.
/// </summary>
/// <param name="Ssid">ネットワーク名 (Service Set Identifier). 非公開ネットワークの場合は null.</param>
/// <param name="Bssid">アクセスポイントの MAC アドレス (Basic Service Set Identifier).</param>
/// <param name="RssiValue">受信信号強度 (dBm). 通常 -30 ～ -90 の範囲.</param>
/// <param name="NoiseMeasurement">ノイズフロア (dBm). 通常 -80 ～ -100 の範囲.</param>
/// <param name="Channel">チャンネル情報 (番号、帯域、帯域幅).</param>
/// <param name="Security">サポートされるセキュリティプロトコル.</param>
/// <param name="BeaconInterval">ビーコン間隔 (ミリ秒). アクセスポイントがビーコンフレームを送信する間隔.</param>
/// <param name="CountryCode">ISO 国コード. 無線規制の準拠国を示す.</param>
/// <param name="IsIbss">アドホックネットワーク (IBSS) かどうか. true の場合はピアツーピア接続.</param>
/// <param name="InformationElementDataLength">情報要素データの長さ (バイト). ビーコン/プローブ応答フレームの生データサイズ.</param>
/// <param name="SignalToNoiseRatio">信号対雑音比 (SNR, dB). RssiValue - NoiseMeasurement で算出. 値が大きいほど通信品質が良い.</param>
/// <param name="SignalQualityPercent">信号品質 (%). RSSI を 0-100% にマッピングした値. UI 表示用.</param>
internal sealed record WifiAccessPointInfo(
    string? Ssid,
    string? Bssid,
    nint RssiValue,
    nint NoiseMeasurement,
    WifiChannelInfo? Channel,
    WifiSecurityInfo Security,
    nint BeaconInterval,
    string? CountryCode,
    bool IsIbss,
    int InformationElementDataLength,
    int SignalToNoiseRatio,
    int SignalQualityPercent);
