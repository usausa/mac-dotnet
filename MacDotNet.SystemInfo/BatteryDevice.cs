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

public sealed class BatteryDevice : IDisposable
{
    // ReSharper disable StringLiteralTypo
    // AppleSmartBattery properties
    private static readonly IntPtr IsChargingKey = CFSTR("IsCharging");
    private static readonly IntPtr FullyChargedKey = CFSTR("FullyCharged");
    private static readonly IntPtr ExternalConnectedKey = CFSTR("ExternalConnected");
    private static readonly IntPtr ExternalChargeCapableKey = CFSTR("ExternalChargeCapable");
    private static readonly IntPtr AtWarnLevelKey = CFSTR("AtWarnLevel");
    private static readonly IntPtr AtCriticalLevelKey = CFSTR("AtCriticalLevel");
    private static readonly IntPtr CycleCountKey = CFSTR("CycleCount");
    private static readonly IntPtr VoltageKey = CFSTR("Voltage");
    private static readonly IntPtr AmperageKey = CFSTR("Amperage");
    private static readonly IntPtr TemperatureKey = CFSTR("Temperature");
    private static readonly IntPtr AppleRawCurrentCapacityKey = CFSTR("AppleRawCurrentCapacity");
    private static readonly IntPtr CurrentCapacityKey = CFSTR("CurrentCapacity");
    private static readonly IntPtr AppleRawMaxCapacityKey = CFSTR("AppleRawMaxCapacity");
    private static readonly IntPtr NominalChargeCapacityKey = CFSTR("NominalChargeCapacity");
    private static readonly IntPtr MaxCapacityKey = CFSTR("MaxCapacity");

    // Power source description
    private static readonly IntPtr SourceTypeKey = CFSTR("Type");
    private static readonly IntPtr SourceIsPresentKey = CFSTR("Is Present");
    private static readonly IntPtr SourceMaxCapacityKey = CFSTR("Max Capacity");
    private static readonly IntPtr SourceCurrentCapacityKey = CFSTR("Current Capacity");
    private static readonly IntPtr SourceTimeToEmptyKey = CFSTR("Time to Empty");
    private static readonly IntPtr SourceTimeToFullKey = CFSTR("Time to Full Charge");
    private static readonly IntPtr SourceHealthKey = CFSTR("BatteryHealth");
    private static readonly IntPtr SourceHealthConditionKey = CFSTR("BatteryHealthCondition");
    private static readonly IntPtr SourceOptimizedChargingKey = CFSTR("Optimized Battery Charging Engaged");
    // ReSharper restore StringLiteralTypo

    // AppleSmartBattery service (held only when supported)
    private SafeIOObjectHandle? service;

    private bool disposed;

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
    private BatteryDevice()
    {
        var found = FindService();
        if (found == 0)
        {
            return;
        }

        service = new SafeIOObjectHandle(found);

        using var properties = new CFRef(CopyProperties(found));
        if (!properties.IsValid || (properties.ContainsKey("BatteryInstalled") && !properties.GetBoolean("BatteryInstalled")))
        {
            // Not supported: the service is not needed
            service.Dispose();
            service = null;
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

    internal static BatteryDevice Create() => new();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        service?.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!Supported)
        {
            return false;
        }

        using var properties = new CFRef(CopyServiceProperties());
        if (!properties.IsValid)
        {
            return false;
        }

        UpdateCore(properties);

        return true;
    }

    // Properties of the held service; on failure the service is looked up again and read once more
    private IntPtr CopyServiceProperties()
    {
        if (service is not null)
        {
            var properties = CopyProperties(service.Value);
            if (properties != IntPtr.Zero)
            {
                return properties;
            }
        }

        service?.Dispose();
        service = null;

        var found = FindService();
        if (found == 0)
        {
            return IntPtr.Zero;
        }

        service = new SafeIOObjectHandle(found);
        return CopyProperties(found);
    }

    private void UpdateCore(CFRef properties)
    {
        IsCharging = properties.GetBoolean(IsChargingKey);
        IsCharged = properties.GetBoolean(FullyChargedKey);
        ExternalConnected = properties.GetBoolean(ExternalConnectedKey);
        ExternalChargeCapable = properties.GetBoolean(ExternalChargeCapableKey);
        AtWarnLevel = properties.GetBoolean(AtWarnLevelKey);
        AtCriticalLevel = properties.GetBoolean(AtCriticalLevelKey);
        CycleCount = (int)properties.GetInt64(CycleCountKey);
        Voltage = (int)properties.GetInt64(VoltageKey);
        Current = (int)properties.GetInt64(AmperageKey);
        Temperature = properties.TryGetInt64(TemperatureKey, out var temperature) ? temperature / 100.0 : 0;

        // Apple silicon publishes percent in CurrentCapacity/MaxCapacity and mAh in the AppleRaw* keys
        Charge = properties.TryGetInt64(AppleRawCurrentCapacityKey, out var charge) || properties.TryGetInt64(CurrentCapacityKey, out charge)
            ? (int)charge
            : 0;
        ChargeFull = properties.TryGetInt64(AppleRawMaxCapacityKey, out var chargeFull) || properties.TryGetInt64(NominalChargeCapacityKey, out chargeFull) || properties.TryGetInt64(MaxCapacityKey, out chargeFull)
            ? (int)chargeFull
            : 0;

        // Defaults when power source information is unavailable
        var maxCapacity = properties.GetInt64(MaxCapacityKey);
        Capacity = maxCapacity > 0 ? (int)(properties.GetInt64(CurrentCapacityKey) * 100 / maxCapacity) : 0;
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
            if ((description.GetString(SourceTypeKey) != "InternalBattery") ||
                (description.ContainsKey(SourceIsPresentKey) && !description.GetBoolean(SourceIsPresentKey)))
            {
                continue;
            }

            var maxCapacity = description.GetInt64(SourceMaxCapacityKey);
            if (maxCapacity > 0)
            {
                Capacity = (int)(description.GetInt64(SourceCurrentCapacityKey) * 100 / maxCapacity);
            }

            if (description.TryGetInt64(SourceTimeToEmptyKey, out var timeToEmpty))
            {
                TimeToEmpty = (int)timeToEmpty;
            }

            if (description.TryGetInt64(SourceTimeToFullKey, out var timeToFull))
            {
                TimeToFull = (int)timeToFull;
            }

            Health = ParseHealth(description.GetString(SourceHealthKey));
            HealthCondition = description.GetString(SourceHealthConditionKey);
            OptimizedChargingEngaged = description.GetBoolean(SourceOptimizedChargingKey);
            break;
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // ReSharper disable once StringLiteralTypo
    private static uint FindService() => IOServiceGetMatchingService(0, IOServiceMatching("AppleSmartBattery"));

    // Owned property dictionary of the service (0 on failure)
    private static IntPtr CopyProperties(uint entry)
    {
        if ((IORegistryEntryCreateCFProperties(entry, out var properties, IntPtr.Zero, 0) != KERN_SUCCESS) || (properties == IntPtr.Zero))
        {
            return IntPtr.Zero;
        }

        return properties;
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
