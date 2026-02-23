namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>温度センサー。HardwareMonitor.Update() で値が更新される。<br/>Temperature sensor. Value is updated by HardwareMonitor.Update().</summary>
public sealed class TemperatureSensor
{
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;

    /// <summary>SMC キー文字列。例: "TC0P"<br/>SMC key string. Example: "TC0P"</summary>
    public string Key { get; }

    /// <summary>センサーの説明。例: "CPU Proximity"<br/>Sensor description. Example: "CPU Proximity"</summary>
    public string Description { get; }

    /// <summary>SMC データ型文字列。例: "sp78"、"flt "<br/>SMC data type string. Example: "sp78", "flt "</summary>
    public string DataTypeString { get; }

    /// <summary>温度 (°C)<br/>Temperature in °C</summary>
    public double Value { get; internal set; }

    internal TemperatureSensor(uint rawKey, uint dataType, uint dataSize, string key, string description, string dataTypeString)
    {
        RawKey = rawKey;
        DataType = dataType;
        DataSize = dataSize;
        Key = key;
        Description = description;
        DataTypeString = dataTypeString;
    }
}

/// <summary>電圧センサー。HardwareMonitor.Update() で値が更新される。<br/>Voltage sensor. Value is updated by HardwareMonitor.Update().</summary>
public sealed class VoltageSensor
{
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;

    /// <summary>SMC キー文字列。例: "VD0R"<br/>SMC key string. Example: "VD0R"</summary>
    public string Key { get; }

    /// <summary>センサーの説明。例: "Main DC Input"<br/>Sensor description. Example: "Main DC Input"</summary>
    public string Description { get; }

    /// <summary>SMC データ型文字列<br/>SMC data type string</summary>
    public string DataTypeString { get; }

    /// <summary>電圧 (V)<br/>Voltage in V</summary>
    public double Value { get; internal set; }

    internal VoltageSensor(uint rawKey, uint dataType, uint dataSize, string key, string description, string dataTypeString)
    {
        RawKey = rawKey;
        DataType = dataType;
        DataSize = dataSize;
        Key = key;
        Description = description;
        DataTypeString = dataTypeString;
    }
}

/// <summary>電力センサー。HardwareMonitor.Update() で値が更新される。<br/>Power sensor. Value is updated by HardwareMonitor.Update().</summary>
public sealed class PowerSensor
{
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;

    /// <summary>SMC キー文字列。例: "PSTR"<br/>SMC key string. Example: "PSTR"</summary>
    public string Key { get; }

    /// <summary>センサーの説明。例: "Total System"<br/>Sensor description. Example: "Total System"</summary>
    public string Description { get; }

    /// <summary>SMC データ型文字列<br/>SMC data type string</summary>
    public string DataTypeString { get; }

    /// <summary>電力 (W)<br/>Power in W</summary>
    public double Value { get; internal set; }

    internal PowerSensor(uint rawKey, uint dataType, uint dataSize, string key, string description, string dataTypeString)
    {
        RawKey = rawKey;
        DataType = dataType;
        DataSize = dataSize;
        Key = key;
        Description = description;
        DataTypeString = dataTypeString;
    }
}

/// <summary>ファンセンサー。HardwareMonitor.Update() で値が更新される。<br/>Fan sensor. Values are updated by HardwareMonitor.Update().</summary>
public sealed class FanSensor
{
    /// <summary>ファンのインデックス (0 始まり)<br/>Fan index (0-based)</summary>
    public int Index { get; }

    /// <summary>現在の実際の回転数 (RPM)<br/>Current actual fan speed (RPM)</summary>
    public double ActualRpm { get; internal set; }

    /// <summary>最小回転数 (RPM)<br/>Minimum fan speed (RPM)</summary>
    public double MinRpm { get; internal set; }

    /// <summary>最大回転数 (RPM)<br/>Maximum fan speed (RPM)</summary>
    public double MaxRpm { get; internal set; }

    /// <summary>目標回転数 (RPM)<br/>Target fan speed (RPM)</summary>
    public double TargetRpm { get; internal set; }

    internal readonly uint KeyActual;
    internal readonly uint DataTypeActual;
    internal readonly uint DataSizeActual;
    internal readonly uint KeyMin;
    internal readonly uint DataTypeMin;
    internal readonly uint DataSizeMin;
    internal readonly uint KeyMax;
    internal readonly uint DataTypeMax;
    internal readonly uint DataSizeMax;
    internal readonly uint KeyTarget;
    internal readonly uint DataTypeTarget;
    internal readonly uint DataSizeTarget;

    internal FanSensor(
        int index,
        uint keyActual, uint dataTypeActual, uint dataSizeActual,
        uint keyMin, uint dataTypeMin, uint dataSizeMin,
        uint keyMax, uint dataTypeMax, uint dataSizeMax,
        uint keyTarget, uint dataTypeTarget, uint dataSizeTarget)
    {
        Index = index;
        KeyActual = keyActual;
        DataTypeActual = dataTypeActual;
        DataSizeActual = dataSizeActual;
        KeyMin = keyMin;
        DataTypeMin = dataTypeMin;
        DataSizeMin = dataSizeMin;
        KeyMax = keyMax;
        DataTypeMax = dataTypeMax;
        DataSizeMax = dataSizeMax;
        KeyTarget = keyTarget;
        DataTypeTarget = dataTypeTarget;
        DataSizeTarget = dataSizeTarget;
    }
}

/// <summary>
/// ハードウェアモニター。温度・電圧・電力・ファンの各センサー値を管理する。
/// <see cref="Create"/> でインスタンスを生成し、<see cref="Update"/> を呼ぶたびに最新値を更新する。
/// SMC 接続をインスタンスの生存期間中保持するため、使用後は <see cref="Dispose"/> を呼び出すこと。
/// <para>
/// Hardware monitor that manages temperature, voltage, power, and fan sensor values.
/// Create an instance via <see cref="Create"/> and call <see cref="Update"/> to refresh all values.
/// Holds an SMC connection for the lifetime of the instance; call <see cref="Dispose"/> when done.
/// </para>
/// </summary>
public sealed class HardwareMonitor : IDisposable
{
    private readonly uint _service;
    private readonly uint _conn;
    private readonly PowerSensor? _systemPowerSensor;
    private bool _disposed;

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>温度センサー一覧<br/>List of temperature sensors</summary>
    public IReadOnlyList<TemperatureSensor> Temperatures { get; }

    /// <summary>電圧センサー一覧<br/>List of voltage sensors</summary>
    public IReadOnlyList<VoltageSensor> Voltages { get; }

    /// <summary>電力センサー一覧<br/>List of power sensors</summary>
    public IReadOnlyList<PowerSensor> Powers { get; }

    /// <summary>ファンセンサー一覧<br/>List of fan sensors</summary>
    public IReadOnlyList<FanSensor> Fans { get; }

    /// <summary>システム総電力 (W)。PSTR キーが存在しない場合は null<br/>Total system power in watts. Null if the PSTR key is not present.</summary>
    public double? TotalSystemPower => _systemPowerSensor?.Value;

    //--------------------------------------------------------------------------------
    // Constructor / Factory
    //--------------------------------------------------------------------------------

    private HardwareMonitor(
        uint service,
        uint conn,
        List<TemperatureSensor> temperatures,
        List<VoltageSensor> voltages,
        List<PowerSensor> powers,
        List<FanSensor> fans)
    {
        _service = service;
        _conn = conn;
        Temperatures = temperatures;
        Voltages = voltages;
        Powers = powers;
        Fans = fans;
        _systemPowerSensor = powers.Find(static p => p.Key == "PSTR");
        UpdateAt = DateTime.Now;
    }

    /// <summary>
    /// SMC に接続してセンサーを検出し、HardwareMonitor インスタンスを生成する。
    /// AppleSMC サービスが見つからない場合は null を返す。
    /// </summary>
    public static HardwareMonitor? Create()
    {
        if (!TryOpenConnection(out var service, out var conn))
        {
            return null;
        }

        try
        {
            var (temperatures, voltages, powers) = DiscoverSensors(conn);
            var fans = DiscoverFans(conn);
            return new HardwareMonitor(service, conn, temperatures, voltages, powers, fans);
        }
        catch
        {
            IOServiceClose(conn);
            IOObjectRelease(service);
            return null;
        }
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// 全センサーの値を SMC から読み取り、各プロパティを更新する。
    /// 一度のメソッド呼び出しで温度・電圧・電力・ファンをまとめて更新する。
    /// </summary>
    public bool Update()
    {
        if (_disposed)
        {
            return false;
        }

        foreach (var sensor in Temperatures)
        {
            var value = ReadSensorValue(_conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var sensor in Voltages)
        {
            var value = ReadSensorValue(_conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var sensor in Powers)
        {
            var value = ReadSensorValue(_conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var fan in Fans)
        {
            var actual = ReadSensorValue(_conn, fan.KeyActual, fan.DataTypeActual, fan.DataSizeActual);
            if (actual.HasValue)
            {
                fan.ActualRpm = actual.Value;
            }

            var min = ReadSensorValue(_conn, fan.KeyMin, fan.DataTypeMin, fan.DataSizeMin);
            if (min.HasValue)
            {
                fan.MinRpm = min.Value;
            }

            var max = ReadSensorValue(_conn, fan.KeyMax, fan.DataTypeMax, fan.DataSizeMax);
            if (max.HasValue)
            {
                fan.MaxRpm = max.Value;
            }

            var target = ReadSensorValue(_conn, fan.KeyTarget, fan.DataTypeTarget, fan.DataSizeTarget);
            if (target.HasValue)
            {
                fan.TargetRpm = target.Value;
            }
        }

        UpdateAt = DateTime.Now;
        return true;
    }

    //--------------------------------------------------------------------------------
    // Dispose
    //--------------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IOServiceClose(_conn);
        IOObjectRelease(_service);
        GC.SuppressFinalize(this);
    }

    //--------------------------------------------------------------------------------
    // Internal one-off read (GpuDevice 等の単発読み取り用)
    //--------------------------------------------------------------------------------

    internal static int? ReadTemperatureOnce(string key)
    {
        if (!TryOpenConnection(out var service, out var conn))
        {
            return null;
        }

        try
        {
            var value = ReadSmcFloat(conn, key);
            if (value is not null && value.Value != 128)
            {
                return (int)value.Value;
            }

            return null;
        }
        finally
        {
            IOServiceClose(conn);
            IOObjectRelease(service);
        }
    }

    //--------------------------------------------------------------------------------
    // Discovery
    //--------------------------------------------------------------------------------

    private static unsafe (List<TemperatureSensor>, List<VoltageSensor>, List<PowerSensor>) DiscoverSensors(uint conn)
    {
        var temperatures = new List<TemperatureSensor>();
        var voltages = new List<VoltageSensor>();
        var powers = new List<PowerSensor>();

        var keyCount = GetKeyCount(conn);
        if (keyCount <= 0)
        {
            return (temperatures, voltages, powers);
        }

        for (uint i = 0; i < (uint)keyCount; i++)
        {
            var key = SmcReadIndex(conn, i);
            if (key == 0)
            {
                continue;
            }

            var firstChar = (char)((key >> 24) & 0xFF);
            if (firstChar is not ('T' or 'V' or 'P'))
            {
                continue;
            }

            if (!SmcReadKeyInfo(conn, key, out var dataSize, out var dataType) || dataSize == 0)
            {
                continue;
            }

            SMCKeyData_t input = default;
            SMCKeyData_t output = default;
            input.key = key;
            input.keyInfo.dataSize = dataSize;
            input.data8 = SMC_CMD_READ_BYTES;

            if (SmcCall(conn, &input, &output) != KERN_SUCCESS)
            {
                continue;
            }

            var value = DecodeValue(output.bytes, dataType, dataSize);
            if (value is null)
            {
                continue;
            }

            var keyStr = UInt32ToKey(key);
            var dataTypeStr = UInt32ToKey(dataType);

            switch (firstChar)
            {
                case 'T':
                    var tempSensor = new TemperatureSensor(key, dataType, dataSize, keyStr, GetTemperatureDescription(keyStr), dataTypeStr);
                    tempSensor.Value = value.Value;
                    temperatures.Add(tempSensor);
                    break;

                case 'V':
                    var voltSensor = new VoltageSensor(key, dataType, dataSize, keyStr, GetVoltageDescription(keyStr), dataTypeStr);
                    voltSensor.Value = value.Value;
                    voltages.Add(voltSensor);
                    break;

                case 'P':
                    var powerSensor = new PowerSensor(key, dataType, dataSize, keyStr, GetPowerDescription(keyStr), dataTypeStr);
                    powerSensor.Value = value.Value;
                    powers.Add(powerSensor);
                    break;
            }
        }

        return (temperatures, voltages, powers);
    }

    private static List<FanSensor> DiscoverFans(uint conn)
    {
        var fans = new List<FanSensor>();

        var fanCountVal = ReadSmcFloat(conn, "FNum");
        if (fanCountVal is null)
        {
            return fans;
        }

        var fanCount = (int)fanCountVal.Value;
        if (fanCount <= 0)
        {
            return fans;
        }

        for (var i = 0; i < fanCount; i++)
        {
            if (!TryGetKeyEntry(conn, $"F{i}Ac", out var actual) ||
                !TryGetKeyEntry(conn, $"F{i}Mn", out var min) ||
                !TryGetKeyEntry(conn, $"F{i}Mx", out var max) ||
                !TryGetKeyEntry(conn, $"F{i}Tg", out var target))
            {
                continue;
            }

            var fan = new FanSensor(
                i,
                actual.Key, actual.DataType, actual.DataSize,
                min.Key, min.DataType, min.DataSize,
                max.Key, max.DataType, max.DataSize,
                target.Key, target.DataType, target.DataSize);

            // 初期値を設定
            var actualVal = ReadSensorValue(conn, actual.Key, actual.DataType, actual.DataSize);
            if (actualVal.HasValue) fan.ActualRpm = actualVal.Value;

            var minVal = ReadSensorValue(conn, min.Key, min.DataType, min.DataSize);
            if (minVal.HasValue) fan.MinRpm = minVal.Value;

            var maxVal = ReadSensorValue(conn, max.Key, max.DataType, max.DataSize);
            if (maxVal.HasValue) fan.MaxRpm = maxVal.Value;

            var targetVal = ReadSensorValue(conn, target.Key, target.DataType, target.DataSize);
            if (targetVal.HasValue) fan.TargetRpm = targetVal.Value;

            fans.Add(fan);
        }

        return fans;
    }

    private static bool TryGetKeyEntry(uint conn, string keyStr, out (uint Key, uint DataType, uint DataSize) entry)
    {
        var rawKey = KeyToUInt32(keyStr);
        if (SmcReadKeyInfo(conn, rawKey, out var dataSize, out var dataType) && dataSize > 0)
        {
            entry = (rawKey, dataType, dataSize);
            return true;
        }

        entry = default;
        return false;
    }

    //--------------------------------------------------------------------------------
    // Low-level SMC helpers
    //--------------------------------------------------------------------------------

    private static unsafe double? ReadSensorValue(uint conn, uint rawKey, uint dataType, uint dataSize)
    {
        SMCKeyData_t input = default;
        SMCKeyData_t output = default;
        input.key = rawKey;
        input.keyInfo.dataSize = dataSize;
        input.data8 = SMC_CMD_READ_BYTES;

        return SmcCall(conn, &input, &output) == KERN_SUCCESS
            ? DecodeValue(output.bytes, dataType, dataSize)
            : null;
    }

    private static bool TryOpenConnection(out uint service, out uint conn)
    {
        service = IOServiceGetMatchingService(0, IOServiceMatching("AppleSMC"));
        if (service == 0)
        {
            conn = 0;
            return false;
        }

        var ret = IOServiceOpen(service, task_self_trap(), 0, out conn);
        if (ret != KERN_SUCCESS)
        {
            IOObjectRelease(service);
            conn = 0;
            return false;
        }

        return true;
    }

    private static unsafe int GetKeyCount(uint conn)
    {
        SMCKeyData_t input = default;
        SMCKeyData_t output = default;

        var key = KeyToUInt32("#KEY");
        if (!SmcReadKeyInfo(conn, key, out var dataSize, out _))
        {
            return 0;
        }

        input.key = key;
        input.keyInfo.dataSize = dataSize;
        input.data8 = SMC_CMD_READ_BYTES;

        if (SmcCall(conn, &input, &output) != KERN_SUCCESS)
        {
            return 0;
        }

        return (output.bytes[0] << 24) | (output.bytes[1] << 16) | (output.bytes[2] << 8) | output.bytes[3];
    }

    private static unsafe uint SmcReadIndex(uint conn, uint index)
    {
        SMCKeyData_t input = default;
        SMCKeyData_t output = default;
        input.data8 = SMC_CMD_READ_INDEX;
        input.data32 = index;
        return SmcCall(conn, &input, &output) == KERN_SUCCESS ? output.key : 0;
    }

    private static unsafe bool SmcReadKeyInfo(uint conn, uint key, out uint dataSize, out uint dataType)
    {
        SMCKeyData_t input = default;
        SMCKeyData_t output = default;
        input.key = key;
        input.data8 = SMC_CMD_READ_KEYINFO;

        if (SmcCall(conn, &input, &output) == KERN_SUCCESS)
        {
            dataSize = output.keyInfo.dataSize;
            dataType = output.keyInfo.dataType;
            return true;
        }

        dataSize = 0;
        dataType = 0;
        return false;
    }

    private static unsafe double? ReadSmcFloat(uint conn, string keyStr)
    {
        var key = KeyToUInt32(keyStr);
        if (!SmcReadKeyInfo(conn, key, out var dataSize, out var dataType) || dataSize == 0)
        {
            return null;
        }

        SMCKeyData_t input = default;
        SMCKeyData_t output = default;
        input.key = key;
        input.keyInfo.dataSize = dataSize;
        input.data8 = SMC_CMD_READ_BYTES;

        if (SmcCall(conn, &input, &output) != KERN_SUCCESS)
        {
            return null;
        }

        return DecodeValue(output.bytes, dataType, dataSize);
    }

    private static unsafe int SmcCall(uint conn, SMCKeyData_t* input, SMCKeyData_t* output)
    {
        var outputSize = (nuint)sizeof(SMCKeyData_t);
        return IOConnectCallStructMethod(
            conn,
            KERNEL_INDEX_SMC,
            input,
            (nuint)sizeof(SMCKeyData_t),
            output,
            &outputSize);
    }

    private static unsafe double? DecodeValue(byte* bytes, uint dataType, uint dataSize)
    {
        if (dataType == DATA_TYPE_FLT && dataSize == 4)
        {
            return *(float*)bytes;
        }

        if (dataType == DATA_TYPE_SP78 && dataSize == 2)
        {
            var raw = (short)((bytes[0] << 8) | bytes[1]);
            return raw / 256.0;
        }

        if (dataType == DATA_TYPE_FPE2 && dataSize == 2)
        {
            var raw = (ushort)((bytes[0] << 8) | bytes[1]);
            return raw / 4.0;
        }

        if (dataType == DATA_TYPE_IOFT && dataSize == 8)
        {
            var raw = *(int*)bytes;
            return raw / 65536.0;
        }

        if (dataType == DATA_TYPE_UI8 && dataSize == 1)
        {
            return bytes[0];
        }

        if (dataType == DATA_TYPE_UI16 && dataSize == 2)
        {
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        if (dataType == DATA_TYPE_UI32 && dataSize == 4)
        {
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        return null;
    }

    private static uint KeyToUInt32(string key) =>
        ((uint)key[0] << 24) | ((uint)key[1] << 16) | ((uint)key[2] << 8) | key[3];

    private static string UInt32ToKey(uint key) => new(
    [
        (char)((key >> 24) & 0xFF),
        (char)((key >> 16) & 0xFF),
        (char)((key >> 8) & 0xFF),
        (char)(key & 0xFF),
    ]);

    //--------------------------------------------------------------------------------
    // Description providers
    //--------------------------------------------------------------------------------

    private static string GetTemperatureDescription(string key) => key switch
    {
        "TC0P" => "CPU Proximity",
        "TC0E" => "CPU E-Cluster",
        "TC0F" => "CPU P-Cluster",
        "TCXC" => "CPU PECI/Die",
        "TCDX" => "CPU Die",
        "TCMb" => "CPU Die (average)",
        "TCMz" => "CPU Die (max)",
        "TG0P" => "GPU Proximity",
        "TGDD" => "GPU Die - Diode",
        "TG0D" => "GPU Die",
        "TG0T" => "GPU Transistor",
        "TB0T" => "Battery",
        "TPCD" => "Platform Controller Hub Die",
        "TPMP" => "PMU",
        "TPSD" => "SSD",
        "TPSP" => "SoC Package",
        "TW0P" => "WiFi Proximity",
        "TH0P" => "Thunderbolt Proximity",
        "TM0P" => "Memory Proximity",
        "TM1P" => "Memory Proximity 1",
        "TMVR" => "Memory VRM",
        "TA0P" => "Ambient",
        "Ts0P" => "Palm Rest",
        "Ts1P" => "Palm Rest Right",
        _ => GetTemperatureDescriptionByPrefix(key),
    };

    private static string GetTemperatureDescriptionByPrefix(string key)
    {
        if (key.Length < 2)
        {
            return key;
        }

        return key[..2] switch
        {
            "TP" => $"Die Sensor ({key})",
            "TR" => $"DRAM ({key})",
            "TS" or "Ts" => $"Surface ({key})",
            "TV" => $"Video ({key})",
            "TH" => $"Thunderbolt ({key})",
            "TI" => $"I/O ({key})",
            "Ta" => $"Ambient/SoC ({key})",
            "Te" => $"E-Cluster ({key})",
            "Tf" => $"Thermal Filter ({key})",
            _ => key,
        };
    }

    private static string GetPowerDescription(string key) => key switch
    {
        "PDTR" => "Total System",
        "PHPC" => "High Power Controller",
        "PHPM" => "High Power Module",
        "PHPS" => "High Power System",
        "PHPB" => "High Power Budget",
        "PPMR" => "Memory Rail",
        "PPSR" => "System Rail",
        _ => GetPowerDescriptionByPrefix(key),
    };

    private static string GetPowerDescriptionByPrefix(string key)
    {
        if (key.Length < 2)
        {
            return key;
        }

        return key[..2] switch
        {
            "PC" => $"CPU Power ({key})",
            "PH" => $"High Power ({key})",
            "PP" => $"Power Rail ({key})",
            "PR" => $"Power Rail ({key})",
            "PA" => $"Amplifier ({key})",
            "PF" => $"Fan Power ({key})",
            "PI" => $"I/O Power ({key})",
            "PM" => $"Memory Power ({key})",
            "PO" => $"Other Power ({key})",
            _ => key,
        };
    }

    private static string GetVoltageDescription(string key) => key switch
    {
        "V5SC" => "5V Supply",
        "VD0R" => "Main DC Input",
        "VDMA" => "DMA",
        "VDMM" => "Memory Controller",
        "VMVC" => "Main Voltage Controller",
        "VRTC" => "RTC Battery",
        _ => GetVoltageDescriptionByPrefix(key),
    };

    private static string GetVoltageDescriptionByPrefix(string key)
    {
        if (key.Length < 2)
        {
            return key;
        }

        return key[..2] switch
        {
            "VC" => $"CPU Core ({key})",
            "VD" => $"DC Rail ({key})",
            "VP" => $"Power Rail ({key})",
            "VR" => $"Regulator ({key})",
            _ => key,
        };
    }
}
