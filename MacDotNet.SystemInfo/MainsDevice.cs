namespace MacDotNet.SystemInfo;

using System.Globalization;

using static MacDotNet.SystemInfo.NativeMethods;

public enum PowerSourceType
{
    Unknown = 0,
    Ac,
    Battery,
    Ups
}

public enum TimeRemainingState
{
    Unknown = 0,
    Unlimited,
    Estimated
}

public sealed class MainsDevice : IDisposable
{
    // ReSharper disable StringLiteralTypo
    // Providing power source types (compared with CFEqual)
    private static readonly IntPtr AcPowerValue = CFSTR("AC Power");
    private static readonly IntPtr BatteryPowerValue = CFSTR("Battery Power");
    private static readonly IntPtr UpsPowerValue = CFSTR("UPS Power");

    // External power adapter details
    private static readonly IntPtr FamilyCodeKey = CFSTR("FamilyCode");
    private static readonly IntPtr WattsKey = CFSTR("Watts");
    private static readonly IntPtr VoltageKey = CFSTR("Voltage");
    private static readonly IntPtr AdapterVoltageKey = CFSTR("AdapterVoltage");
    private static readonly IntPtr CurrentKey = CFSTR("Current");
    private static readonly IntPtr AdapterIdKey = CFSTR("AdapterID");
    private static readonly IntPtr NameKey = CFSTR("Name");
    private static readonly IntPtr DescriptionKey = CFSTR("Description");
    private static readonly IntPtr ManufacturerKey = CFSTR("Manufacturer");
    private static readonly IntPtr SerialStringKey = CFSTR("SerialString");
    private static readonly IntPtr SerialNumberKey = CFSTR("SerialNumber");
    // ReSharper restore StringLiteralTypo

    private bool disposed;

    public DateTime UpdateAt { get; private set; }

    public bool Supported { get; }

    // Power source

    public bool Online { get; private set; }

    public PowerSourceType ProvidingSource { get; private set; }

    public TimeRemainingState TimeRemainingState { get; private set; }

    // Valid when TimeRemainingState is Estimated
    public TimeSpan TimeRemaining { get; private set; }

    // Adapter

    public bool AdapterConnected { get; private set; }

    // W
    public int AdapterWatts { get; private set; }

    // mV
    public int AdapterVoltage { get; private set; }

    // mA
    public int AdapterCurrent { get; private set; }

    public int AdapterId { get; private set; }

    public int AdapterFamily { get; private set; }

    public string? AdapterName { get; private set; }

    public string? AdapterDescription { get; private set; }

    public string? AdapterManufacturer { get; private set; }

    public string? AdapterSerialNumber { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private MainsDevice()
    {
        using var blob = new CFRef(IOPSCopyPowerSourcesInfo());
        Supported = blob.IsValid;

        Update();
    }

    internal static MainsDevice Create() => new();

    public void Dispose()
    {
        disposed = true;
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

        using var blob = new CFRef(IOPSCopyPowerSourcesInfo());
        if (!blob.IsValid)
        {
            return false;
        }

        // Returned string is a constant and must not be released
        ProvidingSource = ToPowerSourceType(IOPSGetProvidingPowerSourceType(blob));
        Online = ProvidingSource == PowerSourceType.Ac;

        var estimate = IOPSGetTimeRemainingEstimate();
        if (estimate >= 0)
        {
            TimeRemainingState = TimeRemainingState.Estimated;
            TimeRemaining = TimeSpan.FromSeconds(estimate);
        }
        else
        {
            TimeRemainingState = estimate <= kIOPSTimeRemainingUnlimited ? TimeRemainingState.Unlimited : TimeRemainingState.Unknown;
            TimeRemaining = TimeSpan.Zero;
        }

        ReadAdapterDetails();

        UpdateAt = DateTime.Now;

        return true;
    }

    private void ReadAdapterDetails()
    {
        using var adapter = new CFRef(IOPSCopyExternalPowerAdapterDetails());
        // Desktop Macs return only FamilyCode = kIOPSFamilyCodeDisconnected
        AdapterConnected = adapter.IsValid && (!adapter.TryGetInt64(FamilyCodeKey, out var family) || (family != kIOPSFamilyCodeDisconnected));
        if (!AdapterConnected)
        {
            AdapterWatts = 0;
            AdapterVoltage = 0;
            AdapterCurrent = 0;
            AdapterId = 0;
            AdapterFamily = 0;
            AdapterName = null;
            AdapterDescription = null;
            AdapterManufacturer = null;
            AdapterSerialNumber = null;
            return;
        }

        AdapterWatts = (int)adapter.GetInt64(WattsKey);
        AdapterVoltage = adapter.TryGetInt64(VoltageKey, out var voltage) || adapter.TryGetInt64(AdapterVoltageKey, out voltage) ? (int)voltage : 0;
        AdapterCurrent = (int)adapter.GetInt64(CurrentKey);
        AdapterId = (int)adapter.GetInt64(AdapterIdKey);
        AdapterFamily = (int)adapter.GetInt64(FamilyCodeKey);
        AdapterName = adapter.GetString(NameKey);
        AdapterDescription = adapter.GetString(DescriptionKey);
        AdapterManufacturer = adapter.GetString(ManufacturerKey);
        AdapterSerialNumber = adapter.GetString(SerialStringKey) ??
                              (adapter.TryGetInt64(SerialNumberKey, out var serialNumber) ? serialNumber.ToString(CultureInfo.InvariantCulture) : null);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static PowerSourceType ToPowerSourceType(IntPtr type)
    {
        if (type == IntPtr.Zero)
        {
            return PowerSourceType.Unknown;
        }

        if (CFEqual(type, AcPowerValue))
        {
            return PowerSourceType.Ac;
        }
        if (CFEqual(type, BatteryPowerValue))
        {
            return PowerSourceType.Battery;
        }
        if (CFEqual(type, UpsPowerValue))
        {
            return PowerSourceType.Ups;
        }

        return PowerSourceType.Unknown;
    }
}
