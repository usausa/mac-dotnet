namespace WorkWifi;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static WorkWifi.NativeMethods;

/// <summary>
/// macOS CoreWLAN フレームワークを使用して Wi-Fi アクセスポイント情報を取得する.
/// Objective-C ランタイムを介して CoreWLAN の CWWiFiClient / CWInterface / CWNetwork を操作する.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class WifiInfoProvider
{
    // セレクタキャッシュ (繰り返しの sel_registerName 呼び出しを避ける)
    private static readonly IntPtr SelSharedWiFiClient = sel_registerName("sharedWiFiClient");
    private static readonly IntPtr SelInterface = sel_registerName("interface");
    private static readonly IntPtr SelScanForNetworksWithNameError = sel_registerName("scanForNetworksWithName:error:");
    private static readonly IntPtr SelAllObjects = sel_registerName("allObjects");
    private static readonly IntPtr SelCount = sel_registerName("count");
    private static readonly IntPtr SelObjectAtIndex = sel_registerName("objectAtIndex:");
    private static readonly IntPtr SelSsid = sel_registerName("ssid");
    private static readonly IntPtr SelBssid = sel_registerName("bssid");
    private static readonly IntPtr SelRssiValue = sel_registerName("rssiValue");
    private static readonly IntPtr SelNoiseMeasurement = sel_registerName("noiseMeasurement");
    private static readonly IntPtr SelWlanChannel = sel_registerName("wlanChannel");
    private static readonly IntPtr SelChannelNumber = sel_registerName("channelNumber");
    private static readonly IntPtr SelChannelBand = sel_registerName("channelBand");
    private static readonly IntPtr SelChannelWidth = sel_registerName("channelWidth");
    private static readonly IntPtr SelBeaconInterval = sel_registerName("beaconInterval");
    private static readonly IntPtr SelCountryCode = sel_registerName("countryCode");
    private static readonly IntPtr SelIsIBSS = sel_registerName("ibss");
    private static readonly IntPtr SelInformationElementData = sel_registerName("informationElementData");
    private static readonly IntPtr SelLength = sel_registerName("length");
    private static readonly IntPtr SelSupportsSecurity = sel_registerName("supportsSecurity:");
    private static readonly IntPtr SelUtf8String = sel_registerName("UTF8String");
    private static readonly IntPtr SelLocalizedDescription = sel_registerName("localizedDescription");
    private static readonly IntPtr SelInterfaceName = sel_registerName("interfaceName");
    private static readonly IntPtr SelPowerOn = sel_registerName("powerOn");

    /// <summary>
    /// CoreWLAN フレームワークをロードする.
    /// Objective-C クラスを使用する前に呼び出す必要がある.
    /// </summary>
    public static void LoadCoreWlanFramework()
    {
        var handle = dlopen("/System/Library/Frameworks/CoreWLAN.framework/CoreWLAN", RTLD_LAZY);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("CoreWLAN フレームワークのロードに失敗しました。macOS 上で実行してください。");
        }
    }

    /// <summary>
    /// Wi-Fi インターフェース名を取得する.
    /// </summary>
    public static string? GetInterfaceName()
    {
        var wifiInterface = GetWifiInterface();
        if (wifiInterface == IntPtr.Zero)
        {
            return null;
        }

        return GetNSString(objc_msgSend(wifiInterface, SelInterfaceName));
    }

    /// <summary>
    /// Wi-Fi の電源が ON かどうかを返す.
    /// </summary>
    public static bool IsWifiPowerOn()
    {
        var wifiInterface = GetWifiInterface();
        if (wifiInterface == IntPtr.Zero)
        {
            return false;
        }

        return objc_msgSend_bool_noargs(wifiInterface, SelPowerOn);
    }

    /// <summary>
    /// Wi-Fi アクセスポイントをスキャンし、検出されたネットワーク情報の一覧を返す.
    /// </summary>
    /// <remarks>
    /// スキャンには位置情報サービスの許可が必要な場合がある (macOS 14+).
    /// 管理者権限またはシステム環境設定での位置情報許可が求められることがある.
    /// </remarks>
    /// <returns>検出されたアクセスポイント情報のリスト.</returns>
    public static IReadOnlyList<WifiAccessPointInfo> ScanAccessPoints()
    {
        var wifiInterface = GetWifiInterface();
        if (wifiInterface == IntPtr.Zero)
        {
            throw new InvalidOperationException("Wi-Fi インターフェースが見つかりません。");
        }

        // scanForNetworksWithName:error: を呼び出す
        // 第1引数: nil (全ネットワークスキャン)
        // 第2引数: NSError** (エラー出力ポインタ)
        IntPtr errorPtr = IntPtr.Zero;
        IntPtr networks;

        unsafe
        {
            networks = objc_msgSend(wifiInterface, SelScanForNetworksWithNameError, IntPtr.Zero, (IntPtr)(&errorPtr));
        }

        if (networks == IntPtr.Zero || errorPtr != IntPtr.Zero)
        {
            var errorMessage = "Wi-Fi スキャンに失敗しました。";
            if (errorPtr != IntPtr.Zero)
            {
                var desc = GetNSString(objc_msgSend(errorPtr, SelLocalizedDescription));
                if (desc is not null)
                {
                    errorMessage += $" エラー: {desc}";
                }
            }

            throw new InvalidOperationException(errorMessage);
        }

        // NSSet → NSArray に変換して列挙
        var array = objc_msgSend(networks, SelAllObjects);
        var count = objc_msgSend_nuint(array, SelCount);
        var results = new List<WifiAccessPointInfo>((int)count);

        for (nuint i = 0; i < count; i++)
        {
            var network = objc_msgSend(array, SelObjectAtIndex, (IntPtr)i);
            if (network == IntPtr.Zero)
            {
                continue;
            }

            results.Add(CreateAccessPointInfo(network));
        }

        return results;
    }

    /// <summary>
    /// CWNetwork オブジェクトから WifiAccessPointInfo を構築する.
    /// </summary>
    private static WifiAccessPointInfo CreateAccessPointInfo(IntPtr network)
    {
        var ssid = GetNSString(objc_msgSend(network, SelSsid));
        var bssid = GetNSString(objc_msgSend(network, SelBssid));
        var rssi = objc_msgSend_nint(network, SelRssiValue);
        var noise = objc_msgSend_nint(network, SelNoiseMeasurement);
        var beaconInterval = objc_msgSend_nint(network, SelBeaconInterval);
        var countryCode = GetNSString(objc_msgSend(network, SelCountryCode));
        var isIbss = objc_msgSend_bool_noargs(network, SelIsIBSS);

        // チャンネル情報の取得
        WifiChannelInfo? channelInfo = null;
        var channel = objc_msgSend(network, SelWlanChannel);
        if (channel != IntPtr.Zero)
        {
            channelInfo = new WifiChannelInfo(
                ChannelNumber: objc_msgSend_nint(channel, SelChannelNumber),
                Band: objc_msgSend_nint(channel, SelChannelBand),
                Width: objc_msgSend_nint(channel, SelChannelWidth));
        }

        // セキュリティ情報の取得
        var securityTypes = GetSupportedSecurityTypes(network);

        // 情報要素データの長さ取得
        var ieData = objc_msgSend(network, SelInformationElementData);
        var ieDataLength = ieData != IntPtr.Zero ? (int)objc_msgSend_nuint(ieData, SelLength) : 0;

        // SNR (信号対雑音比) の算出
        var snr = (int)(rssi - noise);

        // 信号品質を 0-100% に変換 (一般的な RSSI → パーセント変換)
        // -30 dBm 以上 = 100%, -90 dBm 以下 = 0%
        var quality = Math.Clamp((int)((rssi + 90) * 100 / 60), 0, 100);

        return new WifiAccessPointInfo(
            Ssid: ssid,
            Bssid: bssid,
            RssiValue: rssi,
            NoiseMeasurement: noise,
            Channel: channelInfo,
            Security: new WifiSecurityInfo(securityTypes),
            BeaconInterval: beaconInterval,
            CountryCode: countryCode,
            IsIbss: isIbss,
            InformationElementDataLength: ieDataLength,
            SignalToNoiseRatio: snr,
            SignalQualityPercent: quality);
    }

    /// <summary>
    /// CWNetwork がサポートするセキュリティタイプの一覧を取得する.
    /// CWSecurity の各値に対して supportsSecurity: を呼び出して判定する.
    /// </summary>
    private static List<string> GetSupportedSecurityTypes(IntPtr network)
    {
        // チェック対象のセキュリティタイプと表示名
        ReadOnlySpan<(nint Value, string Name)> securityChecks =
        [
            (kCWSecurityNone, "Open"),
            (kCWSecurityWEP, "WEP"),
            (kCWSecurityWPAPersonal, "WPA Personal"),
            (kCWSecurityWPAPersonalMixed, "WPA Personal Mixed"),
            (kCWSecurityWPA2Personal, "WPA2 Personal"),
            (kCWSecurityPersonalTransition, "WPA2/WPA3 Transition"),
            (kCWSecurityWPA3Personal, "WPA3 Personal"),
            (kCWSecurityDynamicWEP, "Dynamic WEP"),
            (kCWSecurityWPAEnterprise, "WPA Enterprise"),
            (kCWSecurityWPAEnterpriseMixed, "WPA Enterprise Mixed"),
            (kCWSecurityWPA2Enterprise, "WPA2 Enterprise"),
            (kCWSecurityWPA3Enterprise, "WPA3 Enterprise"),
            (kCWSecurityWPA3EnterpriseTransition, "WPA3 Enterprise Transition"),
        ];

        var result = new List<string>();
        foreach (var (value, name) in securityChecks)
        {
            if (objc_msgSend_bool(network, SelSupportsSecurity, value))
            {
                result.Add(name);
            }
        }

        return result;
    }

    /// <summary>
    /// CWWiFiClient の共有インスタンスから CWInterface を取得する.
    /// </summary>
    private static IntPtr GetWifiInterface()
    {
        var clientClass = objc_getClass("CWWiFiClient");
        if (clientClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var client = objc_msgSend(clientClass, SelSharedWiFiClient);
        if (client == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return objc_msgSend(client, SelInterface);
    }

    /// <summary>
    /// Objective-C の NSString を C# の string に変換する.
    /// </summary>
    private static string? GetNSString(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero)
        {
            return null;
        }

        var utf8Ptr = objc_msgSend(nsString, SelUtf8String);
        return utf8Ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8Ptr);
    }
}
