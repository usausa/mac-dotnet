namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;


/// <summary>
/// SCNetworkInterfaceGetInterfaceType() が返す SC レベルのインターフェース種別。
/// カーネルの InterfaceType (if_data.ifi_type) とは異なり、Wi-Fi を正しく識別できる。
/// </summary>
public enum ScNetworkInterfaceType
{
    /// <summary>不明または SCNetworkService に含まれないインターフェース</summary>
    Unknown,
    /// <summary>"Ethernet" — 有線 Ethernet (USB Ethernet アダプタを含む)</summary>
    Ethernet,
    /// <summary>"IEEE80211" — Wi-Fi (AirPort)</summary>
    WiFi,
    /// <summary>"Bridge" — ブリッジインターフェース (Thunderbolt Bridge など)</summary>
    Bridge,
    /// <summary>"Bond" — ボンディングインターフェース</summary>
    Bond,
    /// <summary>"VLAN" — 仮想 LAN</summary>
    Vlan,
    /// <summary>"PPP" — PPP 接続</summary>
    Ppp,
    /// <summary>"VPN" — VPN インターフェース</summary>
    Vpn,
}


/// <summary>
/// macOS ネットワークインターフェースの設定情報エントリ。
/// <para>
/// <see cref="System.Net.NetworkInformation.NetworkInterface"/> と相互補完的に使用する設計。
/// Name (BSD 名) を突合キーとして、標準 API では取得できない macOS 固有情報を提供する:
/// SC サービス名・種別・有効状態。
/// </para>
/// <para>
/// 以下は <see cref="System.Net.NetworkInformation.NetworkInterface"/> で取得可能なため本クラスでは提供しない:
/// OperationalStatus (State)、PhysicalAddress (MAC)、UnicastAddresses (IP)、
/// NetworkInterfaceType、Speed、Mtu、SupportsMulticast。
/// </para>
/// <para>
/// トラフィック統計 (Rx/Tx バイト数・パケット数・デルタ) は <see cref="NetworkStats"/> を使用する。
/// </para>
/// </summary>
public sealed record NetworkInterfaceEntry
{
    /// <summary>
    /// BSD インターフェース名。例: "en0"、"lo0"。
    /// <see cref="System.Net.NetworkInformation.NetworkInterface.Name"/> と同一の値で、両者の突合キーになる。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// macOS カーネルのインターフェースフラグ (IFF_*)。
    /// <see cref="System.Net.NetworkInformation"/> では公開されない macOS 固有の raw フラグ。
    /// </summary>
    public required uint Flags { get; init; }

    /// <summary>ブロードキャストをサポートするかどうか (IFF_BROADCAST)</summary>
    public bool SupportsBroadcast => (Flags & IFF_BROADCAST) != 0;

    /// <summary>ポイント・ツー・ポイント接続かどうか (IFF_POINTOPOINT)。VPN トンネルなど</summary>
    public bool IsPointToPoint => (Flags & IFF_POINTOPOINT) != 0;

    /// <summary>
    /// macOS System Settings のネットワーク設定に表示されるサービス名。
    /// 例: "Ethernet"、"Wi-Fi"。SCNetworkServiceCopyAll に含まれないインターフェースは null。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>macOS System Settings のネットワーク設定に登録されたサービスかどうか</summary>
    public bool IsHardwareInterface => DisplayName is not null;

    /// <summary>
    /// SCNetworkInterfaceGetInterfaceType() が返す SC レベルのインターフェース種別。
    /// <see cref="System.Net.NetworkInformation.NetworkInterface.NetworkInterfaceType"/> より正確で、
    /// Wi-Fi を <see cref="ScNetworkInterfaceType.WiFi"/> として識別できる。
    /// SCNetworkServiceCopyAll に含まれないインターフェースは Unknown。
    /// </summary>
    public ScNetworkInterfaceType ScNetworkInterfaceType { get; init; }

    /// <summary>
    /// macOS System Settings でサービスが有効かどうか (SCNetworkServiceGetEnabled)。
    /// SCNetworkServiceCopyAll に含まれないインターフェース (includeAll = true 時) は null。
    /// </summary>
    public bool? IsServiceEnabled { get; init; }
}

public static class NetworkInfo
{
    /// <summary>
    /// ネットワークインターフェースの一覧を返す。
    /// デフォルト (includeAll = false) では macOS System Settings のネットワーク設定に表示される
    /// サービスのみを返す。DisplayName にサービス名 ("Ethernet"、"Wi-Fi" 等) が設定される。
    /// includeAll = true にすると getifaddrs が返すすべてのインターフェースを返す。
    /// </summary>
    public static NetworkInterfaceEntry[] GetNetworkInterfaces(bool includeAll = false)
    {
        var all = GetNetworkInterfacesAll();

        if (includeAll)
        {
            return all;
        }

        // SCNetworkServiceCopyAll で System Settings に表示されるサービスの BSD名→(サービス名, SC種別, 有効)マップを取得
        // enabled=false のサービスは System Settings で無効化されているため除外する
        var serviceMap = GetNetworkServiceMap();
        var result = new List<NetworkInterfaceEntry>(serviceMap.Count);
        foreach (var entry in all)
        {
            if (serviceMap.TryGetValue(entry.Name, out var info) && info.isEnabled)
            {
                result.Add(entry with
                {
                    DisplayName = info.serviceName,
                    ScNetworkInterfaceType = info.scType,
                    IsServiceEnabled = true,
                });
            }
        }

        return [.. result];
    }

    /// <summary>
    /// SCNetworkServiceCopyAll() から BSD名 → (サービス名, SC種別, 有効) のマップを構築する。
    /// これが macOS System Settings のネットワーク設定に表示されるサービス一覧に相当する。
    /// </summary>
    private static Dictionary<string, (string serviceName, ScNetworkInterfaceType scType, bool isEnabled)> GetNetworkServiceMap()
    {
        var result = new Dictionary<string, (string, ScNetworkInterfaceType, bool)>(StringComparer.Ordinal);

        var appName = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, "MacDotNet.SystemInfo", NativeMethods.kCFStringEncodingUTF8);
        var prefs = NativeMethods.SCPreferencesCreate(IntPtr.Zero, appName, IntPtr.Zero);
        NativeMethods.CFRelease(appName);

        if (prefs == IntPtr.Zero)
        {
            return result;
        }

        try
        {
            var services = NativeMethods.SCNetworkServiceCopyAll(prefs);
            if (services == IntPtr.Zero)
            {
                return result;
            }

            try
            {
                var count = NativeMethods.CFArrayGetCount(services);
                for (var i = 0L; i < count; i++)
                {
                    var service = NativeMethods.CFArrayGetValueAtIndex(services, i);
                    if (service == IntPtr.Zero)
                    {
                        continue;
                    }

                    // GetInterface / GetName の戻り値は所有権が移らないため CFRelease 不要
                    var iface = NativeMethods.SCNetworkServiceGetInterface(service);
                    if (iface == IntPtr.Zero)
                    {
                        continue;
                    }

                    var bsdNameRef = NativeMethods.SCNetworkInterfaceGetBSDName(iface);
                    var serviceNameRef = NativeMethods.SCNetworkServiceGetName(service);
                    var scTypeRef = NativeMethods.SCNetworkInterfaceGetInterfaceType(iface);
                    var isEnabled = NativeMethods.SCNetworkServiceGetEnabled(service);

                    // Interface.HiddenConfiguration = True のサービスは System Settings に表示されない隠しサービス
                    // (Thunderbolt ポート経由の自動追加 Ethernet Adapter など)
                    if (IsHiddenConfiguration(prefs, service))
                    {
                        continue;
                    }

                    var bsdName = NativeMethods.CfStringToManaged(bsdNameRef);
                    var serviceName = NativeMethods.CfStringToManaged(serviceNameRef);
                    var scType = ParseScInterfaceType(NativeMethods.CfStringToManaged(scTypeRef));

                    if (bsdName is not null && serviceName is not null)
                    {
                        result.TryAdd(bsdName, (serviceName, scType, isEnabled));
                    }
                }
            }
            finally
            {
                NativeMethods.CFRelease(services);
            }
        }
        finally
        {
            NativeMethods.CFRelease(prefs);
        }

        return result;
    }

    /// <summary>
    /// SC preferences で Interface.HiddenConfiguration = True が設定されたサービスかどうかを返す。
    /// このフラグが True のサービスは macOS System Settings のネットワーク画面に表示されない
    /// 自動管理の隠しサービス (Thunderbolt ポート用 Ethernet Adapter 等)。
    /// </summary>
    private static bool IsHiddenConfiguration(IntPtr prefs, IntPtr service)
    {
        var serviceIdRef = NativeMethods.SCNetworkServiceGetServiceID(service);
        var serviceId = NativeMethods.CfStringToManaged(serviceIdRef);
        if (serviceId is null)
        {
            return false;
        }

        var pathStr = $"/NetworkServices/{serviceId}/Interface";
        var pathRef = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, pathStr, NativeMethods.kCFStringEncodingUTF8);
        var ifaceDict = NativeMethods.SCPreferencesPathGetValue(prefs, pathRef);
        NativeMethods.CFRelease(pathRef);

        if (ifaceDict == IntPtr.Zero)
        {
            return false;
        }

        var hiddenKeyRef = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, "HiddenConfiguration", NativeMethods.kCFStringEncodingUTF8);
        var hiddenRef = NativeMethods.CFDictionaryGetValue(ifaceDict, hiddenKeyRef);
        NativeMethods.CFRelease(hiddenKeyRef);

        return hiddenRef != IntPtr.Zero && NativeMethods.CFBooleanGetValue(hiddenRef);
    }

    private static ScNetworkInterfaceType ParseScInterfaceType(string? scType) => scType switch
    {
        "Ethernet" => ScNetworkInterfaceType.Ethernet,
        "IEEE80211" => ScNetworkInterfaceType.WiFi,
        "Bridge" => ScNetworkInterfaceType.Bridge,
        "Bond" => ScNetworkInterfaceType.Bond,
        "VLAN" => ScNetworkInterfaceType.Vlan,
        "PPP" => ScNetworkInterfaceType.Ppp,
        "VPN" => ScNetworkInterfaceType.Vpn,
        _ => ScNetworkInterfaceType.Unknown,
    };

    /// <summary>
    /// getifaddrs(3) から全インターフェースの名前と Flags のみを収集して返す。
    /// IP アドレス・MAC・統計等は System.Net.NetworkInformation.NetworkInterface / NetworkStats を使用する。
    /// </summary>
    private static unsafe NetworkInterfaceEntry[] GetNetworkInterfacesAll()
    {
        if (getifaddrs(out var ifap) != 0)
        {
            return [];
        }

        try
        {
            // インターフェース名 → Flags のマップ (同名エントリが複数出るので最初のもので確定)
            var map = new Dictionary<string, uint>(StringComparer.Ordinal);

            for (var ptr = ifap; ptr != IntPtr.Zero;)
            {
                var ifa = Marshal.PtrToStructure<ifaddrs>(ptr);
                var name = Marshal.PtrToStringUTF8(ifa.ifa_name);

                if (name is not null)
                {
                    map.TryAdd(name, ifa.ifa_flags);
                }

                ptr = ifa.ifa_next;
            }

            var entries = new NetworkInterfaceEntry[map.Count];
            var idx = 0;
            foreach (var (name, flags) in map)
            {
                entries[idx++] = new NetworkInterfaceEntry
                {
                    Name = name,
                    Flags = flags,
                    DisplayName = null,
                    ScNetworkInterfaceType = ScNetworkInterfaceType.Unknown,
                    IsServiceEnabled = null,
                };
            }

            Array.Sort(entries, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            return entries;
        }
        finally
        {
            freeifaddrs(ifap);
        }
    }
}
