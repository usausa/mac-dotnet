namespace MacDotNet.SystemInfo;

#pragma warning disable CA1024
/// <summary>
/// macOS システム情報 API の統一エントリーポイント。
/// すべてのカテゴリのシステム情報を一か所から取得できる Facade クラス。
/// <para>
/// Unified entry point for macOS system information APIs.
/// A facade class providing access to all system information categories from a single location.
/// </para>
/// </summary>
public static class PlatformProvider
{
    //--------------------------------------------------------------------------------
    // System / システム情報
    //--------------------------------------------------------------------------------

    /// <summary>カーネル情報を取得する。<br/>Returns kernel information.</summary>
    public static KernelInfo GetKernel() => KernelInfo.Create();

    /// <summary>ハードウェア構成情報を取得する。<br/>Returns hardware configuration information.</summary>
    public static HardwareInfo GetHardware() => HardwareInfo.Create();

    /// <summary>CPU パフォーマンスレベル (P-core / E-core) の一覧を取得する。Apple Silicon 以外では空配列。<br/>Returns CPU performance levels (P-core / E-core). Empty array on non-Apple Silicon.</summary>
    public static IReadOnlyList<PerformanceLevelEntry> GetPerformanceLevels() => HardwareInfo.GetPerformanceLevels();

    //--------------------------------------------------------------------------------
    // Load / CPU 負荷
    //--------------------------------------------------------------------------------

    /// <summary>CPU 累積ティック数を取得する。使用率の計算は呼び出し元で行う。<br/>Returns cumulative CPU tick counts. The caller is responsible for computing usage rates.</summary>
    public static CpuStat GetCpuStat() => CpuStat.Create();

    //--------------------------------------------------------------------------------
    // Memory / メモリ
    //--------------------------------------------------------------------------------

    /// <summary>メモリ統計を取得する。<br/>Returns memory statistics.</summary>
    public static MemoryStat GetMemoryStat() => new();

    /// <summary>スワップ使用量を取得する。<br/>Returns swap space usage.</summary>
    public static SwapUsage GetSwapStat() => new();

    //--------------------------------------------------------------------------------
    // Storage / ストレージ
    //--------------------------------------------------------------------------------

    /// <summary>すべてのマウント済みファイルシステムの詳細情報を取得する。<br/>Returns detailed information for all mounted file systems.</summary>
    public static IReadOnlyList<FileSystemEntry> GetFileSystems() => FileSystemInfo.GetFileSystems();

    /// <summary>ユーザーから見えるローカルボリュームの一覧を取得する。<br/>Returns user-visible local disk volumes.</summary>
    public static IReadOnlyList<DiskVolume> GetDiskVolumes() => FileSystemInfo.GetDiskVolumes();

    /// <summary>指定したマウントポイントのディスク使用量を取得する。<br/>Returns disk usage for the specified mount point.</summary>
    public static FileSystemUsage GetFileSystemUsage(string mountPoint) => new(mountPoint);

    //--------------------------------------------------------------------------------
    // Network
    //--------------------------------------------------------------------------------

    /// <summary>
    /// ネットワークインターフェースの設定情報の一覧を返す。macOS SC 固有情報 (サービス名・種別・有効状態) を提供する。
    /// デフォルト (includeAll = false) では macOS System Settings に表示される有効なサービスのみを返す。
    /// includeAll = true にするとすべてのインターフェースを返す。
    /// <see cref="System.Net.NetworkInformation.NetworkInterface"/> と Name (BSD 名) で突合して使用する。
    /// <para>
    /// Returns network interface configuration entries with macOS SC-specific info (service name, type, enabled state).
    /// By default (includeAll = false), only enabled services visible in macOS System Settings are returned.
    /// Set includeAll = true to include all interfaces.
    /// Join with <see cref="System.Net.NetworkInformation.NetworkInterface"/> using the Name (BSD name) as key.
    /// </para>
    /// </summary>
    public static IReadOnlyList<NetworkInterfaceEntry> GetNetworkInterfaces(bool includeAll = false) => NetworkInfo.GetNetworkInterfaces(includeAll);

    /// <summary>
    /// 全ネットワークインターフェースのトラフィック統計を返す。
    /// <see cref="NetworkStats.Update()"/> を呼ぶたびに累積値が更新される。差分が必要な場合は呼び出し元で計算する。
    /// <para>
    /// Returns traffic statistics for all network interfaces.
    /// Cumulative values are updated on each call to <see cref="NetworkStats.Update()"/>.
    /// The caller is responsible for computing deltas.
    /// </para>
    /// </summary>
    public static NetworkStats GetNetworkStats() => NetworkStats.Create();

    //--------------------------------------------------------------------------------
    // Process / プロセス
    //--------------------------------------------------------------------------------

    /// <summary>全プロセスの情報を PID 昇順で取得する。<br/>Returns information for all processes sorted by PID ascending.</summary>
    public static ProcessInfo[] GetProcesses() => ProcessInfo.GetProcesses();

    /// <summary>指定 PID のプロセス情報を取得する。存在しない場合は null を返す。<br/>Returns information for the specified PID. Returns null if not found.</summary>
    public static ProcessInfo? GetProcess(int processId) => ProcessInfo.GetProcess(processId);

    /// <summary>プロセス数とスレッド総数のサマリを取得する。<br/>Returns a summary of process and thread counts.</summary>
    public static ProcessSummary GetProcessSummary() => new();

    //--------------------------------------------------------------------------------
    // GPU
    //--------------------------------------------------------------------------------

    /// <summary>搭載 GPU の静的ハードウェア情報を取得する。<br/>Returns static hardware information for all installed GPUs.</summary>
    public static GpuInfo[] GetGpuInfos() => HardwareInfo.GetGpus();

    /// <summary>GPU デバイスの動的統計・センサー情報を取得する。<br/>Returns dynamic statistics and sensor readings for GPU devices.</summary>
    public static GpuDevice[] GetGpuDevices() => GpuDevice.GetDevices();

    //--------------------------------------------------------------------------------
    // Power / 電源・バッテリー
    //--------------------------------------------------------------------------------

    /// <summary>IOPowerSources からバッテリーのサマリ情報を取得する。<br/>Returns battery summary information from IOPowerSources.</summary>
    public static Battery GetBattery() => new();

    /// <summary>IOKit/AppleSmartBattery からバッテリーの詳細情報を取得する。<br/>Returns detailed battery information from IOKit/AppleSmartBattery.</summary>
    public static BatteryDetail GetBatteryDetail() => BatteryDetail.Create();

    /// <summary>IOPowerSources と AppleSmartBattery を統合したバッテリー情報を取得する。<br/>Returns unified battery information combining IOPowerSources and AppleSmartBattery.</summary>
    public static BatteryGeneric GetBatteryGeneric() => BatteryGeneric.Create();

    /// <summary>Apple Silicon IOReport エネルギーカウンターを取得する。ARM64 以外では Supported = false。<br/>Returns the Apple Silicon IOReport energy counter. Supported = false on non-ARM64.</summary>
    public static AppleSiliconEnergyCounter GetAppleSiliconEnergyCounter() => AppleSiliconEnergyCounter.Create();

    //--------------------------------------------------------------------------------
    // Sensor / SMC センサー
    //--------------------------------------------------------------------------------

    /// <summary>
    /// SMC センサーモニターを生成する。温度・電圧・電力・ファンをまとめて管理し、
    /// Update() で一括更新できる。使用後は Dispose() を呼び出すこと。
    /// AppleSMC サービスが見つからない場合は null を返す。
    /// <para>
    /// Creates an SMC hardware monitor that manages temperature, voltage, power, and fan sensors.
    /// Call Update() to refresh all sensors at once. Call Dispose() when done.
    /// Returns null if the AppleSMC service is not found.
    /// </para>
    /// </summary>
    public static HardwareMonitor? GetHardwareMonitor() => HardwareMonitor.Create();
}

