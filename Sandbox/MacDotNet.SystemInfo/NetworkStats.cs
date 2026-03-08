namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.IokitHelper;
using static MacDotNet.SystemInfo.NativeMethods;

public enum NetworkInterfaceType
{
    Unknown,
    Ethernet,
    WiFi,
    Bridge,
    Bond,
    Vlan,
    Ppp,
    Vpn
}

public sealed class NetworkStatEntry
{
    internal bool Live { get; set; }

    // Interface name. Example: "en0"
    public string Name { get; }

    // Service name shown in macOS System Settings
    public string? DisplayName { get; }

    public NetworkInterfaceType InterfaceType { get; }

    // SC service metadata (for caller-side filtering)

    /// <summary>macOS System Settings に SC サービスとして登録されているか。<br/>Whether this interface has a registered SC network service.</summary>
    public bool IsRegistered { get; }

    /// <summary>SC サービスが有効か。Update() のたびに更新される。<br/>Whether the SC service is enabled. Updated on each Update() call.</summary>
    public bool IsEnabled { get; internal set; }

    /// <summary>HiddenConfiguration フラグが立っているか (System Settings 非表示)。<br/>Whether the interface has the HiddenConfiguration flag set.</summary>
    public bool IsHidden { get; }

    // Cumulative bytes

    public uint RxBytes { get; internal set; }
    public uint RxPackets { get; internal set; }
    public uint RxErrors { get; internal set; }
    public uint RxDrops { get; internal set; }
    public uint RxMulticast { get; internal set; }

    public uint TxBytes { get; internal set; }
    public uint TxPackets { get; internal set; }
    public uint TxErrors { get; internal set; }
    public uint TxMulticast { get; internal set; }

    public uint Collisions { get; internal set; }
    public uint NoProto { get; internal set; }

    internal NetworkStatEntry(string name, string? displayName, NetworkInterfaceType interfaceType, bool isRegistered, bool isHidden)
    {
        Name = name;
        DisplayName = displayName;
        InterfaceType = interfaceType;
        IsRegistered = isRegistered;
        IsHidden = isHidden;
    }
}

public sealed class NetworkStat
{
    private readonly List<NetworkStatEntry> interfaces = new();

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<NetworkStatEntry> Interfaces => interfaces;


    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    internal NetworkStat()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        if (getifaddrs(out var ifap) != 0)
        {
            return false;
        }

        foreach (var iface in interfaces)
        {
            iface.Live = false;
        }

        try
        {
            var added = false;

            for (var ifa = (ifaddrs*)ifap; ifa != null; ifa = (ifaddrs*)ifa->ifa_next)
            {
                var name = Marshal.PtrToStringUTF8(ifa->ifa_name);

                if ((name is not null) &&
                    (ifa->ifa_addr != IntPtr.Zero) &&
                    (((sockaddr*)ifa->ifa_addr)->sa_family == AF_LINK) &&
                    (ifa->ifa_data != IntPtr.Zero))
                {
                    var raw = *(if_data*)ifa->ifa_data;

                    var iface = default(NetworkStatEntry);
                    foreach (var item in interfaces)
                    {
                        if (item.Name == name)
                        {
                            iface = item;
                            break;
                        }
                    }

                    if (iface is null)
                    {
                        // 新規デバイス: SC から静的情報を一度だけ取得してエントリを作成する
                        iface = CreateEntry(name);
                        interfaces.Add(iface);
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
            }

            for (var i = interfaces.Count - 1; i >= 0; i--)
            {
                if (!interfaces[i].Live)
                {
                    interfaces.RemoveAt(i);
                }
            }

            if (added)
            {
                interfaces.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            }

            // 登録済みかつ非 Hidden のエントリについて IsEnabled を更新する
            RefreshEnabledState();

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            freeifaddrs(ifap);
        }
    }

    //--------------------------------------------------------------------------------
    // Static info (read once at entry creation)
    //--------------------------------------------------------------------------------

    /// <summary>
    /// 新規インターフェース検出時に SC から静的情報を取得してエントリを生成する。
    /// SCPreferences を開いてサービス一覧を検索し、BSD 名が一致するサービスの
    /// IsRegistered / IsHidden / DisplayName / InterfaceType を読み取る。
    /// IsEnabled は同一 Update() 内の RefreshEnabledState() で設定されるため、ここでは取得しない。
    /// </summary>
    private static NetworkStatEntry CreateEntry(string bsdName)
    {
        using var appNameRef = CFRef.CreateString("MacDotNet.SystemInfo");
        using var prefs = new CFRef(SCPreferencesCreate(IntPtr.Zero, appNameRef, IntPtr.Zero));
        if (!prefs.IsValid)
        {
            return new NetworkStatEntry(bsdName, null, NetworkInterfaceType.Unknown, isRegistered: false, isHidden: false);
        }

        using var services = new CFRef(SCNetworkServiceCopyAll(prefs));
        if (!services.IsValid)
        {
            return new NetworkStatEntry(bsdName, null, NetworkInterfaceType.Unknown, isRegistered: false, isHidden: false);
        }

        var count = CFArrayGetCount(services);
        for (var i = 0L; i < count; i++)
        {
            var service = CFArrayGetValueAtIndex(services, i);
            if (service == IntPtr.Zero)
            {
                continue;
            }

            var iface = SCNetworkServiceGetInterface(service);
            if (iface == IntPtr.Zero)
            {
                continue;
            }

            var name = CfStringToManaged(SCNetworkInterfaceGetBSDName(iface));
            if (name != bsdName)
            {
                continue;
            }

            var isHidden = IsHiddenConfiguration(prefs, service);
            var displayName = CfStringToManaged(SCNetworkServiceGetName(service));
            var interfaceType = ParseInterfaceType(CfStringToManaged(SCNetworkInterfaceGetInterfaceType(iface)));
            return new NetworkStatEntry(bsdName, displayName, interfaceType, isRegistered: true, isHidden: isHidden);
        }

        // SC サービスが見つからなかった場合
        return new NetworkStatEntry(bsdName, null, NetworkInterfaceType.Unknown, isRegistered: false, isHidden: false);
    }

    //--------------------------------------------------------------------------------
    // Dynamic info (refreshed on every Update)
    //--------------------------------------------------------------------------------

    /// <summary>
    /// 登録済みかつ非 Hidden のエントリについて、SC から IsEnabled を読み取って更新する。
    /// SC サービス一覧を一度だけ取得し、BSD 名で突合して更新する。
    /// </summary>
    private void RefreshEnabledState()
    {
        // 更新対象エントリがなければ SC を開かずに終了
        var hasTarget = false;
        foreach (var iface in interfaces)
        {
            if (iface.IsRegistered && !iface.IsHidden)
            {
                hasTarget = true;
                break;
            }
        }

        if (!hasTarget)
        {
            return;
        }

        using var appNameRef = CFRef.CreateString("MacDotNet.SystemInfo");
        using var prefs = new CFRef(SCPreferencesCreate(IntPtr.Zero, appNameRef, IntPtr.Zero));
        if (!prefs.IsValid)
        {
            return;
        }

        using var services = new CFRef(SCNetworkServiceCopyAll(prefs));
        if (!services.IsValid)
        {
            return;
        }

        var count = CFArrayGetCount(services);
        for (var i = 0L; i < count; i++)
        {
            var service = CFArrayGetValueAtIndex(services, i);
            if (service == IntPtr.Zero)
            {
                continue;
            }

            var iface = SCNetworkServiceGetInterface(service);
            if (iface == IntPtr.Zero)
            {
                continue;
            }

            var bsdName = CfStringToManaged(SCNetworkInterfaceGetBSDName(iface));
            if (bsdName is null)
            {
                continue;
            }

            foreach (var entry in interfaces)
            {
                if (entry.IsRegistered && !entry.IsHidden && entry.Name == bsdName)
                {
                    entry.IsEnabled = SCNetworkServiceGetEnabled(service);
                    break;
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Helpers
    //--------------------------------------------------------------------------------

    private static bool IsHiddenConfiguration(IntPtr prefs, IntPtr service)
    {
        var serviceId = CfStringToManaged(SCNetworkServiceGetServiceID(service));
        if (serviceId is null)
        {
            return false;
        }

        using var pathRef = CFRef.CreateString($"/NetworkServices/{serviceId}/Interface");
        var ifaceDict = SCPreferencesPathGetValue(prefs, pathRef);
        if (ifaceDict == IntPtr.Zero)
        {
            return false;
        }

        using var hiddenKeyRef = CFRef.CreateString("HiddenConfiguration");
        var hiddenRef = CFDictionaryGetValue(ifaceDict, hiddenKeyRef);
        return (hiddenRef != IntPtr.Zero) && CFBooleanGetValue(hiddenRef);
    }

    private static NetworkInterfaceType ParseInterfaceType(string? interfaceType) =>
        interfaceType switch
        {
            "Ethernet" => NetworkInterfaceType.Ethernet,
            "IEEE80211" => NetworkInterfaceType.WiFi,
            "Bridge" => NetworkInterfaceType.Bridge,
            "Bond" => NetworkInterfaceType.Bond,
            "VLAN" => NetworkInterfaceType.Vlan,
            "PPP" => NetworkInterfaceType.Ppp,
            "VPN" => NetworkInterfaceType.Vpn,
            _ => NetworkInterfaceType.Unknown
        };
}
