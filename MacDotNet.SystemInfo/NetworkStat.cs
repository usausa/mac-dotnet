namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

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

    // Interface

    public string Name { get; }

    public string? DisplayName { get; }

    public NetworkInterfaceType InterfaceType { get; }

    // SC service metadata

    public bool IsRegistered { get; }

    public bool IsHidden { get; }

    public bool IsEnabled { get; internal set; }

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

            RefreshEnabledState();

            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            freeifaddrs(ifap);
        }
    }

    private void RefreshEnabledState()
    {
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

        var appNameRef = CFStringCreateWithCString(IntPtr.Zero, "MacDotNet.SystemInfo", kCFStringEncodingUTF8);
        IntPtr prefs;
        try
        {
            prefs = SCPreferencesCreate(IntPtr.Zero, appNameRef, IntPtr.Zero);
        }
        finally
        {
            CFRelease(appNameRef);
        }

        if (prefs == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var services = SCNetworkServiceCopyAll(prefs);
            if (services == IntPtr.Zero)
            {
                return;
            }

            try
            {
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
                        if (entry.IsRegistered && !entry.IsHidden && (entry.Name == bsdName))
                        {
                            entry.IsEnabled = SCNetworkServiceGetEnabled(service);
                            break;
                        }
                    }
                }
            }
            finally
            {
                CFRelease(services);
            }
        }
        finally
        {
            CFRelease(prefs);
        }
    }

    //--------------------------------------------------------------------------------
    // Helpers
    //--------------------------------------------------------------------------------

    private static NetworkStatEntry CreateEntry(string name)
    {
        var appNameRef = CFStringCreateWithCString(IntPtr.Zero, "MacDotNet.SystemInfo", kCFStringEncodingUTF8);
        IntPtr prefs;
        try
        {
            prefs = SCPreferencesCreate(IntPtr.Zero, appNameRef, IntPtr.Zero);
        }
        finally
        {
            CFRelease(appNameRef);
        }

        if (prefs == IntPtr.Zero)
        {
            return new NetworkStatEntry(name, null, NetworkInterfaceType.Unknown, isRegistered: false, isHidden: false);
        }

        try
        {
            var services = SCNetworkServiceCopyAll(prefs);
            if (services == IntPtr.Zero)
            {
                return new NetworkStatEntry(name, null, NetworkInterfaceType.Unknown, isRegistered: false, isHidden: false);
            }

            try
            {
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
                    if (bsdName != name)
                    {
                        continue;
                    }

                    var isHidden = IsHiddenConfiguration(prefs, service);
                    var displayName = CfStringToManaged(SCNetworkServiceGetName(service));
                    var interfaceType = ParseInterfaceType(CfStringToManaged(SCNetworkInterfaceGetInterfaceType(iface)));
                    return new NetworkStatEntry(name, displayName, interfaceType, isRegistered: true, isHidden: isHidden);
                }

                // Not found
                return new NetworkStatEntry(name, null, NetworkInterfaceType.Unknown, isRegistered: false, isHidden: false);
            }
            finally
            {
                CFRelease(services);
            }
        }
        finally
        {
            CFRelease(prefs);
        }
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

    private static bool IsHiddenConfiguration(IntPtr prefs, IntPtr service)
    {
        var serviceId = CfStringToManaged(SCNetworkServiceGetServiceID(service));
        if (serviceId is null)
        {
            return false;
        }

        var pathRef = CFStringCreateWithCString(IntPtr.Zero, $"/NetworkServices/{serviceId}/Interface", kCFStringEncodingUTF8);
        IntPtr ifaceDict;
        try
        {
            ifaceDict = SCPreferencesPathGetValue(prefs, pathRef);
        }
        finally
        {
            CFRelease(pathRef);
        }

        if (ifaceDict == IntPtr.Zero)
        {
            return false;
        }

        var hiddenKeyRef = CFStringCreateWithCString(IntPtr.Zero, "HiddenConfiguration", kCFStringEncodingUTF8);
        IntPtr hiddenRef;
        try
        {
            hiddenRef = CFDictionaryGetValue(ifaceDict, hiddenKeyRef);
        }
        finally
        {
            CFRelease(hiddenKeyRef);
        }

        return (hiddenRef != IntPtr.Zero) && CFBooleanGetValue(hiddenRef);
    }
}
