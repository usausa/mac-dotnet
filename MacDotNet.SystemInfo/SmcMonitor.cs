namespace MacDotNet.SystemInfo;

using System.Buffers.Binary;

using static MacDotNet.SystemInfo.NativeMethods;

// Temperature

public sealed class TemperatureSensor
{
#pragma warning disable SA1401
    internal uint RawKey;
    internal uint DataType;
    internal uint DataSize;
#pragma warning restore SA1401

    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string DataTypeString { get; init; }

    public double Value { get; internal set; }
}

// Voltage

public sealed class VoltageSensor
{
#pragma warning disable SA1401
    internal uint RawKey;
    internal uint DataType;
    internal uint DataSize;
#pragma warning restore SA1401

    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string DataTypeString { get; init; }

    public double Value { get; internal set; }
}

// Current

public sealed class CurrentSensor
{
#pragma warning disable SA1401
    internal uint RawKey;
    internal uint DataType;
    internal uint DataSize;
#pragma warning restore SA1401

    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string DataTypeString { get; init; }

    public double Value { get; internal set; }
}

// Power

public sealed class PowerSensor
{
#pragma warning disable SA1401
    internal uint RawKey;
    internal uint DataType;
    internal uint DataSize;
#pragma warning restore SA1401

    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string DataTypeString { get; init; }

    public double Value { get; internal set; }
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

// Monitor

public sealed class SmcMonitor : IDisposable
{
    public const uint KeyNum = 0x234B4559u; // "#KEY"
    public const uint FNum = 0x464E756Du; // "FNum"

    // Fan key suffixes: F{i}Ac / F{i}Mn / F{i}Mx / F{i}Tg
    public const uint FanSuffixAc = 0x4163u;
    public const uint FanSuffixMn = 0x4D6Eu;
    public const uint FanSuffixMx = 0x4D78u;
    public const uint FanSuffixTg = 0x5467u;

    private readonly SmcConnection connection = new();

    private readonly List<TemperatureSensor> temperatures = [];

    private readonly List<VoltageSensor> voltages = [];

    private readonly List<PowerSensor> powers = [];

    private readonly List<CurrentSensor> currents = [];

    private readonly List<FanSensor> fans = [];

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public IReadOnlyList<TemperatureSensor> Temperatures => temperatures;

    public IReadOnlyList<VoltageSensor> Voltages => voltages;

    public IReadOnlyList<PowerSensor> Powers => powers;

    public IReadOnlyList<CurrentSensor> Currents => currents;

    public IReadOnlyList<FanSensor> Fans => fans;

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static uint FanKey(int index, uint suffix) => 0x4600_0000u | ((uint)('0' + index) << 16) | suffix;

    private static string ToKeyString(uint value) => new([(char)((value >> 24) & 0xFF), (char)((value >> 16) & 0xFF), (char)((value >> 8) & 0xFF), (char)(value & 0xFF)]);

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private SmcMonitor(Func<string, bool>? filter)
    {
        // When the connection cannot be opened, the sensor lists stay empty (Update tries to open it)
        if (connection.Open())
        {
            var conn = connection.Handle;

            _ = ReadSmcInt(conn, KeyNum, out var keyCount);
            for (var i = 0; i < keyCount; i++)
            {
                _ = SmcReadIndex(conn, i, out var key);
                if (key == 0)
                {
                    continue;
                }

                var firstChar = (char)((key >> 24) & 0xFF);
                if (firstChar is not ('T' or 'V' or 'P' or 'I'))
                {
                    continue;
                }

                // Keys not selected by the filter are neither added nor read
                var keyStr = ToKeyString(key);
                if ((filter is not null) && !filter(keyStr))
                {
                    continue;
                }

                if ((SmcReadKeyInfo(conn, key, out var dataSize, out var dataType) != KERN_SUCCESS) || (dataSize == 0))
                {
                    continue;
                }

                // A sensor whose first read fails is not added (create the monitor again to see it later)
                if (ReadSensorValue(conn, key, dataType, dataSize, out var value) != KERN_SUCCESS)
                {
                    continue;
                }

                var dataTypeStr = ToKeyString(dataType);
                switch (firstChar)
                {
                    case 'T':
                        temperatures.Add(new TemperatureSensor
                        {
                            RawKey = key,
                            DataType = dataType,
                            DataSize = dataSize,
                            Key = keyStr,
                            Description = GetTemperatureDescription(keyStr),
                            DataTypeString = dataTypeStr,
                            Value = value
                        });
                        break;
                    case 'V':
                        voltages.Add(new VoltageSensor
                        {
                            RawKey = key,
                            DataType = dataType,
                            DataSize = dataSize,
                            Key = keyStr,
                            Description = GetVoltageDescription(keyStr),
                            DataTypeString = dataTypeStr,
                            Value = value
                        });
                        break;
                    case 'P':
                        powers.Add(new PowerSensor
                        {
                            RawKey = key,
                            DataType = dataType,
                            DataSize = dataSize,
                            Key = keyStr,
                            Description = GetPowerDescription(keyStr),
                            DataTypeString = dataTypeStr,
                            Value = value
                        });
                        break;
                    case 'I':
                        currents.Add(new CurrentSensor
                        {
                            RawKey = key,
                            DataType = dataType,
                            DataSize = dataSize,
                            Key = keyStr,
                            Description = GetCurrentDescription(keyStr),
                            DataTypeString = dataTypeStr,
                            Value = value
                        });
                        break;
                }
            }

            _ = ReadSmcInt(conn, FNum, out var fanCount);
            for (var i = 0; i < fanCount; i++)
            {
                var ac = FanKey(i, FanSuffixAc);
                if ((SmcReadKeyInfo(conn, ac, out var acSize, out var acType) != KERN_SUCCESS) || (acSize == 0))
                {
                    continue;
                }
                var mn = FanKey(i, FanSuffixMn);
                if ((SmcReadKeyInfo(conn, mn, out var mnSize, out var mnType) != KERN_SUCCESS) || (mnSize == 0))
                {
                    continue;
                }
                var mx = FanKey(i, FanSuffixMx);
                if ((SmcReadKeyInfo(conn, mx, out var mxSize, out var mxType) != KERN_SUCCESS) || (mxSize == 0))
                {
                    continue;
                }
                var tg = FanKey(i, FanSuffixTg);
                if ((SmcReadKeyInfo(conn, tg, out var tgSize, out var tgType) != KERN_SUCCESS) || (tgSize == 0))
                {
                    continue;
                }

                // A fan is not added when the first read of any of its keys fails
                if ((ReadSensorValue(conn, ac, acType, acSize, out var actualRpm) != KERN_SUCCESS) ||
                    (ReadSensorValue(conn, mn, mnType, mnSize, out var minRpm) != KERN_SUCCESS) ||
                    (ReadSensorValue(conn, mx, mxType, mxSize, out var maxRpm) != KERN_SUCCESS) ||
                    (ReadSensorValue(conn, tg, tgType, tgSize, out var targetRpm) != KERN_SUCCESS))
                {
                    continue;
                }

                var fan = new FanSensor(i, ac, acType, acSize, mn, mnType, mnSize, mx, mxType, mxSize, tg, tgType, tgSize)
                {
                    ActualRpm = actualRpm,
                    MinRpm = minRpm,
                    MaxRpm = maxRpm,
                    TargetRpm = targetRpm
                };

                fans.Add(fan);
            }
        }

        UpdateAt = DateTime.Now;
    }

    internal static SmcMonitor Create(Func<string, bool>? filter) => new(filter);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        connection.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        // Not open (open failure at creation or after a failed retry): open once per call
        var opened = false;
        if (!connection.IsOpen)
        {
            if (!connection.Open())
            {
                return false;
            }

            opened = true;
        }

        if (!ReadSensors())
        {
            // Connection-level error (e.g. after sleep/wake): reopen and read all sensors again once
            if (opened || !connection.Open() || !ReadSensors())
            {
                connection.Close();
                return false;
            }
        }

        UpdateAt = DateTime.Now;

        return true;
    }

    // Returns false on a connection-level error; other errors set the value to 0 as before
    private bool ReadSensors()
    {
        var conn = connection.Handle;

        foreach (var sensor in temperatures)
        {
            if (IsConnectionError(ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize, out var value)))
            {
                return false;
            }

            sensor.Value = value;
        }
        foreach (var sensor in voltages)
        {
            if (IsConnectionError(ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize, out var value)))
            {
                return false;
            }

            sensor.Value = value;
        }
        foreach (var sensor in powers)
        {
            if (IsConnectionError(ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize, out var value)))
            {
                return false;
            }

            sensor.Value = value;
        }
        foreach (var sensor in currents)
        {
            if (IsConnectionError(ReadSensorValue(conn, sensor.RawKey, sensor.DataType, sensor.DataSize, out var value)))
            {
                return false;
            }

            sensor.Value = value;
        }
        foreach (var fan in fans)
        {
            if (IsConnectionError(ReadSensorValue(conn, fan.KeyActual, fan.DataTypeActual, fan.DataSizeActual, out var actualRpm)) ||
                IsConnectionError(ReadSensorValue(conn, fan.KeyMin, fan.DataTypeMin, fan.DataSizeMin, out var minRpm)) ||
                IsConnectionError(ReadSensorValue(conn, fan.KeyMax, fan.DataTypeMax, fan.DataSizeMax, out var maxRpm)) ||
                IsConnectionError(ReadSensorValue(conn, fan.KeyTarget, fan.DataTypeTarget, fan.DataSizeTarget, out var targetRpm)))
            {
                return false;
            }

            fan.ActualRpm = actualRpm;
            fan.MinRpm = minRpm;
            fan.MaxRpm = maxRpm;
            fan.TargetRpm = targetRpm;
        }

        return true;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // Errors that mean the connection itself is no longer usable.
    // The connection survives sleep and wake (M0-3); these cover a connection lost in other ways, such as a driver restart.
    private static bool IsConnectionError(int kr) => kr is MACH_SEND_INVALID_DEST or kIOReturnNotOpen or kIOReturnNoDevice;

    // The SMC helpers return the kern_return of the call (output values are 0 on failure)

    private static unsafe int SmcReadIndex(uint conn, int index, out uint key)
    {
        var input = default(SMCKeyData_t);
        var output = default(SMCKeyData_t);
        input.data8 = SMC_CMD_READ_INDEX;
        input.data32 = (uint)index;

        var kr = SmcCall(conn, &input, &output);
        key = kr == KERN_SUCCESS ? output.key : 0;
        return kr;
    }

    private static unsafe int ReadSmcInt(uint conn, uint key, out int value)
    {
        value = 0;

        var kr = SmcReadKeyInfo(conn, key, out var dataSize, out _);
        if ((kr != KERN_SUCCESS) || (dataSize == 0))
        {
            return kr;
        }

        var input = default(SMCKeyData_t);
        var output = default(SMCKeyData_t);
        input.key = key;
        input.keyInfo.dataSize = dataSize;
        input.data8 = SMC_CMD_READ_BYTES;

        kr = SmcCall(conn, &input, &output);
        if (kr != KERN_SUCCESS)
        {
            return kr;
        }

        value = dataSize switch
        {
            1 => output.bytes[0],
            2 => BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(output.bytes, 2)),
            4 => (int)BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(output.bytes, 4)),
            _ => 0
        };
        return kr;
    }

    private static unsafe int SmcReadKeyInfo(uint conn, uint key, out uint dataSize, out uint dataType)
    {
        var input = default(SMCKeyData_t);
        var output = default(SMCKeyData_t);
        input.key = key;
        input.data8 = SMC_CMD_READ_KEYINFO;

        var kr = SmcCall(conn, &input, &output);
        if (kr == KERN_SUCCESS)
        {
            dataSize = output.keyInfo.dataSize;
            dataType = output.keyInfo.dataType;
        }
        else
        {
            dataSize = 0;
            dataType = 0;
        }

        return kr;
    }

    private static unsafe int ReadSensorValue(uint conn, uint rawKey, uint dataType, uint dataSize, out double value)
    {
        var input = default(SMCKeyData_t);
        var output = default(SMCKeyData_t);
        input.key = rawKey;
        input.keyInfo.dataSize = dataSize;
        input.data8 = SMC_CMD_READ_BYTES;

        var kr = SmcCall(conn, &input, &output);
        value = kr == KERN_SUCCESS ? DecodeValue(output.bytes, dataType, dataSize) : 0;
        return kr;
    }

    private static unsafe double DecodeValue(byte* bytes, uint dataType, uint dataSize)
    {
        if ((dataType == DATA_TYPE_FLT) && (dataSize == 4))
        {
            return BinaryPrimitives.ReadSingleLittleEndian(new ReadOnlySpan<byte>(bytes, 4));
        }
        if ((dataType == DATA_TYPE_SP78) && (dataSize == 2))
        {
            return BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(bytes, 2)) / 256.0;
        }
        if ((dataType == DATA_TYPE_FPE2) && (dataSize == 2))
        {
            return BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(bytes, 2)) / 4.0;
        }
        if ((dataType == DATA_TYPE_IOFT) && (dataSize == 8))
        {
            // IOFT is a 48.16 fixed-point value; byte order matches the other little-endian native reads on Apple hardware
            return BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(bytes, 8)) / 65536.0;
        }
        if ((dataType == DATA_TYPE_UI8) && (dataSize == 1))
        {
            return bytes[0];
        }
        if ((dataType == DATA_TYPE_UI16) && (dataSize == 2))
        {
            return BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(bytes, 2));
        }
        if ((dataType == DATA_TYPE_UI32) && (dataSize == 4))
        {
            return BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(bytes, 4));
        }
        return 0;
    }

    private static unsafe int SmcCall(uint conn, SMCKeyData_t* input, SMCKeyData_t* output)
    {
        var outputSize = (UIntPtr)sizeof(SMCKeyData_t);
        return IOConnectCallStructMethod(conn, KERNEL_INDEX_SMC, input, (UIntPtr)sizeof(SMCKeyData_t), output, &outputSize);
    }

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
        var k => GetTemperatureDescriptionByPrefix(k)
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
        var k => GetPowerDescriptionByPrefix(k)
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
        var k => GetCurrentDescriptionByPrefix(k)
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
        var k => GetVoltageDescriptionByPrefix(k)
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
