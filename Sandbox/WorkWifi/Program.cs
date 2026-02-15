namespace WorkWifi;

using System.Runtime.Versioning;

/// <summary>
/// Wi-Fi アクセスポイントスキャンのサンドボックスアプリケーション.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("=== macOS Wi-Fi アクセスポイント スキャナー ===");
        Console.WriteLine();

        try
        {
            // CoreWLAN フレームワークをロード
            WifiInfoProvider.LoadCoreWlanFramework();

            // インターフェース情報の表示
            var interfaceName = WifiInfoProvider.GetInterfaceName();
            var powerOn = WifiInfoProvider.IsWifiPowerOn();
            Console.WriteLine($"Wi-Fi インターフェース: {interfaceName ?? "(不明)"}");
            Console.WriteLine($"Wi-Fi 電源: {(powerOn ? "ON" : "OFF")}");
            Console.WriteLine();

            if (!powerOn)
            {
                Console.WriteLine("Wi-Fi がオフです。スキャンを中止します。");
                return;
            }

            // スキャン実行
            Console.WriteLine("スキャン中...");
            var accessPoints = WifiInfoProvider.ScanAccessPoints();
            Console.WriteLine($"検出されたアクセスポイント数: {accessPoints.Count}");
            Console.WriteLine();

            // 信号強度順にソートして表示
            var sorted = accessPoints.OrderByDescending(ap => ap.RssiValue);

            var index = 0;
            foreach (var ap in sorted)
            {
                index++;
                Console.WriteLine($"--- アクセスポイント #{index} ---");
                Console.WriteLine($"  SSID              : {ap.Ssid ?? "(非公開)"}");
                Console.WriteLine($"  BSSID             : {ap.Bssid ?? "(不明)"}");
                Console.WriteLine($"  RSSI              : {ap.RssiValue} dBm");
                Console.WriteLine($"  ノイズフロア      : {ap.NoiseMeasurement} dBm");
                Console.WriteLine($"  SNR               : {ap.SignalToNoiseRatio} dB");
                Console.WriteLine($"  信号品質          : {ap.SignalQualityPercent}%");

                if (ap.Channel is not null)
                {
                    Console.WriteLine($"  チャンネル        : {ap.Channel.ChannelNumber}");
                    Console.WriteLine($"  周波数帯域        : {ap.Channel.BandName}");
                    Console.WriteLine($"  チャンネル帯域幅  : {ap.Channel.WidthName}");
                }

                Console.WriteLine($"  セキュリティ      : {ap.Security.Summary}");
                Console.WriteLine($"  ビーコン間隔      : {ap.BeaconInterval} ms");
                Console.WriteLine($"  国コード          : {ap.CountryCode ?? "(なし)"}");
                Console.WriteLine($"  IBSS (アドホック)  : {(ap.IsIbss ? "はい" : "いいえ")}");
                Console.WriteLine($"  IE データサイズ    : {ap.InformationElementDataLength} bytes");
                Console.WriteLine();
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"エラー: {ex.Message}");
        }
    }
}
