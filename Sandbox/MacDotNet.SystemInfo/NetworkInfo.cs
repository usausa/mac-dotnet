namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public enum InterfaceState
{
    Down,
    NoCarrier,
    Up,
}

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

public sealed record InterfaceAddress
{
    /// <summary>IP アドレス文字列。例: "192.168.1.1"、"fe80::1%lo0"</summary>
    public required string Address { get; init; }

    /// <summary>サブネットプレフィックス長。例: IPv4 の /24 は 24、IPv6 の /64 は 64</summary>
    public required int PrefixLength { get; init; }
}

public sealed record NetworkInterfaceEntry
{
    /// <summary>インターフェース名。例: "en0"、"lo0"、"utun0"</summary>
    public required string Name { get; init; }

    /// <summary>インターフェースのリンク状態 (Up / Down / NoCarrier)</summary>
    public required InterfaceState State { get; init; }

    /// <summary>インターフェースフラグのビットフィールド (IFF_UP、IFF_LOOPBACK など)</summary>
    public required uint Flags { get; init; }

    /// <summary>ループバックインターフェースかどうか</summary>
    public bool IsLoopback => (Flags & IFF_LOOPBACK) != 0;

    /// <summary>ブロードキャストをサポートするかどうか</summary>
    public bool SupportsBroadcast => (Flags & IFF_BROADCAST) != 0;

    /// <summary>マルチキャストをサポートするかどうか</summary>
    public bool SupportsMulticast => (Flags & IFF_MULTICAST) != 0;

    /// <summary>ポイント・ツー・ポイント接続かどうか (VPN トンネルなど)</summary>
    public bool IsPointToPoint => (Flags & IFF_POINTOPOINT) != 0;

    /// <summary>MAC アドレス文字列。例: "20:a5:cb:d1:da:a0"。物理インターフェース以外では null</summary>
    public string? MacAddress { get; init; }

    /// <summary>IPv4 アドレスの一覧</summary>
    public required IReadOnlyList<InterfaceAddress> IPv4Addresses { get; init; }

    /// <summary>IPv6 アドレスの一覧</summary>
    public required IReadOnlyList<InterfaceAddress> IPv6Addresses { get; init; }

    /// <summary>インターフェースタイプの数値コード (if_data.ifi_type)</summary>
    public byte InterfaceType { get; init; }

    /// <summary>インターフェースタイプの表示名。例: "Ethernet"、"Wi-Fi"、"Loopback"</summary>
    public string InterfaceTypeName => InterfaceType switch
    {
        IFT_ETHER => "Ethernet",
        IFT_LOOP => "Loopback",
        IFT_IEEE80211 => "Wi-Fi",
        IFT_GIF => "GIF Tunnel",
        IFT_STF => "6to4 Tunnel",
        IFT_CELLULAR => "Cellular",
        IFT_BRIDGE => "Bridge",
        _ => $"Other(0x{InterfaceType:X2})",
    };

    /// <summary>最大転送単位 (バイト)</summary>
    public uint Mtu { get; init; }

    /// <summary>リンク速度 (bps)。0 の場合は取得不可</summary>
    public uint LinkSpeed { get; init; }

    /// <summary>受信バイト数の累積値</summary>
    public uint RxBytes { get; init; }

    /// <summary>受信パケット数の累積値</summary>
    public uint RxPackets { get; init; }

    /// <summary>受信エラー数の累積値</summary>
    public uint RxErrors { get; init; }

    /// <summary>受信ドロップ数の累積値</summary>
    public uint RxDrops { get; init; }

    /// <summary>受信マルチキャストパケット数の累積値</summary>
    public uint RxMulticast { get; init; }

    /// <summary>送信バイト数の累積値</summary>
    public uint TxBytes { get; init; }

    /// <summary>送信パケット数の累積値</summary>
    public uint TxPackets { get; init; }

    /// <summary>送信エラー数の累積値</summary>
    public uint TxErrors { get; init; }

    /// <summary>送信マルチキャストパケット数の累積値</summary>
    public uint TxMulticast { get; init; }

    /// <summary>コリジョン数の累積値</summary>
    public uint Collisions { get; init; }

    /// <summary>未知プロトコルによる受信パケット数の累積値</summary>
    public uint NoProto { get; init; }

    /// <summary>macOS System Settings で表示されるサービス名。例: "Ethernet"、"Wi-Fi"。SCNetworkServiceCopyAll に含まれないインターフェースは null</summary>
    public string? DisplayName { get; init; }

    /// <summary>macOS System Settings (ネットワーク設定) に表示されるサービスかどうか</summary>
    public bool IsHardwareInterface => DisplayName is not null;

    /// <summary>
    /// SCNetworkInterfaceGetInterfaceType() が返す SC レベルのインターフェース種別。
    /// カーネルの InterfaceType とは異なり、Wi-Fi を IEEE80211 として正しく識別できる。
    /// SCNetworkServiceCopyAll に含まれないインターフェースは Unknown。
    /// </summary>
    public ScNetworkInterfaceType ScNetworkInterfaceType { get; init; }

    /// <summary>
    /// macOS System Settings でサービスが有効かどうか。
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

    private static unsafe NetworkInterfaceEntry[] GetNetworkInterfacesAll()
    {
        if (getifaddrs(out var ifap) != 0)
        {
            return [];
        }

        try
        {
            var map = new Dictionary<string, InterfaceBuilder>(StringComparer.Ordinal);

            for (var ptr = ifap; ptr != IntPtr.Zero;)
            {
                var ifa = Marshal.PtrToStructure<ifaddrs>(ptr);
                var name = Marshal.PtrToStringUTF8(ifa.ifa_name);

                if (name is not null)
                {
                    if (!map.TryGetValue(name, out var builder))
                    {
                        builder = new InterfaceBuilder { Name = name, Flags = ifa.ifa_flags };
                        map[name] = builder;
                    }

                    if (ifa.ifa_addr != IntPtr.Zero)
                    {
                        var family = ((sockaddr*)ifa.ifa_addr)->sa_family;
                        switch (family)
                        {
                            case AF_LINK:
                                ProcessLinkAddress(ifa, builder);
                                break;
                            case AF_INET:
                                ProcessIPv4Address(ifa, builder);
                                break;
                            case AF_INET6:
                                ProcessIPv6Address(ifa, builder);
                                break;
                        }
                    }
                }

                ptr = ifa.ifa_next;
            }

            var entries = new NetworkInterfaceEntry[map.Count];
            var idx = 0;
            foreach (var builder in map.Values)
            {
                entries[idx++] = builder.Build();
            }
            Array.Sort(entries, static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            return entries;
        }
        finally
        {
            freeifaddrs(ifap);
        }
    }

    private static unsafe void ProcessLinkAddress(ifaddrs ifa, InterfaceBuilder builder)
    {
        if (ifa.ifa_data != IntPtr.Zero)
        {
            var data = (if_data*)ifa.ifa_data;
            builder.InterfaceType = data->ifi_type;
            builder.Mtu = data->ifi_mtu;
            builder.LinkSpeed = data->ifi_baudrate;
            builder.RxBytes = data->ifi_ibytes;
            builder.RxPackets = data->ifi_ipackets;
            builder.RxErrors = data->ifi_ierrors;
            builder.RxDrops = data->ifi_iqdrops;
            builder.RxMulticast = data->ifi_imcasts;
            builder.TxBytes = data->ifi_obytes;
            builder.TxPackets = data->ifi_opackets;
            builder.TxErrors = data->ifi_oerrors;
            builder.TxMulticast = data->ifi_omcasts;
            builder.Collisions = data->ifi_collisions;
            builder.NoProto = data->ifi_noproto;
        }

        var sdl = (sockaddr_dl*)ifa.ifa_addr;
        if (sdl->sdl_alen > 0 && sdl->sdl_alen <= 8)
        {
            var macPtr = (byte*)sdl + sockaddr_dl.DataOffset + sdl->sdl_nlen;
            Span<char> buf = stackalloc char[(sdl->sdl_alen * 3) - 1];
            var pos = 0;
            for (var i = 0; i < sdl->sdl_alen; i++)
            {
                if (i > 0)
                {
                    buf[pos++] = ':';
                }

                var b = macPtr[i];
                var hi = b >> 4;
                var lo = b & 0x0F;
                buf[pos++] = ToHexChar(hi);
                buf[pos++] = ToHexChar(lo);
            }

            builder.MacAddress = new string(buf[..pos]);
        }
    }

    private static char ToHexChar(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private static unsafe void ProcessIPv4Address(ifaddrs ifa, InterfaceBuilder builder)
    {
        var sinAddr = (byte*)ifa.ifa_addr + sockaddr_in.AddrOffset;
        var buf = stackalloc byte[(int)INET_ADDRSTRLEN];

        if (inet_ntop(AF_INET, sinAddr, buf, INET_ADDRSTRLEN) == IntPtr.Zero)
        {
            return;
        }

        var address = Marshal.PtrToStringUTF8((IntPtr)buf);
        if (address is null)
        {
            return;
        }

        var prefix = 0;
        if (ifa.ifa_netmask != IntPtr.Zero)
        {
            var maskAddr = (byte*)ifa.ifa_netmask + sockaddr_in.AddrOffset;
            prefix = CountPrefixBits(maskAddr, 4);
        }

        builder.IPv4Addresses.Add(new InterfaceAddress { Address = address, PrefixLength = prefix });
    }

    private static unsafe void ProcessIPv6Address(ifaddrs ifa, InterfaceBuilder builder)
    {
        var sin6Addr = (byte*)ifa.ifa_addr + sockaddr_in6.AddrOffset;
        var buf = stackalloc byte[(int)INET6_ADDRSTRLEN];

        if (inet_ntop(AF_INET6, sin6Addr, buf, INET6_ADDRSTRLEN) == IntPtr.Zero)
        {
            return;
        }

        var address = Marshal.PtrToStringUTF8((IntPtr)buf);
        if (address is null)
        {
            return;
        }

        var scopeId = *(uint*)((byte*)ifa.ifa_addr + sockaddr_in6.ScopeIdOffset);
        if (scopeId != 0)
        {
            address = $"{address}%{scopeId}";
        }

        var prefix = 0;
        if (ifa.ifa_netmask != IntPtr.Zero)
        {
            var maskAddr = (byte*)ifa.ifa_netmask + sockaddr_in6.AddrOffset;
            prefix = CountPrefixBits(maskAddr, 16);
        }

        builder.IPv6Addresses.Add(new InterfaceAddress { Address = address, PrefixLength = prefix });
    }

    private static unsafe int CountPrefixBits(byte* mask, int length)
    {
        var bits = 0;
        for (var i = 0; i < length; i++)
        {
            if (mask[i] == 0xFF)
            {
                bits += 8;
                continue;
            }

            var b = mask[i];
            while ((b & 0x80) != 0)
            {
                bits++;
                b <<= 1;
            }

            break;
        }

        return bits;
    }

    private sealed class InterfaceBuilder
    {
        public required string Name { get; init; }

        public uint Flags { get; init; }

        public byte InterfaceType { get; set; }

        public string? MacAddress { get; set; }

        public uint Mtu { get; set; }

        public uint LinkSpeed { get; set; }

        public uint RxBytes { get; set; }

        public uint RxPackets { get; set; }

        public uint RxErrors { get; set; }

        public uint RxDrops { get; set; }

        public uint RxMulticast { get; set; }

        public uint TxBytes { get; set; }

        public uint TxPackets { get; set; }

        public uint TxErrors { get; set; }

        public uint TxMulticast { get; set; }

        public uint Collisions { get; set; }

        public uint NoProto { get; set; }

        public List<InterfaceAddress> IPv4Addresses { get; } = [];

        public List<InterfaceAddress> IPv6Addresses { get; } = [];

        public NetworkInterfaceEntry Build() => new()
        {
            Name = Name,
            State = (Flags & IFF_UP) == 0
                ? InterfaceState.Down
                : (Flags & IFF_RUNNING) != 0
                    ? InterfaceState.Up
                    : InterfaceState.NoCarrier,
            Flags = Flags,
            MacAddress = MacAddress,
            InterfaceType = InterfaceType,
            Mtu = Mtu,
            LinkSpeed = LinkSpeed,
            IPv4Addresses = IPv4Addresses,
            IPv6Addresses = IPv6Addresses,
            RxBytes = RxBytes,
            RxPackets = RxPackets,
            RxErrors = RxErrors,
            RxDrops = RxDrops,
            RxMulticast = RxMulticast,
            TxBytes = TxBytes,
            TxPackets = TxPackets,
            TxErrors = TxErrors,
            TxMulticast = TxMulticast,
            Collisions = Collisions,
            NoProto = NoProto,
            DisplayName = null,
            ScNetworkInterfaceType = ScNetworkInterfaceType.Unknown,
            IsServiceEnabled = null,
        };
    }
}
