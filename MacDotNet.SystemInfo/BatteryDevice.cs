namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public enum BatteryStatus
{
    Unknown = 0,
    Charging,
    Discharging,
    Full,
    NotCharging
}

public enum BatteryHealth
{
    Unknown = 0,
    Good,
    Fair,
    Poor,
    CheckBattery
}

public enum LowBatteryWarning
{
    Unknown = 0,
    None,
    Early,
    Final
}

public sealed class BatteryDevice
{
    public DateTime UpdateAt { get; private set; }

    public bool Supported { get; }

    // Identity

    public string? Manufacturer { get; }

    public string? DeviceName { get; }

    public string? SerialNumber { get; }

    // Design

    // mAh
    public int DesignCapacity { get; }

    public int DesignCycleCount { get; }

    // State

    // %
    public int Capacity { get; private set; }

    public BatteryStatus Status { get; private set; }

    public bool IsCharging { get; private set; }

    public bool IsCharged { get; private set; }

    public bool ExternalConnected { get; private set; }

    public bool ExternalChargeCapable { get; private set; }

    // Charge

    // mAh
    public int Charge { get; private set; }

    // mAh
    public int ChargeFull { get; private set; }

    public int CycleCount { get; private set; }

    // Electrical

    // mV
    public int Voltage { get; private set; }

    // mA (negative while discharging)
    public int Current { get; private set; }

    // C
    public double Temperature { get; private set; }

    // Time (minutes, -1 = calculating or unavailable)

    public int TimeToEmpty { get; private set; }

    public int TimeToFull { get; private set; }

    // Health

    public BatteryHealth Health { get; private set; }

    public string? HealthCondition { get; private set; }

    public bool AtWarnLevel { get; private set; }

    public bool AtCriticalLevel { get; private set; }

    public bool OptimizedChargingEngaged { get; private set; }

    public LowBatteryWarning WarningLevel { get; private set; }

    // Derived

    // W (negative while discharging)
    public double Power => Voltage * (double)Current / 1_000_000;

    // %
    public double HealthPercent => DesignCapacity > 0 ? ChargeFull * 100.0 / DesignCapacity : 0;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    // ReSharper disable StringLiteralTypo
    internal BatteryDevice()
    {
        using var service = new IOObj(IOServiceGetMatchingService(0, IOServiceMatching("AppleSmartBattery")));
        if (!service.IsValid)
        {
            return;
        }

        using var properties = CreateProperties(service);
        if (!properties.IsValid || (properties.ContainsKey("BatteryInstalled") && !properties.GetBoolean("BatteryInstalled")))
        {
            return;
        }

        Supported = true;

        Manufacturer = properties.GetString("Manufacturer");
        DeviceName = properties.GetString("DeviceName");
        SerialNumber = properties.GetString("Serial") ?? properties.GetString("BatterySerialNumber");
        DesignCapacity = (int)properties.GetInt64("DesignCapacity");
        DesignCycleCount = (int)properties.GetInt64("DesignCycleCount9C");

        UpdateCore(properties);
    }
    // ReSharper restore StringLiteralTypo

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        if (!Supported)
        {
            return false;
        }

        using var service = new IOObj(IOServiceGetMatchingService(0, IOServiceMatching("AppleSmartBattery")));
        if (!service.IsValid)
        {
            return false;
        }

        using var properties = CreateProperties(service);
        if (!properties.IsValid)
        {
            return false;
        }

        UpdateCore(properties);

        return true;
    }

    // ReSharper disable StringLiteralTypo
    private void UpdateCore(CFRef properties)
    {
        IsCharging = properties.GetBoolean("IsCharging");
        IsCharged = properties.GetBoolean("FullyCharged");
        ExternalConnected = properties.GetBoolean("ExternalConnected");
        ExternalChargeCapable = properties.GetBoolean("ExternalChargeCapable");
        AtWarnLevel = properties.GetBoolean("AtWarnLevel");
        AtCriticalLevel = properties.GetBoolean("AtCriticalLevel");
        CycleCount = (int)properties.GetInt64("CycleCount");
        Voltage = (int)properties.GetInt64("Voltage");
        Current = (int)properties.GetInt64("Amperage");
        Temperature = properties.TryGetInt64("Temperature", out var temperature) ? temperature / 100.0 : 0;

        // Apple silicon publishes percent in CurrentCapacity/MaxCapacity and mAh in the AppleRaw* keys
        Charge = properties.TryGetInt64("AppleRawCurrentCapacity", out var charge) || properties.TryGetInt64("CurrentCapacity", out charge)
            ? (int)charge
            : 0;
        ChargeFull = properties.TryGetInt64("AppleRawMaxCapacity", out var chargeFull) || properties.TryGetInt64("NominalChargeCapacity", out chargeFull) || properties.TryGetInt64("MaxCapacity", out chargeFull)
            ? (int)chargeFull
            : 0;

        // Defaults when power source information is unavailable
        var maxCapacity = properties.GetInt64("MaxCapacity");
        Capacity = maxCapacity > 0 ? (int)(properties.GetInt64("CurrentCapacity") * 100 / maxCapacity) : 0;
        TimeToEmpty = -1;
        TimeToFull = -1;
        Health = BatteryHealth.Unknown;
        HealthCondition = null;
        OptimizedChargingEngaged = false;

        ReadPowerSourceInfo();

        WarningLevel = IOPSGetBatteryWarningLevel() switch
        {
            kIOPSLowBatteryWarningNone => LowBatteryWarning.None,
            kIOPSLowBatteryWarningEarly => LowBatteryWarning.Early,
            kIOPSLowBatteryWarningFinal => LowBatteryWarning.Final,
            _ => LowBatteryWarning.Unknown
        };

        Status = !ExternalConnected
            ? BatteryStatus.Discharging
            : IsCharging
                ? BatteryStatus.Charging
                : IsCharged ? BatteryStatus.Full : BatteryStatus.NotCharging;

        UpdateAt = DateTime.Now;
    }

    private void ReadPowerSourceInfo()
    {
        using var blob = new CFRef(IOPSCopyPowerSourcesInfo());
        if (!blob.IsValid)
        {
            return;
        }

        using var list = new CFRef(IOPSCopyPowerSourcesList(blob));
        if (!list.IsValid)
        {
            return;
        }

        var count = CFArrayGetCount(list);
        for (var i = 0L; i < count; i++)
        {
            // The description dictionary is owned by the blob, retain it for the scoped release
            var descriptionRef = IOPSGetPowerSourceDescription(blob, CFArrayGetValueAtIndex(list, i));
            if (descriptionRef == IntPtr.Zero)
            {
                continue;
            }

            using var description = new CFRef(CFRetain(descriptionRef));
            if ((description.GetString("Type") != "InternalBattery") ||
                (description.ContainsKey("Is Present") && !description.GetBoolean("Is Present")))
            {
                continue;
            }

            var maxCapacity = description.GetInt64("Max Capacity");
            if (maxCapacity > 0)
            {
                Capacity = (int)(description.GetInt64("Current Capacity") * 100 / maxCapacity);
            }

            if (description.TryGetInt64("Time to Empty", out var timeToEmpty))
            {
                TimeToEmpty = (int)timeToEmpty;
            }

            if (description.TryGetInt64("Time to Full Charge", out var timeToFull))
            {
                TimeToFull = (int)timeToFull;
            }

            Health = ParseHealth(description.GetString("BatteryHealth"));
            HealthCondition = description.GetString("BatteryHealthCondition");
            OptimizedChargingEngaged = description.GetBoolean("Optimized Battery Charging Engaged");
            break;
        }
    }
    // ReSharper restore StringLiteralTypo

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static CFRef CreateProperties(IOObj service)
    {
        if ((IORegistryEntryCreateCFProperties(service, out var properties, IntPtr.Zero, 0) != KERN_SUCCESS) || (properties == IntPtr.Zero))
        {
            return CFRef.Zero;
        }

        return new CFRef(properties);
    }

    private static BatteryHealth ParseHealth(string? health) => health switch
    {
        "Good" => BatteryHealth.Good,
        "Fair" => BatteryHealth.Fair,
        "Poor" => BatteryHealth.Poor,
        "Check Battery" => BatteryHealth.CheckBattery,
        _ => BatteryHealth.Unknown
    };
}
