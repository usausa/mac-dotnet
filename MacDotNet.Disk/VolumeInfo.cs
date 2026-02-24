namespace MacDotNet.Disk;

/// <summary>
/// 物理ディスクに関連付けられたマウント済みボリュームの情報。
/// <see cref="DiskInfoExtensions.GetVolumes"/> によって返される。
/// <para>
/// Information about a mounted volume associated with a physical disk.
/// Returned by <see cref="DiskInfoExtensions.GetVolumes"/>.
/// </para>
/// </summary>
public sealed class VolumeInfo
{
    /// <summary>マウントポイントのパス。例: "/"、"/Volumes/Macintosh HD"<br/>Mount point path. Example: "/", "/Volumes/Macintosh HD"</summary>
    public string MountPoint { get; internal set; } = default!;

    /// <summary>ファイルシステムの種類。例: "apfs"、"hfs"、"exfat"<br/>File system type. Example: "apfs", "hfs", "exfat"</summary>
    public string TypeName { get; internal set; } = default!;

    /// <summary>デバイスパス。例: "/dev/disk3s1s1"<br/>Device path. Example: "/dev/disk3s1s1"</summary>
    public string DeviceName { get; internal set; } = default!;

    /// <summary>読み取り専用マウントかどうか<br/>Whether the volume is mounted read-only</summary>
    public bool IsReadOnly { get; internal set; }

    /// <summary>総容量 (バイト)<br/>Total capacity in bytes</summary>
    public ulong TotalSize { get; internal set; }

    /// <summary>空き容量 (バイト; スーパーユーザー向け予約を含む)<br/>Free capacity in bytes, including superuser-reserved space</summary>
    public ulong FreeSize { get; internal set; }

    /// <summary>一般ユーザーが利用可能な空き容量 (バイト)<br/>Available capacity in bytes for non-superuser</summary>
    public ulong AvailableSize { get; internal set; }

    /// <summary>ディスク使用率 (0.0〜1.0)<br/>Disk usage ratio from 0.0 to 1.0</summary>
    public double UsagePercent { get; internal set; }
}
