// IOKit、IOBlockStorageDeviceを使用したディスク情報の取得処理。
// デバイス名、モデル名、ベンダ名、シリアル等の基本情報に加え、
// IOBlockStorageDriverからのI/O統計情報、および
// NVMe/ATA双方のSMART情報取得を行う。

namespace WorkSmart;

using System.Runtime.InteropServices;

using static WorkSmart.NativeMethods;

internal static class Program
{
    public static void Main()
    {
        var disks = DiskInfoProvider.GetDiskInfo();
        if (disks.Length == 0)
        {
            Console.WriteLine("No disks (IOBlockStorageDevice) found.");
            return;
        }

        for (var i = 0; i < disks.Length; i++)
        {
            var disk = disks[i];

            Console.WriteLine($"=== Disk [{i}] ===");
            Console.WriteLine($"BSD Name:           {disk.BsdName ?? "(n/a)"}");
            Console.WriteLine($"Device Name:        {disk.DeviceName ?? "(n/a)"}");
            Console.WriteLine($"Model:              {disk.ModelName ?? "(n/a)"}");
            Console.WriteLine($"Vendor:             {disk.VendorName ?? "(n/a)"}");
            Console.WriteLine($"Serial Number:      {disk.SerialNumber ?? "(n/a)"}");
            Console.WriteLine($"Firmware Revision:  {disk.FirmwareRevision ?? "(n/a)"}");
            Console.WriteLine($"Medium Type:        {disk.MediumType ?? "(n/a)"}");
            Console.WriteLine($"Removable:          {disk.IsRemovable}");
            Console.WriteLine($"Ejectable:          {disk.IsEjectable}");
            Console.WriteLine($"Physical Block:     {disk.PhysicalBlockSize}");
            Console.WriteLine($"Logical Block:      {disk.LogicalBlockSize}");
            Console.WriteLine($"Disk Size:          {FormatBytes(disk.DiskSize)}");
            Console.WriteLine($"Bus Type:           {disk.PhysicalInterconnect ?? "(n/a)"}");
            Console.WriteLine($"Bus Location:       {disk.PhysicalInterconnectLocation ?? "(n/a)"}");
            Console.WriteLine($"Content Type:       {disk.ContentType ?? "(n/a)"}");
            Console.WriteLine();

            // I/O統計
            if (disk.IOStatistics is not null)
            {
                var s = disk.IOStatistics;
                Console.WriteLine("--- I/O Statistics ---");
                Console.WriteLine($"Bytes Read:         {FormatBytes((ulong)Math.Max(0, s.BytesRead))}");
                Console.WriteLine($"Bytes Written:      {FormatBytes((ulong)Math.Max(0, s.BytesWritten))}");
                Console.WriteLine($"Ops Read:           {s.OperationsRead}");
                Console.WriteLine($"Ops Written:        {s.OperationsWritten}");
                Console.WriteLine($"Time Read:          {s.TotalTimeRead / 1_000_000} ms");
                Console.WriteLine($"Time Written:       {s.TotalTimeWritten / 1_000_000} ms");
                Console.WriteLine($"Retries Read:       {s.RetriesRead}");
                Console.WriteLine($"Retries Written:    {s.RetriesWritten}");
                Console.WriteLine($"Errors Read:        {s.ErrorsRead}");
                Console.WriteLine($"Errors Written:     {s.ErrorsWritten}");
                Console.WriteLine($"Latency Read:       {s.LatencyTimeRead / 1_000_000} ms");
                Console.WriteLine($"Latency Written:    {s.LatencyTimeWritten / 1_000_000} ms");
                Console.WriteLine();
            }

            // NVMe SMART
            if (disk.ReadNvmeSmart is not null)
            {
                Console.WriteLine("--- NVMe SMART ---");
                var smart = disk.ReadNvmeSmart();
                if (smart is not null)
                {
                    DisplayNvmeSmart(smart);

                    // 繰り返し呼び出しのデモ
                    Console.WriteLine();
                    Console.WriteLine("--- NVMe SMART (2nd read) ---");
                    var smart2 = disk.ReadNvmeSmart();
                    if (smart2 is not null)
                    {
                        Console.WriteLine($"Temperature:        {smart2.TemperatureCelsius} C");
                        Console.WriteLine($"Available Spare:    {smart2.AvailableSpare}%");
                        Console.WriteLine($"Percentage Used:    {smart2.PercentageUsed}%");
                    }
                }
                else
                {
                    Console.WriteLine("SMART data not available (may require elevated privileges).");
                }

                Console.WriteLine();
            }

            // ATA SMART
            if (disk.ReadAtaSmart is not null)
            {
                Console.WriteLine("--- ATA SMART ---");
                var smart = disk.ReadAtaSmart();
                if (smart is not null)
                {
                    DisplayAtaSmart(smart);

                    // 繰り返し呼び出しのデモ
                    Console.WriteLine();
                    Console.WriteLine("--- ATA SMART (2nd read) ---");
                    var smart2 = disk.ReadAtaSmart();
                    if (smart2 is not null)
                    {
                        Console.WriteLine($"Attributes found:   {smart2.GetAttributes().Length}");
                    }
                }
                else
                {
                    Console.WriteLine("SMART data not available (may require elevated privileges or unsupported device).");
                }

                Console.WriteLine();
            }
        }
    }

    private static void DisplayNvmeSmart(NvmeSmartLog smart)
    {
        Console.WriteLine($"Critical Warning:   0x{smart.CriticalWarning:X2}");
        Console.WriteLine($"Temperature:        {smart.TemperatureCelsius} C ({smart.TemperatureKelvin} K)");
        Console.WriteLine($"Available Spare:    {smart.AvailableSpare}%");
        Console.WriteLine($"Spare Threshold:    {smart.SpareThreshold}%");
        Console.WriteLine($"Percentage Used:    {smart.PercentageUsed}%");
        Console.WriteLine($"Data Units Read:    {smart.DataUnitsRead}");
        Console.WriteLine($"Data Units Written: {smart.DataUnitsWritten}");
        Console.WriteLine($"Host Read Cmds:     {smart.HostReadCommands}");
        Console.WriteLine($"Host Write Cmds:    {smart.HostWriteCommands}");
        Console.WriteLine($"Ctrl Busy Time:     {smart.ControllerBusyTime}");
        Console.WriteLine($"Power Cycles:       {smart.PowerCycles}");
        Console.WriteLine($"Power On Hours:     {smart.PowerOnHours}");
        Console.WriteLine($"Unsafe Shutdowns:   {smart.UnsafeShutdowns}");
        Console.WriteLine($"Media Errors:       {smart.MediaErrors}");
        Console.WriteLine($"Error Log Entries:  {smart.NumErrorLogEntries}");
        Console.WriteLine($"Warning Temp Time:  {smart.WarningTempTime} min");
        Console.WriteLine($"Critical Temp Time: {smart.CriticalCompTime} min");

        // 追加温度センサー (存在する場合)
        for (var j = 0; j < 8; j++)
        {
            var temp = smart.GetTempSensor(j);
            if (temp > 0)
            {
                Console.WriteLine($"Temp Sensor {j}:      {temp - 273} C ({temp} K)");
            }
        }
    }

    private static void DisplayAtaSmart(AtaSmartData smart)
    {
        Console.WriteLine($"Revision:           {smart.RevisionNumber}");
        var attrs = smart.GetAttributes();
        if (attrs.Length > 0)
        {
            Console.WriteLine($"{"ID",4} {"Cur",4} {"Wst",4} {"Raw",12}  Flags");
            Console.WriteLine(new string('-', 40));
            foreach (var attr in attrs)
            {
                Console.WriteLine($"{attr.AttributeId,4} {attr.CurrentValue,4} {attr.WorstValue,4} {attr.RawValueNumeric,12}  0x{attr.Flags:X4}");
            }
        }
        else
        {
            Console.WriteLine("No SMART attributes found.");
        }
    }

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1UL << 40 => $"{bytes / (double)(1UL << 40):F2} TiB",
        >= 1UL << 30 => $"{bytes / (double)(1UL << 30):F2} GiB",
        >= 1UL << 20 => $"{bytes / (double)(1UL << 20):F2} MiB",
        >= 1UL << 10 => $"{bytes / (double)(1UL << 10):F2} KiB",
        _ => $"{bytes} B",
    };
}

// ディスク情報
internal sealed record DiskInfo
{
    // BSDデバイス名 (例: "disk0")
    public string? BsdName { get; init; }

    // IORegistryエントリ名 (ドライバクラス名等)
    public string? DeviceName { get; init; }

    // モデル名 (Product Name)
    public string? ModelName { get; init; }

    // ベンダ名
    public string? VendorName { get; init; }

    // シリアル番号
    public string? SerialNumber { get; init; }

    // ファームウェアリビジョン
    public string? FirmwareRevision { get; init; }

    // メディアタイプ (Solid State / Rotational 等)
    public string? MediumType { get; init; }

    // リムーバブルメディアか
    public bool IsRemovable { get; init; }

    // メディア取り出し可能か (Ejectable)
    public bool IsEjectable { get; init; }

    // 物理ブロックサイズ (バイト)
    public ulong PhysicalBlockSize { get; init; }

    // 論理ブロックサイズ (バイト)
    public ulong LogicalBlockSize { get; init; }

    // ディスク容量 (バイト)
    public ulong DiskSize { get; init; }

    // 接続バスの種類 (NVMe, SATA, USB, Fibre Channel 等)
    public string? PhysicalInterconnect { get; init; }

    // 接続ロケーション (Internal, External 等)
    public string? PhysicalInterconnectLocation { get; init; }

    // コンテントタイプ (GUID_partition_scheme, Apple_APFS 等)
    public string? ContentType { get; init; }

    // I/O統計情報 (IOBlockStorageDriverのStatisticsプロパティ)
    public DiskIOStatistics? IOStatistics { get; init; }

    // NVMeプロトコルか判定
    // Apple Silicon Macでは内部SSDが "Apple Fabric" として報告されるが、NVMe互換のため含める
    public bool IsNvme => PhysicalInterconnect is "NVMe" or "Apple Fabric";

    // ATAプロトコルか判定
    public bool IsAta => PhysicalInterconnect is "ATA" or "SATA" or "ATAPI";

    // NVMe SMART情報取得デリゲート (繰り返し呼び出し可能、呼び出す度に最新データを取得)
    // プラグインインターフェースを保持し、SMARTReadDataを繰り返し呼び出す
    public Func<NvmeSmartLog?>? ReadNvmeSmart { get; init; }

    // ATA SMART情報取得デリゲート (繰り返し呼び出し可能、呼び出す度に最新データを取得)
    // プラグインインターフェースを保持し、SMARTReadDataを繰り返し呼び出す
    public Func<AtaSmartData?>? ReadAtaSmart { get; init; }
}

// I/O統計情報 (IOBlockStorageDriverのStatistics辞書から取得)
internal sealed record DiskIOStatistics
{
    // 読み取りバイト数
    public long BytesRead { get; init; }

    // 書き込みバイト数
    public long BytesWritten { get; init; }

    // 読み取り操作回数
    public long OperationsRead { get; init; }

    // 書き込み操作回数
    public long OperationsWritten { get; init; }

    // 読み取り合計時間 (ナノ秒)
    public long TotalTimeRead { get; init; }

    // 書き込み合計時間 (ナノ秒)
    public long TotalTimeWritten { get; init; }

    // 読み取りリトライ回数
    public long RetriesRead { get; init; }

    // 書き込みリトライ回数
    public long RetriesWritten { get; init; }

    // 読み取りエラー回数
    public long ErrorsRead { get; init; }

    // 書き込みエラー回数
    public long ErrorsWritten { get; init; }

    // 読み取りレイテンシ (ナノ秒)
    public long LatencyTimeRead { get; init; }

    // 書き込みレイテンシ (ナノ秒)
    public long LatencyTimeWritten { get; init; }
}

// NVMe SMART/Health Information Log (NVMe仕様準拠、512バイト)
// RawDataフィールドで生データに直接アクセス可能。
// 各プロパティはNVMe仕様のオフセットに基づいて値を返す。
internal sealed class NvmeSmartLog
{
    // 生のSMARTデータ (512バイト)
    public required byte[] RawData { get; init; }

    // クリティカル警告ビットフィールド (オフセット0)
    // bit0: 予備領域不足, bit1: 温度超過, bit2: 信頼性低下, bit3: 読み取り専用, bit4: 揮発性バックアップ失敗
    public byte CriticalWarning => RawData[0];

    // 温度 (ケルビン、オフセット1-2、リトルエンディアン)
    public ushort TemperatureKelvin => BitConverter.ToUInt16(RawData, 1);

    // 温度 (摂氏)
    public int TemperatureCelsius => TemperatureKelvin - 273;

    // 利用可能な予備領域 (パーセント、オフセット3)
    public byte AvailableSpare => RawData[3];

    // 予備領域閾値 (パーセント、オフセット4)
    public byte SpareThreshold => RawData[4];

    // 使用率 (パーセント、オフセット5) — 100を超える場合あり
    public byte PercentageUsed => RawData[5];

    // エンデュランスグループクリティカル警告サマリ (オフセット6)
    public byte EnduranceGroupCritWarnSummary => RawData[6];

    // 読み取りデータユニット数 (1ユニット=512バイト×1000、オフセット32-47、128ビット)
    public UInt128 DataUnitsRead => ReadUInt128(32);

    // 書き込みデータユニット数 (オフセット48-63)
    public UInt128 DataUnitsWritten => ReadUInt128(48);

    // ホスト読み取りコマンド数 (オフセット64-79)
    public UInt128 HostReadCommands => ReadUInt128(64);

    // ホスト書き込みコマンド数 (オフセット80-95)
    public UInt128 HostWriteCommands => ReadUInt128(80);

    // コントローラビジー時間 (分、オフセット96-111)
    public UInt128 ControllerBusyTime => ReadUInt128(96);

    // 電源投入サイクル数 (オフセット112-127)
    public UInt128 PowerCycles => ReadUInt128(112);

    // 電源投入時間 (時間、オフセット128-143)
    public UInt128 PowerOnHours => ReadUInt128(128);

    // 安全でないシャットダウン回数 (オフセット144-159)
    public UInt128 UnsafeShutdowns => ReadUInt128(144);

    // メディアおよびデータ整合性エラー数 (オフセット160-175)
    public UInt128 MediaErrors => ReadUInt128(160);

    // エラーログエントリ数 (オフセット176-191)
    public UInt128 NumErrorLogEntries => ReadUInt128(176);

    // 警告温度到達累積時間 (分、オフセット192-195)
    public uint WarningTempTime => BitConverter.ToUInt32(RawData, 192);

    // クリティカル温度到達累積時間 (分、オフセット196-199)
    public uint CriticalCompTime => BitConverter.ToUInt32(RawData, 196);

    // 温度センサー値取得 (ケルビン、0の場合は未実装、オフセット200+index*2)
    public ushort GetTempSensor(int index) =>
        index is >= 0 and < 8 ? BitConverter.ToUInt16(RawData, 200 + (index * 2)) : (ushort)0;

    // サーマルマネジメント温度1遷移回数 (オフセット216-219)
    public uint ThmTemp1TransCount => BitConverter.ToUInt32(RawData, 216);

    // サーマルマネジメント温度2遷移回数 (オフセット220-223)
    public uint ThmTemp2TransCount => BitConverter.ToUInt32(RawData, 220);

    // サーマルマネジメント温度1累積時間 (秒、オフセット224-227)
    public uint ThmTemp1TotalTime => BitConverter.ToUInt32(RawData, 224);

    // サーマルマネジメント温度2累積時間 (秒、オフセット228-231)
    public uint ThmTemp2TotalTime => BitConverter.ToUInt32(RawData, 228);

    private UInt128 ReadUInt128(int offset)
    {
        var lo = BitConverter.ToUInt64(RawData, offset);
        var hi = BitConverter.ToUInt64(RawData, offset + 8);
        return new UInt128(hi, lo);
    }
}

// ATA SMART データ (ATA/ATAPI仕様準拠、512バイト)
// RawDataフィールドで生データに直接アクセス可能。
internal sealed class AtaSmartData
{
    // 生のSMARTデータ (512バイト)
    public required byte[] RawData { get; init; }

    // リビジョン番号 (オフセット0-1)
    public ushort RevisionNumber => BitConverter.ToUInt16(RawData, 0);

    // 指定インデックスのSMART属性を取得 (0-29)
    // 各属性は12バイト、オフセット2から開始
    public AtaSmartAttribute GetAttribute(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 30);

        var offset = 2 + (index * 12);
        var raw = new byte[6];
        Array.Copy(RawData, offset + 5, raw, 0, 6);

        return new AtaSmartAttribute
        {
            AttributeId = RawData[offset],
            Flags = BitConverter.ToUInt16(RawData, offset + 1),
            CurrentValue = RawData[offset + 3],
            WorstValue = RawData[offset + 4],
            RawValue = raw,
        };
    }

    // 有効な (ID != 0) SMART属性の一覧を取得
    public AtaSmartAttribute[] GetAttributes()
    {
        var attrs = new List<AtaSmartAttribute>();
        for (var i = 0; i < 30; i++)
        {
            var attr = GetAttribute(i);
            if (attr.AttributeId != 0)
            {
                attrs.Add(attr);
            }
        }

        return [.. attrs];
    }
}

// ATA SMART 属性 (1エントリ12バイト)
internal sealed record AtaSmartAttribute
{
    // 属性ID (1: Read Error Rate, 5: Reallocated Sectors Count, 194: Temperature 等)
    public required byte AttributeId { get; init; }

    // フラグ (bit0: Pre-fail/Advisory, bit1: Online data collection 等)
    public required ushort Flags { get; init; }

    // 現在値 (1-253、正規化された値)
    public required byte CurrentValue { get; init; }

    // 最悪値 (これまでの最悪の正規化値)
    public required byte WorstValue { get; init; }

    // 生の値 (6バイト、ベンダ固有の意味)
    public required byte[] RawValue { get; init; }

    // 生の値を48ビット数値として返す (リトルエンディアン)
    public ulong RawValueNumeric
    {
        get
        {
            ulong val = 0;
            for (var i = 0; i < RawValue.Length && i < 6; i++)
            {
                val |= (ulong)RawValue[i] << (i * 8);
            }

            return val;
        }
    }
}

// ディスク情報取得処理
// IOBlockStorageDeviceをマッチングキーとして使用し、
// IOKitレジストリからディスクの各種プロパティを取得する。
#pragma warning disable CA1806
internal static class DiskInfoProvider
{
    public static DiskInfo[] GetDiskInfo()
    {
        var matching = IOServiceMatching("IOBlockStorageDevice");
        if (matching == nint.Zero)
        {
            return [];
        }

        // IOServiceGetMatchingServicesはmatchingを消費する (CFRelease不要)
        nint iter = nint.Zero;
        if (IOServiceGetMatchingServices(0, matching, ref iter) != KERN_SUCCESS || iter == nint.Zero)
        {
            return [];
        }

        try
        {
            var results = new List<DiskInfo>();
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    results.Add(ReadDiskEntry(entry));
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }

            return [.. results];
        }
        finally
        {
            IOObjectRelease(iter);
        }
    }

    private static unsafe DiskInfo ReadDiskEntry(uint entry)
    {
        // IORegistryエントリ名を取得
        byte* nameBuf = stackalloc byte[128];
        IORegistryEntryGetName(entry, nameBuf);
        var deviceName = Marshal.PtrToStringUTF8((nint)nameBuf);

        // Device Characteristics辞書からデバイス基本情報を取得
        string? modelName = null;
        string? vendorName = null;
        string? serialNumber = null;
        string? firmwareRevision = null;
        string? mediumType = null;

        var devCharDict = GetDictionaryProperty(entry, "Device Characteristics");
        if (devCharDict != nint.Zero)
        {
            try
            {
                modelName = GetDictString(devCharDict, "Product Name");
                vendorName = GetDictString(devCharDict, "Vendor Name");
                serialNumber = GetDictString(devCharDict, "Serial Number");
                firmwareRevision = GetDictString(devCharDict, "Product Revision Level");
                mediumType = GetDictString(devCharDict, "Medium Type");
            }
            finally
            {
                CFRelease(devCharDict);
            }
        }

        // Protocol Characteristics辞書からバス情報を取得
        string? physInterconnect = null;
        string? physInterconnectLocation = null;

        var protoCharDict = GetDictionaryProperty(entry, "Protocol Characteristics");
        if (protoCharDict != nint.Zero)
        {
            try
            {
                physInterconnect = GetDictString(protoCharDict, "Physical Interconnect");
                physInterconnectLocation = GetDictString(protoCharDict, "Physical Interconnect Location");
            }
            finally
            {
                CFRelease(protoCharDict);
            }
        }

        // 子エントリを再帰検索してIOMedia/IOBlockStorageDriverのプロパティを取得
        var bsdName = SearchStringProperty(entry, "BSD Name");
        var diskSize = SearchNumberProperty(entry, "Size");
        var logicalBlockSize = SearchNumberProperty(entry, "Preferred Block Size");
        var physicalBlockSize = SearchNumberProperty(entry, "Physical Block Size");
        var removable = SearchBoolProperty(entry, "Removable");
        var ejectable = SearchBoolProperty(entry, "Ejectable");
        var contentType = SearchStringProperty(entry, "Content");

        // IOBlockStorageDriverのStatistics辞書からI/O統計を取得
        DiskIOStatistics? ioStats = null;
        var statsDict = SearchDictionaryProperty(entry, "Statistics");
        if (statsDict != nint.Zero)
        {
            try
            {
                ioStats = new DiskIOStatistics
                {
                    BytesRead = GetDictNumber(statsDict, "Bytes (Read)"),
                    BytesWritten = GetDictNumber(statsDict, "Bytes (Write)"),
                    OperationsRead = GetDictNumber(statsDict, "Operations (Read)"),
                    OperationsWritten = GetDictNumber(statsDict, "Operations (Write)"),
                    TotalTimeRead = GetDictNumber(statsDict, "Total Time (Read)"),
                    TotalTimeWritten = GetDictNumber(statsDict, "Total Time (Write)"),
                    RetriesRead = GetDictNumber(statsDict, "Retries (Read)"),
                    RetriesWritten = GetDictNumber(statsDict, "Retries (Write)"),
                    ErrorsRead = GetDictNumber(statsDict, "Errors (Read)"),
                    ErrorsWritten = GetDictNumber(statsDict, "Errors (Write)"),
                    LatencyTimeRead = GetDictNumber(statsDict, "Latency Time (Read)"),
                    LatencyTimeWritten = GetDictNumber(statsDict, "Latency Time (Write)"),
                };
            }
            finally
            {
                CFRelease(statsDict);
            }
        }

        // 物理ブロックサイズが取得できなかった場合、論理ブロックサイズをフォールバックとして使用
        if (physicalBlockSize <= 0 && logicalBlockSize > 0)
        {
            physicalBlockSize = logicalBlockSize;
        }

        // SMART セッションの作成 (プロトコルに応じてNVMeまたはATAのセッションを開く)
        // IOCreatePlugInInterfaceForServiceは同一デバイスに対して1回しか成功しないため、
        // ここでセッションを開き、繰り返し呼び出し可能なデリゲートとしてDiskInfoに保持する
        Func<NvmeSmartLog?>? readNvmeSmart = null;
        Func<AtaSmartData?>? readAtaSmart = null;

        if (physInterconnect is "NVMe" or "Apple Fabric")
        {
            var session = NvmeSmartSession.Open(entry);
            if (session is not null)
            {
                readNvmeSmart = session.ReadData;
            }
        }
        else if (physInterconnect is "ATA" or "SATA" or "ATAPI")
        {
            var session = AtaSmartSession.Open(entry);
            if (session is not null)
            {
                readAtaSmart = session.ReadData;
            }
        }

        return new DiskInfo
        {
            BsdName = bsdName,
            DeviceName = deviceName,
            ModelName = modelName?.Trim(),
            VendorName = vendorName?.Trim(),
            SerialNumber = serialNumber?.Trim(),
            FirmwareRevision = firmwareRevision?.Trim(),
            MediumType = mediumType?.Trim(),
            IsRemovable = removable,
            IsEjectable = ejectable,
            PhysicalBlockSize = physicalBlockSize > 0 ? (ulong)physicalBlockSize : 0,
            LogicalBlockSize = logicalBlockSize > 0 ? (ulong)logicalBlockSize : 0,
            DiskSize = diskSize > 0 ? (ulong)diskSize : 0,
            PhysicalInterconnect = physInterconnect,
            PhysicalInterconnectLocation = physInterconnectLocation,
            ContentType = contentType,
            IOStatistics = ioStats,
            ReadNvmeSmart = readNvmeSmart,
            ReadAtaSmart = readAtaSmart,
        };
    }

    // IORegistryから辞書プロパティを直接取得 (呼び出し元がCFReleaseすること)
    private static nint GetDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return nint.Zero;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, nint.Zero, 0);
            if (val == nint.Zero)
            {
                return nint.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return nint.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して文字列プロパティを取得
    private static string? SearchStringProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return null;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
            {
                return null;
            }

            try
            {
                return CFGetTypeID(val) == CFStringGetTypeID() ? CfStringToManaged(val) : null;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して数値プロパティを取得
    private static long SearchNumberProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
            {
                return 0;
            }

            try
            {
                if (CFGetTypeID(val) != CFNumberGetTypeID())
                {
                    return 0;
                }

                long result = 0;
                CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
                return result;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して真偽値プロパティを取得
    private static bool SearchBoolProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return false;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
            {
                return false;
            }

            try
            {
                return CFGetTypeID(val) == CFBooleanGetTypeID() && CFBooleanGetValue(val);
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // 子エントリを再帰検索して辞書プロパティを取得 (呼び出し元がCFReleaseすること)
    private static nint SearchDictionaryProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return nint.Zero;
        }

        try
        {
            var val = IORegistryEntrySearchCFProperty(
                entry, kIOServicePlane, cfKey, nint.Zero, kIORegistryIterateRecursively);
            if (val == nint.Zero)
            {
                return nint.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return nint.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // CFDictionaryから文字列値を取得
    internal static string? GetDictString(nint dict, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return null;
        }

        try
        {
            // CFDictionaryGetValueはGet規則 — 返り値をCFReleaseしてはならない
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == nint.Zero)
            {
                return null;
            }

            return CFGetTypeID(val) == CFStringGetTypeID() ? CfStringToManaged(val) : null;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // CFDictionaryから数値を取得
    internal static long GetDictNumber(nint dict, string key)
    {
        var cfKey = CFStringCreateWithCString(nint.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == nint.Zero)
        {
            return 0;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == nint.Zero)
            {
                return 0;
            }

            if (CFGetTypeID(val) != CFNumberGetTypeID())
            {
                return 0;
            }

            long result = 0;
            CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
            return result;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    // CFStringをマネージド文字列に変換
    internal static unsafe string? CfStringToManaged(nint cfString)
    {
        var ptr = CFStringGetCStringPtr(cfString, kCFStringEncodingUTF8);
        if (ptr != nint.Zero)
        {
            return Marshal.PtrToStringUTF8(ptr);
        }

        var length = CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        // CFStringGetCStringPtrが失敗した場合のフォールバック
        var bufSize = (length * 4) + 1;
        var buf = stackalloc byte[(int)bufSize];
        return CFStringGetCString(cfString, buf, bufSize, kCFStringEncodingUTF8)
            ? Marshal.PtrToStringUTF8((nint)buf)
            : null;
    }
}

// NVMe SMARTセッション
// IOCreatePlugInInterfaceForServiceで取得したプラグインインターフェースを保持し、
// SMARTReadDataを繰り返し呼び出すことで最新のSMARTデータを取得する。
// IOCreatePlugInInterfaceForServiceは同一デバイスに対して1回しか成功しないため、
// セッションとしてインターフェースを保持する設計とする。
#pragma warning disable CA1806
#pragma warning disable SA1309
internal sealed class NvmeSmartSession
{
    // プラグインインターフェースハンドル (COM-like二重ポインタ)
    private readonly nint _pluginInterface;

    // NVMe SMARTインターフェースハンドル
    private readonly nint _smartInterface;

    private NvmeSmartSession(nint pluginInterface, nint smartInterface)
    {
        _pluginInterface = pluginInterface;
        _smartInterface = smartInterface;
    }

    // デバイスサービスからSMARTセッションを開く
    public static unsafe NvmeSmartSession? Open(uint service)
    {
        // NVMeSMARTLib plugin UUID (kIONVMeSMARTUserClientTypeID: AA0FA6F9-C2D6-457F-B10B-59A13253292F)
        var pluginTypeUuid = CFUUIDGetConstantUUIDWithBytes(
            nint.Zero, 0xAA, 0x0F, 0xA6, 0xF9, 0xC2, 0xD6, 0x45, 0x7F, 0xB1, 0x0B, 0x59, 0xA1, 0x32, 0x53, 0x29, 0x2F);

        // IOCFPlugInInterface UUID (C244E858-109C-11D4-91D4-0050E4C6426F)
        var cfPluginUuid = CFUUIDGetConstantUUIDWithBytes(
            nint.Zero, 0xC2, 0x44, 0xE8, 0x58, 0x10, 0x9C, 0x11, 0xD4, 0x91, 0xD4, 0x00, 0x50, 0xE4, 0xC6, 0x42, 0x6F);

        if (pluginTypeUuid == nint.Zero || cfPluginUuid == nint.Zero)
        {
            return null;
        }

        nint ppPlugin;
        int score;
        var kr = IOCreatePlugInInterfaceForService(
            service, pluginTypeUuid, cfPluginUuid, &ppPlugin, &score);
        if (kr != KERN_SUCCESS || ppPlugin == nint.Zero)
        {
            Console.Error.WriteLine($"[NVMe SMART] IOCreatePlugInInterfaceForService failed: 0x{kr:X8}");
            return null;
        }

        // QueryInterfaceでSMARTインターフェースを取得
        var vtable = *(nint*)ppPlugin;
        var qiFn = (delegate* unmanaged<nint, CFUUIDBytes, nint*, int>)(*((nint*)vtable + 1));

        // NVMe SMART Interface UUID (kIONVMeSMARTInterfaceID: CCD1DB19-FD9A-4DAF-BF95-12454B230AB6)
        var smartUuid = new CFUUIDBytes
        {
            byte0 = 0xCC, byte1 = 0xD1, byte2 = 0xDB, byte3 = 0x19,
            byte4 = 0xFD, byte5 = 0x9A, byte6 = 0x4D, byte7 = 0xAF,
            byte8 = 0xBF, byte9 = 0x95, byte10 = 0x12, byte11 = 0x45,
            byte12 = 0x4B, byte13 = 0x23, byte14 = 0x0A, byte15 = 0xB6,
        };

        nint smartInterface = nint.Zero;
        var hr = qiFn(ppPlugin, smartUuid, &smartInterface);
        if (hr != S_OK || smartInterface == nint.Zero)
        {
            Console.Error.WriteLine($"[NVMe SMART] QueryInterface failed: hr=0x{hr:X8}");
            ReleaseInterface(ppPlugin);
            return null;
        }

        return new NvmeSmartSession(ppPlugin, smartInterface);
    }

    // SMARTデータ読み取り (繰り返し呼び出し可能)
    public unsafe NvmeSmartLog? ReadData()
    {
        // NVMe SMART interface vtable layout (64-bit):
        //   0: _reserved, 8: QI, 16: AddRef, 24: Release
        //   32: version(2) + revision(2) + pad(4)
        //   40: SMARTReadData
        var smartVtable = *(nint*)_smartInterface;
        var readDataFn = (delegate* unmanaged<nint, byte*, int>)(*(nint*)((byte*)smartVtable + 40));

        var buffer = new byte[512];
        int kr;
        fixed (byte* bufPtr = buffer)
        {
            kr = readDataFn(_smartInterface, bufPtr);
        }

        if (kr != KERN_SUCCESS)
        {
            Console.Error.WriteLine($"[NVMe SMART] SMARTReadData failed: 0x{kr:X8}");
        }

        return kr == KERN_SUCCESS ? new NvmeSmartLog { RawData = buffer } : null;
    }

    // COM-like interfaceのRelease呼び出し
    internal static unsafe void ReleaseInterface(nint ppInterface)
    {
        if (ppInterface == nint.Zero)
        {
            return;
        }

        var vtable = *(nint*)ppInterface;
        var releaseFn = (delegate* unmanaged<nint, uint>)(*((nint*)vtable + 3));
        releaseFn(ppInterface);
    }
}

// ATA SMARTセッション
// IOCreatePlugInInterfaceForServiceで取得したプラグインインターフェースを保持し、
// SMARTReadDataを繰り返し呼び出すことで最新のSMARTデータを取得する。
#pragma warning disable CA1806
#pragma warning disable SA1309
internal sealed class AtaSmartSession
{
    // プラグインインターフェースハンドル
    private readonly nint _pluginInterface;

    // ATA SMARTインターフェースハンドル
    private readonly nint _smartInterface;

    private AtaSmartSession(nint pluginInterface, nint smartInterface)
    {
        _pluginInterface = pluginInterface;
        _smartInterface = smartInterface;
    }

    // デバイスサービスからSMARTセッションを開く
    public static unsafe AtaSmartSession? Open(uint service)
    {
        // ATASMARTLib plugin UUID (kIOATASMARTUserClientTypeID: 24514B7A-2804-11D6-8A02-003065704866)
        var pluginTypeUuid = CFUUIDGetConstantUUIDWithBytes(
            nint.Zero, 0x24, 0x51, 0x4B, 0x7A, 0x28, 0x04, 0x11, 0xD6, 0x8A, 0x02, 0x00, 0x30, 0x65, 0x70, 0x48, 0x66);

        // IOCFPlugInInterface UUID (C244E858-109C-11D4-91D4-0050E4C6426F)
        var cfPluginUuid = CFUUIDGetConstantUUIDWithBytes(
            nint.Zero, 0xC2, 0x44, 0xE8, 0x58, 0x10, 0x9C, 0x11, 0xD4, 0x91, 0xD4, 0x00, 0x50, 0xE4, 0xC6, 0x42, 0x6F);

        if (pluginTypeUuid == nint.Zero || cfPluginUuid == nint.Zero)
        {
            return null;
        }

        nint ppPlugin;
        int score;
        var kr = IOCreatePlugInInterfaceForService(
            service, pluginTypeUuid, cfPluginUuid, &ppPlugin, &score);
        if (kr != KERN_SUCCESS || ppPlugin == nint.Zero)
        {
            return null;
        }

        // QueryInterfaceでSMARTインターフェースを取得
        var vtable = *(nint*)ppPlugin;
        var qiFn = (delegate* unmanaged<nint, CFUUIDBytes, nint*, int>)(*((nint*)vtable + 1));

        // ATA SMART Interface UUID (kIOATASMARTInterfaceID: 08ABE21C-20D4-11D6-8DF6-0003935A76B2)
        var smartUuid = new CFUUIDBytes
        {
            byte0 = 0x08, byte1 = 0xAB, byte2 = 0xE2, byte3 = 0x1C,
            byte4 = 0x20, byte5 = 0xD4, byte6 = 0x11, byte7 = 0xD6,
            byte8 = 0x8D, byte9 = 0xF6, byte10 = 0x00, byte11 = 0x03,
            byte12 = 0x93, byte13 = 0x5A, byte14 = 0x76, byte15 = 0xB2,
        };

        nint smartInterface = nint.Zero;
        var hr = qiFn(ppPlugin, smartUuid, &smartInterface);
        if (hr != S_OK || smartInterface == nint.Zero)
        {
            NvmeSmartSession.ReleaseInterface(ppPlugin);
            return null;
        }

        // SMART操作を有効化
        var smartVtable = *(nint*)smartInterface;

        // ATA SMART interface vtable layout (64-bit):
        //   0: _reserved, 8: QI, 16: AddRef, 24: Release
        //   32: version(2) + revision(2) + pad(4)
        //   40: SMARTEnableDisableOperations
        //   48: SMARTEnableDisableAutosave
        //   56: SMARTReturnStatus
        //   64: SMARTExecuteOffLineImmediate
        //   72: SMARTReadData
        // Boolean(macOS) = unsigned char (1バイト)
        var enableFn = (delegate* unmanaged<nint, byte, int>)(*(nint*)((byte*)smartVtable + 40));
        kr = enableFn(smartInterface, 1);
        if (kr != KERN_SUCCESS)
        {
            NvmeSmartSession.ReleaseInterface(smartInterface);
            NvmeSmartSession.ReleaseInterface(ppPlugin);
            return null;
        }

        return new AtaSmartSession(ppPlugin, smartInterface);
    }

    // SMARTデータ読み取り (繰り返し呼び出し可能)
    public unsafe AtaSmartData? ReadData()
    {
        var smartVtable = *(nint*)_smartInterface;
        var readDataFn = (delegate* unmanaged<nint, byte*, int>)(*(nint*)((byte*)smartVtable + 72));

        var buffer = new byte[512];
        int kr;
        fixed (byte* bufPtr = buffer)
        {
            kr = readDataFn(_smartInterface, bufPtr);
        }

        return kr == KERN_SUCCESS ? new AtaSmartData { RawData = buffer } : null;
    }
}

// COM-like QueryInterfaceで使用するUUID構造体 (CoreFoundation CFUUIDBytes互換)
// フィールド名はAppleオリジナル定義に合わせる
#pragma warning disable SA1307
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct CFUUIDBytes
{
    public byte byte0;
    public byte byte1;
    public byte byte2;
    public byte byte3;
    public byte byte4;
    public byte byte5;
    public byte byte6;
    public byte byte7;
    public byte byte8;
    public byte byte9;
    public byte byte10;
    public byte byte11;
    public byte byte12;
    public byte byte13;
    public byte byte14;
    public byte byte15;
}
#pragma warning restore SA1307

// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CA1806
#pragma warning disable CA2101
#pragma warning disable CA5392
#pragma warning disable CS8981
internal static class NativeMethods
{
    // 成功コード (mach/kern_return.h)
    public const int KERN_SUCCESS = 0;

    // COM HRESULT成功コード
    public const int S_OK = 0;

    // CFString エンコーディング
    public const uint kCFStringEncodingUTF8 = 0x08000100;

    // CFNumber タイプ
    public const int kCFNumberSInt64Type = 4;

    // IORegistryエントリ検索オプション
    public const uint kIORegistryIterateRecursively = 0x00000001;

    // IOServiceプレーン名
    public const string kIOServicePlane = "IOService";

    //------------------------------------------------------------------------
    // IOKit
    //------------------------------------------------------------------------

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IOServiceMatching(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOServiceGetMatchingServices(
        uint mainPort, nint matching, ref nint existing);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOServiceGetMatchingService(
        uint mainPort, nint matching);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOIteratorNext(nint iterator);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOObjectRelease(nint @object);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOObjectRelease(uint @object);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IORegistryEntryCreateCFProperty(
        uint entry, nint key, nint allocator, uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IORegistryEntrySearchCFProperty(
        uint entry,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string plane,
        nint key,
        nint allocator,
        uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IORegistryEntryGetName(
        uint entry, byte* name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IORegistryEntryGetRegistryEntryID(
        uint entry, ulong* entryID);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IORegistryEntryIDMatching(ulong entryID);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IOCreatePlugInInterfaceForService(
        uint service,
        nint pluginType,
        nint interfaceType,
        nint* theInterface,
        int* theScore);

    //------------------------------------------------------------------------
    // CoreFoundation
    //------------------------------------------------------------------------

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFStringCreateWithCString(
        nint alloc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr,
        uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFStringGetCStringPtr(nint theString, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFStringGetLength(nint theString);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern unsafe bool CFStringGetCString(
        nint theString, byte* buffer, nint bufferSize, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(nint number, int theType, ref long valuePtr);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(nint boolean);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFGetTypeID(nint cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFStringGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFNumberGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFDictionaryGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFBooleanGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFDictionaryGetValue(nint theDict, nint key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern void CFRelease(nint cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFUUIDGetConstantUUIDWithBytes(
        nint alloc,
        byte byte0,
        byte byte1,
        byte byte2,
        byte byte3,
        byte byte4,
        byte byte5,
        byte byte6,
        byte byte7,
        byte byte8,
        byte byte9,
        byte byte10,
        byte byte11,
        byte byte12,
        byte byte13,
        byte byte14,
        byte byte15);
}
