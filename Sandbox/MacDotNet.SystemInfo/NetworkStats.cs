namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// ネットワークインターフェース 1 本分の統計。
/// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
/// <para>
/// Statistics for a single network interface.
/// Values are cumulative since kernel boot. The caller is responsible for computing deltas.
/// </para>
/// </summary>
public sealed class NetworkInterfaceStat
{
    internal bool Live { get; set; }

    /// <summary>インターフェース名。例: "en0"<br/>Interface name. Example: "en0"</summary>
    public string Name { get; }

    // ---- 静的情報 (インスタンス作成時に一度だけ設定) ----

    /// <summary>
    /// macOS System Settings のネットワーク設定に表示されるサービス名。
    /// 例: "Ethernet"、"Wi-Fi"。System Settings に登録されていないインターフェースは null。
    /// <para>Service name shown in macOS System Settings (e.g. "Ethernet", "Wi-Fi"). Null if not registered.</para>
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// SCNetworkInterfaceGetInterfaceType() が返す SC レベルのインターフェース種別。
    /// System Settings に登録されていないインターフェースは Unknown。
    /// <para>SC-level interface type. Unknown for interfaces not registered in System Settings.</para>
    /// </summary>
    public ScNetworkInterfaceType ScNetworkInterfaceType { get; }

    // ---- 動的情報 (Update() で更新) ----

    /// <summary>受信バイト数の累積値<br/>Cumulative bytes received</summary>
    public uint RxBytes { get; internal set; }
    /// <summary>受信パケット数の累積値<br/>Cumulative packets received</summary>
    public uint RxPackets { get; internal set; }
    /// <summary>受信エラー数の累積値<br/>Cumulative receive errors</summary>
    public uint RxErrors { get; internal set; }
    /// <summary>受信ドロップ数の累積値<br/>Cumulative receive drops</summary>
    public uint RxDrops { get; internal set; }
    /// <summary>受信マルチキャストパケット数の累積値<br/>Cumulative multicast packets received</summary>
    public uint RxMulticast { get; internal set; }
    /// <summary>送信バイト数の累積値<br/>Cumulative bytes transmitted</summary>
    public uint TxBytes { get; internal set; }
    /// <summary>送信パケット数の累積値<br/>Cumulative packets transmitted</summary>
    public uint TxPackets { get; internal set; }
    /// <summary>送信エラー数の累積値<br/>Cumulative transmit errors</summary>
    public uint TxErrors { get; internal set; }
    /// <summary>送信マルチキャストパケット数の累積値<br/>Cumulative multicast packets transmitted</summary>
    public uint TxMulticast { get; internal set; }
    /// <summary>コリジョン数の累積値<br/>Cumulative collision count</summary>
    public uint Collisions { get; internal set; }
    /// <summary>未知プロトコルによる受信パケット数の累積値<br/>Cumulative packets received for unknown protocols</summary>
    public uint NoProto { get; internal set; }

    internal NetworkInterfaceStat(string name, string? displayName, ScNetworkInterfaceType scType)
    {
        Name = name;
        DisplayName = displayName;
        ScNetworkInterfaceType = scType;
    }
}

/// <summary>
/// 全ネットワークインターフェースのトラフィック統計。
/// <see cref="Create()"/> でインスタンスを生成し、<see cref="Update()"/> を呼ぶたびに
/// 最新の累積値を更新する。<see cref="CpuStat"/> と同じパターン。
/// <para>
/// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
/// </para>
/// <para>
/// <see cref="System.Net.NetworkInformation.NetworkInterface.GetIPv4Statistics()"/> に対する追加価値:
/// Collisions / NoProto 等 .NET 標準では取得できない macOS 固有カウンタを提供する。
/// </para>
/// <para>
/// Traffic statistics for all network interfaces.
/// Create an instance via <see cref="Create()"/> and call <see cref="Update()"/> to refresh.
/// Values are cumulative since kernel boot; the caller is responsible for computing deltas.
/// Provides macOS-specific counters (Collisions, NoProto, etc.) not available via GetIPv4Statistics().
/// </para>
/// </summary>
public sealed class NetworkStats
{
    private readonly bool _excludeHiddenConfiguration;
    private readonly List<NetworkInterfaceStat> _interfaces = new();

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>
    /// 全インターフェースの統計エントリ一覧 (名前昇順)。
    /// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
    /// <para>List of statistics entries for all interfaces (sorted by name). Values are cumulative since boot.</para>
    /// </summary>
    public IReadOnlyList<NetworkInterfaceStat> Interfaces => _interfaces;

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private NetworkStats(bool excludeHiddenConfiguration)
    {
        _excludeHiddenConfiguration = excludeHiddenConfiguration;
        Update();
    }

    /// <summary>
    /// NetworkStats インスタンスを生成する。
    /// </summary>
    /// <param name="excludeHiddenConfiguration">
    /// true の場合、macOS System Settings のネットワーク画面に表示されない
    /// HiddenConfiguration なインターフェース (Thunderbolt ポート用 Ethernet Adapter 等) を除外する。
    /// 対象インターフェースの決定は新規インターフェース出現時に行われ、Update() でも同じ判定基準が使われる。
    /// </param>
    public static NetworkStats Create(bool excludeHiddenConfiguration = true)
        => new(excludeHiddenConfiguration);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// getifaddrs(3) から全インターフェースの if_data カウンタを取得し、Interfaces を更新する。
    /// DisplayName 等の静的情報は新規エントリ作成時にのみ SC を参照して取得し、以降は再取得しない。
    /// SC への問い合わせは新規インターフェース出現時のみ行い、1 回の Update() 呼び出しにつき最大 1 回のみ SC を開く。
    /// </summary>
    public unsafe bool Update()
    {
        if (getifaddrs(out var ifap) != 0)
        {
            return false;
        }

        foreach (var iface in _interfaces)
        {
            iface.Live = false;
        }

        ScServiceSession? session = null;
        try
        {
            var added = false;

            for (var ptr = ifap; ptr != IntPtr.Zero;)
            {
                var ifa = Marshal.PtrToStructure<ifaddrs>(ptr);
                var name = Marshal.PtrToStringUTF8(ifa.ifa_name);

                if (name is not null
                    && ifa.ifa_addr != IntPtr.Zero
                    && ((sockaddr*)ifa.ifa_addr)->sa_family == AF_LINK
                    && ifa.ifa_data != IntPtr.Zero)
                {
                    var raw = *(if_data*)ifa.ifa_data;

                    var iface = default(NetworkInterfaceStat);
                    foreach (var item in _interfaces)
                    {
                        if (item.Name == name)
                        {
                            iface = item;
                            break;
                        }
                    }

                    if (iface is null)
                    {
                        session ??= new ScServiceSession();
                        iface = CreateInterfaceStat(name, session);
                        if (iface is null)
                        {
                            ptr = ifa.ifa_next;
                            continue;
                        }

                        _interfaces.Add(iface);
                        added = true;
                    }

                    iface.Live = true;
                    iface.RxBytes = raw.ifi_ibytes;
                    iface.RxPackets = raw.ifi_ipackets;
                    iface.RxErrors = raw.ifi_ierrors;
                    iface.RxDrops = raw.ifi_iqdrops;
                    iface.RxMulticast = raw.ifi_imcasts;
                    iface.TxBytes = raw.ifi_obytes;
                    iface.TxPackets = raw.ifi_opackets;
                    iface.TxErrors = raw.ifi_oerrors;
                    iface.TxMulticast = raw.ifi_omcasts;
                    iface.Collisions = raw.ifi_collisions;
                    iface.NoProto = raw.ifi_noproto;
                }

                ptr = ifa.ifa_next;
            }

            for (var i = _interfaces.Count - 1; i >= 0; i--)
            {
                if (!_interfaces[i].Live)
                {
                    _interfaces.RemoveAt(i);
                }
            }

            if (added)
            {
                _interfaces.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            }

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            freeifaddrs(ifap);
        }
    }

    //--------------------------------------------------------------------------------
    // Private helpers
    //--------------------------------------------------------------------------------

    /// <summary>
    /// SC サービス情報の検索結果。<see cref="ScServiceSession.LookupService"/> が返す。
    /// </summary>
    private readonly struct ServiceInfo
    {
        /// <summary>SC 接続に失敗した。フィルタリング不可のため全インターフェースを対象とする</summary>
        public bool ScUnavailable { get; init; }
        /// <summary>SC サービスとして登録されている</summary>
        public bool Registered { get; init; }
        /// <summary>HiddenConfiguration = true のサービス (Registered=true の場合のみ有効)</summary>
        public bool IsHidden { get; init; }
        /// <summary>SC サービス名 (Registered=true かつ IsHidden=false の場合のみ有効)</summary>
        public string? DisplayName { get; init; }
        /// <summary>SC インターフェース種別 (Registered=true かつ IsHidden=false の場合のみ有効)</summary>
        public ScNetworkInterfaceType ScType { get; init; }
    }

    /// <summary>
    /// <see cref="ScServiceSession.LookupService"/> の結果をもとに新規インターフェースの
    /// <see cref="NetworkInterfaceStat"/> を生成して返す。
    /// null を返した場合はそのインターフェースを Interfaces に追加しない。
    /// </summary>
    private NetworkInterfaceStat? CreateInterfaceStat(string bsdName, ScServiceSession session)
    {
        var info = session.LookupService(bsdName);

        if (info.ScUnavailable)
        {
            // SC 接続失敗: フィルタリングできないため SC 情報なしで全インターフェースを対象とする
            return new NetworkInterfaceStat(bsdName, null, ScNetworkInterfaceType.Unknown);
        }

        if (!info.Registered || info.IsHidden)
        {
            // 未登録または HiddenConfiguration: excludeHiddenConfiguration=true の場合は除外 (null)
            return _excludeHiddenConfiguration
                ? null
                : new NetworkInterfaceStat(bsdName, null, ScNetworkInterfaceType.Unknown);
        }

        return new NetworkInterfaceStat(bsdName, info.DisplayName, info.ScType);
    }

    //--------------------------------------------------------------------------------
    // ScServiceSession
    //--------------------------------------------------------------------------------

    /// <summary>
    /// 1 回の Update() 呼び出し中に使用する SC サービスマップのキャッシュ。
    /// コンストラクタで SC を開き BSD 名 → <see cref="ServiceInfo"/> の辞書を構築する。
    /// prefs はマップ構築後すぐ解放するためメンバ変数に保持しない。
    /// <see cref="LookupService"/> は辞書引きのみで完結する。
    /// </summary>
    private sealed class ScServiceSession
    {
        private readonly Dictionary<string, ServiceInfo>? _serviceMap;

        public ScServiceSession()
        {
            var appNameRef = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, "MacDotNet.SystemInfo", NativeMethods.kCFStringEncodingUTF8);
            IntPtr prefs;
            try
            {
                prefs = NativeMethods.SCPreferencesCreate(IntPtr.Zero, appNameRef, IntPtr.Zero);
            }
            finally
            {
                NativeMethods.CFRelease(appNameRef);
            }

            if (prefs == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var services = NativeMethods.SCNetworkServiceCopyAll(prefs);
                if (services == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    _serviceMap = BuildServiceMap(prefs, services);
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
        }

        /// <summary>
        /// BSD 名に対応する <see cref="ServiceInfo"/> を辞書から返す。
        /// </summary>
        public ServiceInfo LookupService(string bsdName)
        {
            if (_serviceMap is null)
            {
                return new ServiceInfo { ScUnavailable = true };
            }

            return _serviceMap.TryGetValue(bsdName, out var info)
                ? info
                : new ServiceInfo { Registered = false };
        }

        /// <summary>
        /// SC サービス一覧を走査して BSD 名 → <see cref="ServiceInfo"/> の辞書を構築する。
        /// </summary>
        private static Dictionary<string, ServiceInfo> BuildServiceMap(IntPtr prefs, IntPtr services)
        {
            var map = new Dictionary<string, ServiceInfo>(StringComparer.Ordinal);

            var count = NativeMethods.CFArrayGetCount(services);
            for (var i = 0L; i < count; i++)
            {
                var service = NativeMethods.CFArrayGetValueAtIndex(services, i);
                if (service == IntPtr.Zero)
                {
                    continue;
                }

                var iface = NativeMethods.SCNetworkServiceGetInterface(service);
                if (iface == IntPtr.Zero)
                {
                    continue;
                }

                var bsdName = NativeMethods.CfStringToManaged(NativeMethods.SCNetworkInterfaceGetBSDName(iface));
                if (bsdName is null)
                {
                    continue;
                }

                if (IsHiddenConfiguration(prefs, service))
                {
                    map.TryAdd(bsdName, new ServiceInfo { Registered = true, IsHidden = true });
                    continue;
                }

                var displayName = NativeMethods.CfStringToManaged(NativeMethods.SCNetworkServiceGetName(service));
                var scType = ParseScInterfaceType(NativeMethods.CfStringToManaged(NativeMethods.SCNetworkInterfaceGetInterfaceType(iface)));
                map.TryAdd(bsdName, new ServiceInfo { Registered = true, DisplayName = displayName, ScType = scType });
            }

            return map;
        }

        /// <summary>
        /// SC preferences で Interface.HiddenConfiguration = True が設定されたサービスかどうかを返す。
        /// このフラグが True のサービスは macOS System Settings のネットワーク画面に表示されない
        /// 自動管理の隠しサービス (Thunderbolt ポート用 Ethernet Adapter 等)。
        /// </summary>
        private static bool IsHiddenConfiguration(IntPtr prefs, IntPtr service)
        {
            var serviceId = NativeMethods.CfStringToManaged(NativeMethods.SCNetworkServiceGetServiceID(service));
            if (serviceId is null)
            {
                return false;
            }

            var pathRef = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, $"/NetworkServices/{serviceId}/Interface", NativeMethods.kCFStringEncodingUTF8);
            IntPtr ifaceDict;
            try
            {
                ifaceDict = NativeMethods.SCPreferencesPathGetValue(prefs, pathRef);
            }
            finally
            {
                NativeMethods.CFRelease(pathRef);
            }

            if (ifaceDict == IntPtr.Zero)
            {
                return false;
            }

            var hiddenKeyRef = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, "HiddenConfiguration", NativeMethods.kCFStringEncodingUTF8);
            IntPtr hiddenRef;
            try
            {
                hiddenRef = NativeMethods.CFDictionaryGetValue(ifaceDict, hiddenKeyRef);
            }
            finally
            {
                NativeMethods.CFRelease(hiddenKeyRef);
            }

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
    }
}
