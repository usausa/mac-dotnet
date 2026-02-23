namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// ネットワークインターフェース 1 本分の統計スナップショット。
/// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
/// <para>
/// Statistics snapshot for a single network interface.
/// Values are cumulative since kernel boot. The caller is responsible for computing deltas.
/// </para>
/// </summary>
public readonly record struct NetworkInterfaceStat(
    /// <summary>インターフェース名。例: "en0"<br/>Interface name. Example: "en0"</summary>
    string Name,

    /// <summary>受信バイト数の累積値<br/>Cumulative bytes received</summary>
    uint RxBytes,
    /// <summary>受信パケット数の累積値<br/>Cumulative packets received</summary>
    uint RxPackets,
    /// <summary>受信エラー数の累積値<br/>Cumulative receive errors</summary>
    uint RxErrors,
    /// <summary>受信ドロップ数の累積値<br/>Cumulative receive drops</summary>
    uint RxDrops,
    /// <summary>受信マルチキャストパケット数の累積値<br/>Cumulative multicast packets received</summary>
    uint RxMulticast,
    /// <summary>送信バイト数の累積値<br/>Cumulative bytes transmitted</summary>
    uint TxBytes,
    /// <summary>送信パケット数の累積値<br/>Cumulative packets transmitted</summary>
    uint TxPackets,
    /// <summary>送信エラー数の累積値<br/>Cumulative transmit errors</summary>
    uint TxErrors,
    /// <summary>送信マルチキャストパケット数の累積値<br/>Cumulative multicast packets transmitted</summary>
    uint TxMulticast,
    /// <summary>コリジョン数の累積値<br/>Cumulative collision count</summary>
    uint Collisions,
    /// <summary>未知プロトコルによる受信パケット数の累積値<br/>Cumulative packets received for unknown protocols</summary>
    uint NoProto
);

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
    /// <summary>
    /// HiddenConfiguration 除外が有効な場合、対象インターフェース名のセット。
    /// null の場合はフィルタリングなし (全インターフェースを対象)。
    /// インスタンス生成時に一度だけ構築し、Update() では使い回す。
    /// <para>
    /// Set of included interface names when HiddenConfiguration filtering is enabled.
    /// Null means no filtering (all interfaces included).
    /// Built once at construction time and reused in each Update() call.
    /// </para>
    /// </summary>
    private readonly HashSet<string>? _includedInterfaces;

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>
    /// 全インターフェースの統計エントリ一覧 (名前昇順)。
    /// 値はカーネル起動からの累積値。差分が必要な場合は呼び出し元で計算する。
    /// <para>List of statistics entries for all interfaces (sorted by name). Values are cumulative since boot.</para>
    /// </summary>
    public IReadOnlyList<NetworkInterfaceStat> Interfaces { get; private set; } = [];

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private NetworkStats(HashSet<string>? includedInterfaces)
    {
        _includedInterfaces = includedInterfaces;
        Update();
    }

    /// <summary>
    /// NetworkStats インスタンスを生成する。
    /// </summary>
    /// <param name="excludeHiddenConfiguration">
    /// true の場合、macOS System Settings のネットワーク画面に表示されない
    /// HiddenConfiguration なインターフェース (Thunderbolt ポート用 Ethernet Adapter 等) を除外する。
    /// 対象インターフェースの決定はインスタンス生成時に一度だけ行われ、Update() では使い回される。
    /// </param>
    public static NetworkStats Create(bool excludeHiddenConfiguration = false)
    {
        var includedInterfaces = excludeHiddenConfiguration ? BuildNonHiddenInterfaceSet() : null;
        return new NetworkStats(includedInterfaces);
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// getifaddrs(3) から全インターフェースの if_data カウンタを取得し、Interfaces を更新する。
    /// HiddenConfiguration 除外は Create() 時に決定済みのセットで行い、このメソッドでは再判定しない。
    /// </summary>
    public unsafe bool Update()
    {
        if (getifaddrs(out var ifap) != 0)
        {
            return false;
        }

        try
        {
            var entries = new List<NetworkInterfaceStat>();

            for (var ptr = ifap; ptr != IntPtr.Zero;)
            {
                var ifa = Marshal.PtrToStructure<ifaddrs>(ptr);
                var name = Marshal.PtrToStringUTF8(ifa.ifa_name);

                if (name is not null
                    && ifa.ifa_addr != IntPtr.Zero
                    && ((sockaddr*)ifa.ifa_addr)->sa_family == AF_LINK
                    && ifa.ifa_data != IntPtr.Zero
                    && (_includedInterfaces is null || _includedInterfaces.Contains(name)))
                {
                    var raw = *(if_data*)ifa.ifa_data;
                    entries.Add(new NetworkInterfaceStat(
                        Name: name,
                        RxBytes: raw.ifi_ibytes,
                        RxPackets: raw.ifi_ipackets,
                        RxErrors: raw.ifi_ierrors,
                        RxDrops: raw.ifi_iqdrops,
                        RxMulticast: raw.ifi_imcasts,
                        TxBytes: raw.ifi_obytes,
                        TxPackets: raw.ifi_opackets,
                        TxErrors: raw.ifi_oerrors,
                        TxMulticast: raw.ifi_omcasts,
                        Collisions: raw.ifi_collisions,
                        NoProto: raw.ifi_noproto
                    ));
                }

                ptr = ifa.ifa_next;
            }

            entries.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            Interfaces = entries;
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
    /// SC preferences から HiddenConfiguration でないインターフェース名のセットを構築する。
    /// このメソッドはインスタンス生成時に一度だけ呼ばれる。
    /// </summary>
    private static HashSet<string>? BuildNonHiddenInterfaceSet()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        var appName = NativeMethods.CFStringCreateWithCString(IntPtr.Zero, "MacDotNet.SystemInfo", NativeMethods.kCFStringEncodingUTF8);
        var prefs = NativeMethods.SCPreferencesCreate(IntPtr.Zero, appName, IntPtr.Zero);
        NativeMethods.CFRelease(appName);

        if (prefs == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var services = NativeMethods.SCNetworkServiceCopyAll(prefs);
            if (services == IntPtr.Zero)
            {
                return null;
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

                    if (NetworkInfo.IsHiddenConfiguration(prefs, service))
                    {
                        continue;
                    }

                    var iface = NativeMethods.SCNetworkServiceGetInterface(service);
                    if (iface == IntPtr.Zero)
                    {
                        continue;
                    }

                    var bsdNameRef = NativeMethods.SCNetworkInterfaceGetBSDName(iface);
                    var bsdName = NativeMethods.CfStringToManaged(bsdNameRef);
                    if (bsdName is not null)
                    {
                        result.Add(bsdName);
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
}
