namespace MacDotNet.SystemInfo;

using System.Buffers;
using System.Text;

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

    internal bool Target { get; set; }

    // Interface

    public string Name { get; }

    public string? DisplayName { get; }

    public NetworkInterfaceType InterfaceType { get; }

    // SC service metadata

    public bool IsRegistered { get; }

    public bool IsHidden { get; }

    public bool IsEnabled { get; internal set; }

    // Link

    public bool IsUp { get; internal set; }

    public bool IsLoopback { get; internal set; }

    public uint Mtu { get; internal set; }

    // bits/s (capped at UInt32.MaxValue by the kernel)
    public ulong Baudrate { get; internal set; }

    // Cumulative bytes

    public ulong RxBytes { get; internal set; }
    public ulong RxPackets { get; internal set; }
    public ulong RxErrors { get; internal set; }
    public ulong RxDrops { get; internal set; }
    public ulong RxMulticast { get; internal set; }

    public ulong TxBytes { get; internal set; }
    public ulong TxPackets { get; internal set; }
    public ulong TxErrors { get; internal set; }
    public ulong TxMulticast { get; internal set; }

    public ulong Collisions { get; internal set; }
    public ulong NoProto { get; internal set; }

    internal NetworkStatEntry(string name, string? displayName, NetworkInterfaceType interfaceType, bool isRegistered, bool isHidden)
    {
        Name = name;
        DisplayName = displayName;
        InterfaceType = interfaceType;
        IsRegistered = isRegistered;
        IsHidden = isHidden;
    }
}

public sealed class NetworkStat : IDisposable
{
    private const int ServiceIdBufferSize = 256;

    private static readonly IntPtr PreferencesName = CFSTR("MacDotNet.SystemInfo");

    private readonly bool includeAll;

    private readonly List<NetworkStatEntry> interfaces = [];

    private readonly List<NetworkStatEntry> filteredInterfaces = [];

    private readonly List<ServiceInterface> serviceInterfaces = [];

    private byte[] preferencesSignature = [];

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<NetworkStatEntry> Interfaces => includeAll ? interfaces : filteredInterfaces;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private NetworkStat(bool includeAll)
    {
        this.includeAll = includeAll;
        Update();
    }

    internal static NetworkStat Create(bool includeAll = false) => new(includeAll);

    public void Dispose()
    {
        disposed = true;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        using var pool = AutoreleasePool.Push();

        var mib = stackalloc int[6];
        mib[0] = CTL_NET;
        mib[1] = PF_LINK;
        mib[2] = NETLINK_GENERIC;
        mib[3] = IFMIB_IFALLDATA;
        mib[4] = 0;
        mib[5] = IFDATA_GENERAL;
        var size = IntPtr.Zero;
        if ((sysctl(mib, 6, null, ref size, IntPtr.Zero, IntPtr.Zero) != 0) || (size == IntPtr.Zero))
        {
            return false;
        }

        var buffer = ArrayPool<byte>.Shared.Rent((int)size);
        try
        {
            fixed (byte* ptr = buffer)
            {
                size = buffer.Length;
                if (sysctl(mib, 6, ptr, ref size, IntPtr.Zero, IntPtr.Zero) != 0)
                {
                    return false;
                }

                foreach (var iface in interfaces)
                {
                    iface.Live = false;
                }

                var added = false;
                var filterAdded = false;

                Span<char> nameBuffer = stackalloc char[IFNAMSIZ];

                var count = (int)size / sizeof(ifmibdata);
                for (var i = 0; i < count; i++)
                {
                    var data = (ifmibdata*)ptr + i;
                    var name = DecodeInterfaceName(data->ifmd_name, nameBuffer);
                    // Interface not attached
                    if (name.IsEmpty)
                    {
                        continue;
                    }

                    var raw = &data->ifmd_data;

                    var iface = default(NetworkStatEntry);
                    foreach (var item in interfaces)
                    {
                        if (name.SequenceEqual(item.Name))
                        {
                            iface = item;
                            break;
                        }
                    }

                    if (iface is null)
                    {
                        iface = CreateEntry(name.ToString());
                        iface.Target = includeAll || (iface.IsRegistered && !iface.IsHidden);

                        interfaces.Add(iface);
                        added = true;

                        if (!includeAll && iface.Target)
                        {
                            filteredInterfaces.Add(iface);
                            filterAdded = true;
                        }
                    }

                    if (iface.Target)
                    {
                        iface.IsUp = (data->ifmd_flags & IFF_UP) != 0;
                        iface.IsLoopback = (data->ifmd_flags & IFF_LOOPBACK) != 0;
                        iface.Mtu = raw->ifi_mtu;
                        iface.Baudrate = raw->ifi_baudrate;
                        iface.RxBytes = raw->ifi_ibytes;
                        iface.RxPackets = raw->ifi_ipackets;
                        iface.RxErrors = raw->ifi_ierrors;
                        iface.RxDrops = raw->ifi_iqdrops;
                        iface.RxMulticast = raw->ifi_imcasts;
                        iface.TxBytes = raw->ifi_obytes;
                        iface.TxPackets = raw->ifi_opackets;
                        iface.TxErrors = raw->ifi_oerrors;
                        iface.TxMulticast = raw->ifi_omcasts;
                        iface.Collisions = raw->ifi_collisions;
                        iface.NoProto = raw->ifi_noproto;
                    }

                    iface.Live = true;
                }

                for (var i = interfaces.Count - 1; i >= 0; i--)
                {
                    var iface = interfaces[i];
                    if (!iface.Live)
                    {
                        if (iface.Target)
                        {
                            filteredInterfaces.Remove(iface);
                        }
                        interfaces.RemoveAt(i);
                    }
                }

                if (added)
                {
                    interfaces.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
                    if (filterAdded)
                    {
                        filteredInterfaces.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
                    }
                }

                RefreshEnabledState();

                UpdateAt = DateTime.Now;

                return true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private unsafe void RefreshEnabledState()
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

        using var prefs = new CFRef(SCPreferencesCreate(IntPtr.Zero, PreferencesName, IntPtr.Zero));
        if (!prefs.IsValid)
        {
            return;
        }

        using var services = new CFRef(SCNetworkServiceCopyAll(prefs));
        if (!services.IsValid)
        {
            return;
        }

        var signature = SCPreferencesGetSignature(prefs);
        if (signature != IntPtr.Zero)
        {
            var signatureBytes = new ReadOnlySpan<byte>((void*)CFDataGetBytePtr(signature), (int)CFDataGetLength(signature));
            if (!signatureBytes.SequenceEqual(preferencesSignature))
            {
                preferencesSignature = signatureBytes.ToArray();
                serviceInterfaces.Clear();
            }
        }

        foreach (var item in serviceInterfaces)
        {
            item.Live = false;
        }

        var idBuffer = stackalloc byte[ServiceIdBufferSize];

        var count = CFArrayGetCount(services);
        for (var i = 0L; i < count; i++)
        {
            var service = CFArrayGetValueAtIndex(services, i);
            if (service == IntPtr.Zero)
            {
                continue;
            }

            var serviceId = SCNetworkServiceGetServiceID(service);
            if ((serviceId == IntPtr.Zero) || !CFStringGetCString(serviceId, idBuffer, ServiceIdBufferSize, kCFStringEncodingUTF8))
            {
                continue;
            }

            var id = new ReadOnlySpan<byte>(idBuffer, ServiceIdBufferSize);
            var length = id.IndexOf((byte)0);
            if (length < 0)
            {
                continue;
            }

            id = id[..length];

            var cached = default(ServiceInterface);
            foreach (var item in serviceInterfaces)
            {
                if (id.SequenceEqual(item.ServiceId))
                {
                    cached = item;
                    break;
                }
            }

            if (cached is null)
            {
                cached = new ServiceInterface(id.ToArray(), GetInterfaceBsdName(service));
                serviceInterfaces.Add(cached);
            }

            cached.Live = true;

            var bsdName = cached.BsdName;
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

        for (var i = serviceInterfaces.Count - 1; i >= 0; i--)
        {
            if (!serviceInterfaces[i].Live)
            {
                serviceInterfaces.RemoveAt(i);
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static NetworkStatEntry CreateEntry(string bsdName)
    {
        using var prefs = new CFRef(SCPreferencesCreate(IntPtr.Zero, PreferencesName, IntPtr.Zero));
        if (!prefs.IsValid)
        {
            return new NetworkStatEntry(bsdName, null, NetworkInterfaceType.Unknown, false, false);
        }

        using var services = new CFRef(SCNetworkServiceCopyAll(prefs));
        if (!services.IsValid)
        {
            return new NetworkStatEntry(bsdName, null, NetworkInterfaceType.Unknown, false, false);
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

            var name = ToManagedString(SCNetworkInterfaceGetBSDName(iface));
            if (name != bsdName)
            {
                continue;
            }

            var displayName = ToManagedString(SCNetworkServiceGetName(service));
            var interfaceType = ParseInterfaceType(ToManagedString(SCNetworkInterfaceGetInterfaceType(iface)));
            var isHidden = IsHiddenConfiguration(prefs, service);
            return new NetworkStatEntry(bsdName, displayName, interfaceType, true, isHidden);
        }

        // SC service not found
        return new NetworkStatEntry(bsdName, null, NetworkInterfaceType.Unknown, false, false);
    }

    private static bool IsHiddenConfiguration(IntPtr prefs, IntPtr service)
    {
        var serviceId = ToManagedString(SCNetworkServiceGetServiceID(service));
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

    private static unsafe ReadOnlySpan<char> DecodeInterfaceName(byte* name, Span<char> buffer)
    {
        var bytes = new ReadOnlySpan<byte>(name, IFNAMSIZ);
        var length = bytes.IndexOf((byte)0);
        if (length >= 0)
        {
            bytes = bytes[..length];
        }

        return buffer[..Encoding.UTF8.GetChars(bytes, buffer)];
    }

    private static string? GetInterfaceBsdName(IntPtr service)
    {
        var iface = SCNetworkServiceGetInterface(service);
        return iface != IntPtr.Zero ? ToManagedString(SCNetworkInterfaceGetBSDName(iface)) : null;
    }

    private sealed class ServiceInterface(byte[] serviceId, string? bsdName)
    {
        public byte[] ServiceId { get; } = serviceId;

        public string? BsdName { get; } = bsdName;

        public bool Live { get; set; }
    }
}
