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

public sealed class MainsDevice
{
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

    internal MainsDevice()
    {
        using var blob = new CFRef(IOPSCopyPowerSourcesInfo());
        Supported = blob.IsValid;

        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
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
        ProvidingSource = ToManagedString(IOPSGetProvidingPowerSourceType(blob)) switch
        {
            "AC Power" => PowerSourceType.Ac,
            "Battery Power" => PowerSourceType.Battery,
            "UPS Power" => PowerSourceType.Ups,
            _ => PowerSourceType.Unknown
        };
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

    // ReSharper disable StringLiteralTypo
    private void ReadAdapterDetails()
    {
        using var adapter = new CFRef(IOPSCopyExternalPowerAdapterDetails());
        AdapterConnected = adapter.IsValid;
        if (!adapter.IsValid)
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

        AdapterWatts = (int)adapter.GetInt64("Watts");
        AdapterVoltage = adapter.TryGetInt64("Voltage", out var voltage) || adapter.TryGetInt64("AdapterVoltage", out voltage) ? (int)voltage : 0;
        AdapterCurrent = (int)adapter.GetInt64("Current");
        AdapterId = (int)adapter.GetInt64("AdapterID");
        AdapterFamily = (int)adapter.GetInt64("FamilyCode");
        AdapterName = adapter.GetString("Name");
        AdapterDescription = adapter.GetString("Description");
        AdapterManufacturer = adapter.GetString("Manufacturer");
        AdapterSerialNumber = adapter.GetString("SerialString") ??
                              (adapter.TryGetInt64("SerialNumber", out var serialNumber) ? serialNumber.ToString(CultureInfo.InvariantCulture) : null);
    }
    // ReSharper restore StringLiteralTypo
}
