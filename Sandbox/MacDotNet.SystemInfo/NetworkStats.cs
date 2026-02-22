namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// ネットワークインターフェース 1 本分の統計スナップショット。
/// 累積値 (カーネル起動からの合計) とデルタ値 (前回 Update() からの差分) を保持する。
/// </summary>
public readonly record struct NetworkInterfaceStat(
    /// <summary>インターフェース名。例: "en0"</summary>
    string Name,

    // ---- 累積値 ----

    /// <summary>受信バイト数の累積値</summary>
    uint RxBytes,
    /// <summary>受信パケット数の累積値</summary>
    uint RxPackets,
    /// <summary>受信エラー数の累積値</summary>
    uint RxErrors,
    /// <summary>受信ドロップ数の累積値</summary>
    uint RxDrops,
    /// <summary>受信マルチキャストパケット数の累積値</summary>
    uint RxMulticast,
    /// <summary>送信バイト数の累積値</summary>
    uint TxBytes,
    /// <summary>送信パケット数の累積値</summary>
    uint TxPackets,
    /// <summary>送信エラー数の累積値</summary>
    uint TxErrors,
    /// <summary>送信マルチキャストパケット数の累積値</summary>
    uint TxMulticast,
    /// <summary>コリジョン数の累積値</summary>
    uint Collisions,
    /// <summary>未知プロトコルによる受信パケット数の累積値</summary>
    uint NoProto,

    // ---- デルタ値 (前回 Update() からの差分、初回は 0) ----

    /// <summary>受信バイト数のデルタ</summary>
    uint DeltaRxBytes,
    /// <summary>受信パケット数のデルタ</summary>
    uint DeltaRxPackets,
    /// <summary>受信エラー数のデルタ</summary>
    uint DeltaRxErrors,
    /// <summary>受信ドロップ数のデルタ</summary>
    uint DeltaRxDrops,
    /// <summary>送信バイト数のデルタ</summary>
    uint DeltaTxBytes,
    /// <summary>送信パケット数のデルタ</summary>
    uint DeltaTxPackets,
    /// <summary>送信エラー数のデルタ</summary>
    uint DeltaTxErrors,
    /// <summary>コリジョン数のデルタ</summary>
    uint DeltaCollisions
);

/// <summary>
/// 全ネットワークインターフェースのトラフィック統計。
/// <see cref="Create()"/> でインスタンスを生成し、<see cref="Update()"/> を呼ぶたびに
/// 最新値とデルタ値を更新する。<see cref="CpuUsage"/> と同じパターン。
/// <para>
/// <see cref="System.Net.NetworkInformation.NetworkInterface.GetIPv4Statistics()"/> に対する追加価値:
/// デルタ値の計算、Collisions / NoProto 等 .NET 標準では取得できない macOS 固有カウンタを提供する。
/// </para>
/// </summary>
public sealed class NetworkStats
{
    private Dictionary<string, NetworkInterfaceStat> _previous = [];

    /// <summary>最後に Update() を呼び出した日時</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>
    /// 全インターフェースの統計エントリ一覧 (名前昇順)。
    /// デルタ値は初回 Update() では 0、2 回目以降から有効。
    /// </summary>
    public IReadOnlyList<NetworkInterfaceStat> Interfaces { get; private set; } = [];

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private NetworkStats()
    {
        Update();
    }

    public static NetworkStats Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// getifaddrs(3) から全インターフェースの if_data カウンタを取得し、
    /// 前回スナップショットとの差分でデルタを計算して Interfaces を更新する。
    /// </summary>
    public unsafe bool Update()
    {
        if (getifaddrs(out var ifap) != 0)
        {
            return false;
        }

        try
        {
            // AF_LINK エントリから if_data を収集 (1インターフェースにつき1エントリ)
            var raws = new Dictionary<string, if_data>(StringComparer.Ordinal);

            for (var ptr = ifap; ptr != IntPtr.Zero;)
            {
                var ifa = Marshal.PtrToStructure<ifaddrs>(ptr);
                var name = Marshal.PtrToStringUTF8(ifa.ifa_name);

                if (name is not null
                    && ifa.ifa_addr != IntPtr.Zero
                    && ((sockaddr*)ifa.ifa_addr)->sa_family == AF_LINK
                    && ifa.ifa_data != IntPtr.Zero)
                {
                    raws[name] = *(if_data*)ifa.ifa_data;
                }

                ptr = ifa.ifa_next;
            }

            // スナップショット構築
            var entries = new List<NetworkInterfaceStat>(raws.Count);
            var newPrev = new Dictionary<string, NetworkInterfaceStat>(raws.Count, StringComparer.Ordinal);

            foreach (var (name, raw) in raws)
            {
                var hasPrev = _previous.TryGetValue(name, out var prev);

                var stat = new NetworkInterfaceStat(
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
                    NoProto: raw.ifi_noproto,
                    DeltaRxBytes: hasPrev ? unchecked(raw.ifi_ibytes - prev.RxBytes) : 0,
                    DeltaRxPackets: hasPrev ? unchecked(raw.ifi_ipackets - prev.RxPackets) : 0,
                    DeltaRxErrors: hasPrev ? unchecked(raw.ifi_ierrors - prev.RxErrors) : 0,
                    DeltaRxDrops: hasPrev ? unchecked(raw.ifi_iqdrops - prev.RxDrops) : 0,
                    DeltaTxBytes: hasPrev ? unchecked(raw.ifi_obytes - prev.TxBytes) : 0,
                    DeltaTxPackets: hasPrev ? unchecked(raw.ifi_opackets - prev.TxPackets) : 0,
                    DeltaTxErrors: hasPrev ? unchecked(raw.ifi_oerrors - prev.TxErrors) : 0,
                    DeltaCollisions: hasPrev ? unchecked(raw.ifi_collisions - prev.Collisions) : 0
                );

                entries.Add(stat);
                newPrev[name] = stat;
            }

            entries.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            Interfaces = entries;
            _previous = newPrev;
            UpdateAt = DateTime.Now;

            return true;
        }
        finally
        {
            freeifaddrs(ifap);
        }
    }
}
