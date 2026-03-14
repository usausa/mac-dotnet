namespace MacDotNet.SystemInfo;

using System.Buffers.Binary;

using static MacDotNet.SystemInfo.NativeMethods;

// Temperature

public sealed class TemperatureSensor
{
#pragma warning disable SA1401
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;
#pragma warning restore SA1401

    public string Key { get; }

    public string Description { get; }

    public string DataTypeString { get; }

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

// Voltage

public sealed class VoltageSensor
{
#pragma warning disable SA1401
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;
#pragma warning restore SA1401

    public string Key { get; }

    public string Description { get; }

    public string DataTypeString { get; }

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

// Current

public sealed class CurrentSensor
{
#pragma warning disable SA1401
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;
#pragma warning restore SA1401

    public string Key { get; }

    public string Description { get; }

    public string DataTypeString { get; }

    public double Value { get; internal set; }

    internal CurrentSensor(uint rawKey, uint dataType, uint dataSize, string key, string description, string dataTypeString)
    {
        RawKey = rawKey;
        DataType = dataType;
        DataSize = dataSize;
        Key = key;
        Description = description;
        DataTypeString = dataTypeString;
    }
}

// Power

public sealed class PowerSensor
{
#pragma warning disable SA1401
    internal readonly uint RawKey;
    internal readonly uint DataType;
    internal readonly uint DataSize;
#pragma warning restore SA1401

    public string Key { get; }

    public string Description { get; }

    public string DataTypeString { get; }

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

// Fan

public sealed class FanSensor
{
#pragma warning disable SA1401
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
#pragma warning restore SA1401

    public int Index { get; }

    public double ActualRpm { get; internal set; }

    public double MinRpm { get; internal set; }

    public double MaxRpm { get; internal set; }

    public double TargetRpm { get; internal set; }

#pragma warning disable SA1117
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
#pragma warning restore SA1117
}

// Monitor

public sealed class SmcMonitor
{
    private readonly PowerSensor? systemPowerSensor;

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<TemperatureSensor> Temperatures { get; }

    public IReadOnlyList<VoltageSensor> Voltages { get; }

    public IReadOnlyList<PowerSensor> Powers { get; }

    public IReadOnlyList<CurrentSensor> Currents { get; }

    public IReadOnlyList<FanSensor> Fans { get; }

    public double TotalSystemPower => systemPowerSensor?.Value ?? 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public SmcMonitor()
    {
        // TODO
        using var service = new IOObj(IOServiceGetMatchingService(0, IOServiceMatching("AppleSMC")));
        if (!service.IsValid || IOServiceOpen(service, task_self_trap(), 0, out var connHandle) != KERN_SUCCESS)
        {
            Temperatures = [];
            Voltages = [];
            Powers = [];
            Currents = [];
            Fans = [];
            UpdateAt = DateTime.Now;
            return;
        }

        using var conn = new IOService(connHandle);

        // センサー検出
        var temperatures = new List<TemperatureSensor>();
        var voltages = new List<VoltageSensor>();
        var powers = new List<PowerSensor>();
        var currents = new List<CurrentSensor>();

        var keyCount = GetKeyCount(conn);
        for (uint i = 0; i < (uint)keyCount; i++)
        {
            var key = SmcReadIndex(conn, i);
            if (key == 0)
            {
                continue;
            }

            var firstChar = (char)((key >> 24) & 0xFF);
            if (firstChar is not ('T' or 'V' or 'P' or 'I'))
            {
                continue;
            }

            if (!SmcReadKeyInfo(conn, key, out var dataSize, out var dataType) || dataSize == 0)
            {
                continue;
            }

            var value = ReadSensorValue(conn, key, dataType, dataSize);
            if (value is null)
            {
                continue;
            }

            var keyStr = UInt32ToKey(key);
            var dataTypeStr = UInt32ToKey(dataType);

            switch (firstChar)
            {
                case 'T':
                    temperatures.Add(new TemperatureSensor(key, dataType, dataSize, keyStr, GetTemperatureDescription(keyStr), dataTypeStr) { Value = value.Value });
                    break;
                case 'V':
                    voltages.Add(new VoltageSensor(key, dataType, dataSize, keyStr, GetVoltageDescription(keyStr), dataTypeStr) { Value = value.Value });
                    break;
                case 'P':
                    powers.Add(new PowerSensor(key, dataType, dataSize, keyStr, GetPowerDescription(keyStr), dataTypeStr) { Value = value.Value });
                    break;
                case 'I':
                    currents.Add(new CurrentSensor(key, dataType, dataSize, keyStr, GetCurrentDescription(keyStr), dataTypeStr) { Value = value.Value });
                    break;
            }
        }

        // ファン検出
        var fans = new List<FanSensor>();
        var fanCountVal = ReadSmcFloat(conn, "FNum");
        if (fanCountVal is not null)
        {
            for (var i = 0; i < (int)fanCountVal.Value; i++)
            {
                var ac = KeyToUInt32($"F{i}Ac");
                var mn = KeyToUInt32($"F{i}Mn");
                var mx = KeyToUInt32($"F{i}Mx");
                var tg = KeyToUInt32($"F{i}Tg");

                if (!SmcReadKeyInfo(conn, ac, out var acSize, out var acType) || acSize == 0)
                {
                    continue;
                }
                if (!SmcReadKeyInfo(conn, mn, out var mnSize, out var mnType) || mnSize == 0)
                {
                    continue;
                }
                if (!SmcReadKeyInfo(conn, mx, out var mxSize, out var mxType) || mxSize == 0)
                {
                    continue;
                }
                if (!SmcReadKeyInfo(conn, tg, out var tgSize, out var tgType) || tgSize == 0)
                {
                    continue;
                }

                var fan = new FanSensor(i, ac, acType, acSize, mn, mnType, mnSize, mx, mxType, mxSize, tg, tgType, tgSize);

                var actualVal = ReadSensorValue(conn, ac, acType, acSize);
                if (actualVal.HasValue)
                {
                    fan.ActualRpm = actualVal.Value;
                }
                var minVal = ReadSensorValue(conn, mn, mnType, mnSize);
                if (minVal.HasValue)
                {
                    fan.MinRpm = minVal.Value;
                }
                var maxVal = ReadSensorValue(conn, mx, mxType, mxSize);
                if (maxVal.HasValue)
                {
                    fan.MaxRpm = maxVal.Value;
                }
                var targetVal = ReadSensorValue(conn, tg, tgType, tgSize);
                if (targetVal.HasValue)
                {
                    fan.TargetRpm = targetVal.Value;
                }

                fans.Add(fan);
            }
        }

        Temperatures = temperatures;
        Voltages = voltages;
        Powers = powers;
        Currents = currents;
        Fans = fans;
        // ReSharper disable once StringLiteralTypo
        systemPowerSensor = powers.Find(static p => p.Key == "PSTR");

        UpdateAt = DateTime.Now;
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        using var service = new IOObj(IOServiceGetMatchingService(0, IOServiceMatching("AppleSMC")));
        if (!service.IsValid || IOServiceOpen(service, task_self_trap(), 0, out var connHandle) != KERN_SUCCESS)
        {
            return false;
        }

        using var conn = new IOService(connHandle);

        foreach (var sensor in Temperatures)
        {
            var value = ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var sensor in Voltages)
        {
            var value = ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var sensor in Powers)
        {
            var value = ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var sensor in Currents)
        {
            var value = ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize);
            if (value.HasValue)
            {
                sensor.Value = value.Value;
            }
        }

        foreach (var fan in Fans)
        {
            var actual = ReadSensorValue(conn, fan.KeyActual, fan.DataTypeActual, fan.DataSizeActual);
            if (actual.HasValue)
            {
                fan.ActualRpm = actual.Value;
            }

            var min = ReadSensorValue(conn, fan.KeyMin, fan.DataTypeMin, fan.DataSizeMin);
            if (min.HasValue)
            {
                fan.MinRpm = min.Value;
            }

            var max = ReadSensorValue(conn, fan.KeyMax, fan.DataTypeMax, fan.DataSizeMax);
            if (max.HasValue)
            {
                fan.MaxRpm = max.Value;
            }

            var target = ReadSensorValue(conn, fan.KeyTarget, fan.DataTypeTarget, fan.DataSizeTarget);
            if (target.HasValue)
            {
                fan.TargetRpm = target.Value;
            }
        }

        UpdateAt = DateTime.Now;
        return true;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static unsafe double? ReadSensorValue(uint conn, uint rawKey, uint dataType, uint dataSize)
    {
        // TODO style
        SMCKeyData_t input = default;
        SMCKeyData_t output = default;
        input.key = rawKey;
        input.keyInfo.dataSize = dataSize;
        input.data8 = SMC_CMD_READ_BYTES;

        return SmcCall(conn, &input, &output) == KERN_SUCCESS
            ? DecodeValue(output.bytes, dataType, dataSize)
            : null;
    }

    private static unsafe int GetKeyCount(uint conn)
    {
        SMCKeyData_t input = default;
        SMCKeyData_t output = default;

        // TODO optimize
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

        return BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(output.bytes, 4));
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

        // TODO reverse
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
        return IOConnectCallStructMethod(conn, KERNEL_INDEX_SMC, input, (nuint)sizeof(SMCKeyData_t), output, &outputSize);
    }

    private static unsafe double? DecodeValue(byte* bytes, uint dataType, uint dataSize)
    {
        if (dataType == DATA_TYPE_FLT && dataSize == 4)
        {
            // TODO?
            return *(float*)bytes;
        }
        if (dataType == DATA_TYPE_SP78 && dataSize == 2)
        {
            var raw = BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(bytes, 2));
            return raw / 256.0;
        }
        if (dataType == DATA_TYPE_FPE2 && dataSize == 2)
        {
            var raw = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(bytes, 2));
            return raw / 4.0;
        }
        if (dataType == DATA_TYPE_IOFT && dataSize == 8)
        {
            // TODO
            var raw = *(int*)bytes;
            return raw / 65536.0;
        }
        if (dataType == DATA_TYPE_UI8 && dataSize == 1)
        {
            return bytes[0];
        }
        if (dataType == DATA_TYPE_UI16 && dataSize == 2)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(bytes, 2));
        }
        if (dataType == DATA_TYPE_UI32 && dataSize == 4)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(bytes, 4));
        }
        return null;
    }

    // TODO why ?
    private static uint KeyToUInt32(string key) =>
        ((uint)key[0] << 24) | ((uint)key[1] << 16) | ((uint)key[2] << 8) | key[3];

    private static string UInt32ToKey(uint key) => new(
    [
        (char)((key >> 24) & 0xFF),
        (char)((key >> 16) & 0xFF),
        (char)((key >> 8) & 0xFF),
        (char)(key & 0xFF)
    ]);

    //--------------------------------------------------------------------------------
    // Description
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
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
        "TB1T" => "Battery 1",
        "TB2T" => "Battery 2",
        "TPCD" => "Platform Controller Hub Die",
        "TPMP" => "PMU",
        "TPSD" => "SSD",
        "TPSP" => "SoC Package",
        "TW0P" => "Airport",
        "TH0P" => "Thunderbolt Proximity",
        "TH0x" => "NAND",
        "TM0P" => "Memory Proximity",
        "TM1P" => "Memory Proximity 1",
        "TMVR" => "Memory VRM",
        "Tm0P" => "Mainboard",
        "TA0P" => "Ambient",
        "TaLP" => "Airflow Left",
        "TaRF" => "Airflow Right",
        "Ts0P" => "Palm Rest",
        "Ts1P" => "Palm Rest Right",
        _ => GetTemperatureDescriptionByPrefix(key)
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
            _ => key
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
        _ => GetPowerDescriptionByPrefix(key)
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
            _ => key
        };
    }

    private static string GetCurrentDescription(string key) => key switch
    {
        "IC0R" => "CPU High Side",
        "IG0R" => "GPU High Side",
        "ID0R" => "DC In",
        "IBAC" => "Battery",
        "IDBR" => "Brightness",
        "IU1R" => "Thunderbolt Left",
        "IU2R" => "Thunderbolt Right",
        _ => GetCurrentDescriptionByPrefix(key)
    };

    private static string GetCurrentDescriptionByPrefix(string key)
    {
        if (key.Length < 2)
        {
            return key;
        }

        return key[..2] switch
        {
            "IC" => $"CPU ({key})",
            "IG" => $"GPU ({key})",
            "ID" => $"DC ({key})",
            "IB" => $"Battery ({key})",
            "IU" => $"USB/Thunderbolt ({key})",
            _ => key
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
        _ => GetVoltageDescriptionByPrefix(key)
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
            _ => key
        };
    }
    // ReSharper restore StringLiteralTypo
}
