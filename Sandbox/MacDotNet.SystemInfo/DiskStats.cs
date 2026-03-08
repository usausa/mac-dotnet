namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class DiskDeviceStat
{
    internal bool Live { get; set; }

    internal ulong RegistryEntryId { get; }

    // Interface

    public string Name { get; }

    // Statistics

    public ulong BytesRead { get; internal set; }

    public ulong BytesWrite { get; internal set; }

    public ulong ReadsCompleted { get; internal set; }

    public ulong WritesCompleted { get; internal set; }

    public ulong TotalTimeRead { get; internal set; }

    public ulong TotalTimeWrite { get; internal set; }

    public ulong RetriesRead { get; internal set; }

    public ulong RetriesWrite { get; internal set; }

    public ulong ErrorsRead { get; internal set; }

    public ulong ErrorsWrite { get; internal set; }

    public ulong LatencyTimeRead { get; internal set; }

    public ulong LatencyTimeWrite { get; internal set; }

    // ---- 静的情報 (インスタンス作成時に一度だけ取得) ----

    public bool IsPhysical { get; }

    public bool IsRemovable { get; }

    public string? BusType { get; }

    public ulong DiskSize { get; }

    public string? MediaName { get; }

    public string? VendorName { get; }

    public string? MediumType { get; }

    internal DiskDeviceStat(ulong registryEntryId, string name, bool isPhysicalMedium, string? mediaName, string? vendorName, string? mediumType, bool isRemovable, ulong diskSize, string? busType)
    {
        // TODO 順序見直し
        RegistryEntryId = registryEntryId;
        Name = name;
        IsPhysical = isPhysicalMedium;
        IsRemovable = isRemovable;
        BusType = busType;
        DiskSize = diskSize;
        MediaName = mediaName;
        VendorName = vendorName;
        MediumType = mediumType;
    }
}

public sealed class DiskStats
{
    private readonly List<DiskDeviceStat> devices = new();

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<DiskDeviceStat> Devices => devices;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal DiskStats()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        foreach (var device in devices)
        {
            device.Live = false;
        }

        var iter = IntPtr.Zero;
        var kr = IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref iter);
        if (kr != KERN_SUCCESS || iter == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var added = false;
            uint entry;
            while ((entry = IOIteratorNext(iter)) != 0)
            {
                try
                {
                    var bsdName = GetBsdNameIfWhole(entry);
                    if (bsdName is null)
                    {
                        continue;
                    }

                    var parent = GetParentEntry(entry);
                    if (parent == 0)
                    {
                        continue;
                    }

                    try
                    {
                        if (IORegistryEntryGetRegistryEntryID(parent, out var entryId) != KERN_SUCCESS)
                        {
                            continue;
                        }

                        var device = default(DiskDeviceStat);
                        foreach (var item in devices)
                        {
                            if (item.RegistryEntryId == entryId)
                            {
                                device = item;
                                break;
                            }
                        }

                        if (device is null)
                        {
                            // TODO NetworkStats と同様にCreateEntryにまとめる、プロパティの取得はそれ単位でメソッドにする
                            var isPhysical = IsPhysical(parent);
                            var (isRemovable, mediaName, vendorName, mediumType, diskSize, busType) = ReadStaticDeviceInfo(entry);
                            device = new DiskDeviceStat(entryId, bsdName, isPhysical, mediaName, vendorName, mediumType, isRemovable, diskSize, busType);
                            devices.Add(device);
                            added = true;
                        }

                        device.Live = true;
                        ReadStatistics(parent, device);
                    }
                    finally
                    {
                        _ = IOObjectRelease(parent);
                    }
                }
                finally
                {
                    _ = IOObjectRelease(entry);
                }
            }

            for (var i = devices.Count - 1; i >= 0; i--)
            {
                if (!devices[i].Live)
                {
                    devices.RemoveAt(i);
                }
            }

            if (added)
            {
                devices.Sort(static (x, y) => StringComparer.Ordinal.Compare(x.Name, y.Name));
            }

            UpdateAt = DateTime.Now;
            return true;
        }
        finally
        {
            _ = IOObjectRelease(iter);
        }
    }

    private static void ReadStatistics(uint parentEntry, DiskDeviceStat device)
    {
        var statsDict = GetIokitDictionary(parentEntry, "Statistics");
        if (statsDict == IntPtr.Zero)
        {
            return;
        }

        try
        {
            device.BytesRead = GetIokitDictUInt64(statsDict, "Bytes (Read)");
            device.BytesWrite = GetIokitDictUInt64(statsDict, "Bytes (Write)");
            device.ReadsCompleted = GetIokitDictUInt64(statsDict, "Operations (Read)");
            device.WritesCompleted = GetIokitDictUInt64(statsDict, "Operations (Write)");
            device.TotalTimeRead = GetIokitDictUInt64(statsDict, "Total Time (Read)");
            device.TotalTimeWrite = GetIokitDictUInt64(statsDict, "Total Time (Write)");
            device.RetriesRead = GetIokitDictUInt64(statsDict, "Retries (Read)");
            device.RetriesWrite = GetIokitDictUInt64(statsDict, "Retries (Write)");
            device.ErrorsRead = GetIokitDictUInt64(statsDict, "Errors (Read)");
            device.ErrorsWrite = GetIokitDictUInt64(statsDict, "Errors (Write)");
            device.LatencyTimeRead = GetIokitDictUInt64(statsDict, "Latency Time (Read)");
            device.LatencyTimeWrite = GetIokitDictUInt64(statsDict, "Latency Time (Write)");
        }
        finally
        {
            CFRelease(statsDict);
        }
    }

    //--------------------------------------------------------------------------------
    // Private types / helpers
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOMedia エントリが Whole=true の場合に BSD 名を返す。
    /// Whole でない場合、または BSD 名が取得できない場合は null を返す。
    /// </summary>
    private static string? GetBsdNameIfWhole(uint mediaEntry)
    {
        var wholeKey = CFStringCreateWithCString(IntPtr.Zero, "Whole", kCFStringEncodingUTF8);
        if (wholeKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var wholeProp = IORegistryEntryCreateCFProperty(mediaEntry, wholeKey, IntPtr.Zero, 0);
            if (wholeProp == IntPtr.Zero)
            {
                return null;
            }

            var isWhole = CFBooleanGetValue(wholeProp);
            CFRelease(wholeProp);
            if (!isWhole)
            {
                return null;
            }
        }
        finally
        {
            CFRelease(wholeKey);
        }

        return GetIokitString(mediaEntry, "BSD Name");
    }

    /// <summary>
    /// IOMedia エントリの IOService プレーンにおける親エントリを返す。
    /// 取得失敗時は 0 を返す。戻り値が非 0 の場合、呼び出し元が IOObjectRelease しなければならない。
    /// </summary>
    private static uint GetParentEntry(uint mediaEntry)
    {
        return IORegistryEntryGetParentEntry(mediaEntry, "IOService", out var parent) == KERN_SUCCESS && parent != 0
            ? parent
            : 0;
    }

    /// <summary>
    /// 親エントリのクラスが IOBlockStorageDriver かどうかを返す。
    /// true の場合、物理ブロックストレージデバイス (NVMe、USB NVMe など)。
    /// false の場合、APFS コンテナ仮想ディスクなど合成された論理ディスク。
    /// </summary>
    private static bool IsPhysical(uint parentEntry)
    {
        return GetIokitClassName(parentEntry) == "IOBlockStorageDriver";
    }

    /// <summary>
    /// IOMedia エントリから静的なデバイス情報を読み取る。新規デバイス登録時に一度だけ呼ばれる。
    /// </summary>
    private static (bool IsRemovable, string? MediaName, string? VendorName, string? MediumType, ulong DiskSize, string? BusType) ReadStaticDeviceInfo(uint mediaEntry)
    {
        var isRemovable = GetIokitBoolean(mediaEntry, "Removable");
        var diskSize = GetIokitUInt64(mediaEntry, "Size");
        var (mediaName, vendorName, mediumType, busType) = FindDeviceCharacteristics(mediaEntry);
        return (isRemovable, mediaName, vendorName, mediumType, diskSize, busType);
    }

    /// <summary>
    /// IOKit エントリの祖先を IOService プレーンで上方向に辿り、
    /// "Device Characteristics" 辞書中の製品名・ベンダー名・メディア種別と
    /// "Protocol Characteristics" 辞書中のバス接続種別を返す。
    /// entry 自体は呼び出し元が所有するため release しない。
    /// </summary>
    private static (string? MediaName, string? VendorName, string? MediumType, string? BusType) FindDeviceCharacteristics(uint entry)
    {
        var current = entry;
        var shouldReleaseCurrent = false;

        string? mediaName = null, vendorName = null, mediumType = null, busType = null;

        for (var depth = 0; depth < 8; depth++)
        {
            if (IORegistryEntryGetParentEntry(current, "IOService", out var parent) != KERN_SUCCESS || parent == 0)
            {
                break;
            }

            if (shouldReleaseCurrent)
            {
                IOObjectRelease(current);
            }

            current = parent;
            shouldReleaseCurrent = true;

            if (mediaName is null)
            {
                var deviceCharacts = GetIokitDictionary(current, "Device Characteristics");
                if (deviceCharacts != IntPtr.Zero)
                {
                    try
                    {
                        mediaName = GetIokitDictString(deviceCharacts, "Product Name");
                        vendorName = GetIokitDictString(deviceCharacts, "Vendor Name");
                        mediumType = GetIokitDictString(deviceCharacts, "Medium Type");
                    }
                    finally
                    {
                        CFRelease(deviceCharacts);
                    }
                }
            }

            if (busType is null)
            {
                var protoCharacts = GetIokitDictionary(current, "Protocol Characteristics");
                if (protoCharacts != IntPtr.Zero)
                {
                    try
                    {
                        busType = GetIokitDictString(protoCharacts, "Physical Interconnect");
                    }
                    finally
                    {
                        CFRelease(protoCharacts);
                    }
                }
            }

            // 両方取得できたら早期終了
            if (mediaName is not null && busType is not null)
            {
                break;
            }
        }

        if (shouldReleaseCurrent)
        {
            IOObjectRelease(current);
        }

        return (mediaName, vendorName, mediumType, busType);
    }
}
