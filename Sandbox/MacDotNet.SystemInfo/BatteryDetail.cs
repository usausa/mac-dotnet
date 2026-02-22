namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class BatteryDetail
{
    /// <summary>バッテリー詳細情報の取得に成功したかどうか</summary>
    public bool Supported { get; private set; }

    /// <summary>現在のバッテリー電圧 (V)</summary>
    public double Voltage { get; private set; }

    /// <summary>現在の電流 (mA)。負の値は放電を示す</summary>
    public int Amperage { get; private set; }

    /// <summary>バッテリー温度 (°C)</summary>
    public double Temperature { get; private set; }

    /// <summary>充放電サイクル数</summary>
    public int CycleCount { get; private set; }

    /// <summary>現在のバッテリー容量 (mAh)。AppleRawCurrentCapacity</summary>
    public int CurrentCapacity { get; private set; }

    /// <summary>設計上のバッテリー容量 (mAh)</summary>
    public int DesignCapacity { get; private set; }

    /// <summary>現在の最大バッテリー容量 (mAh)。Apple Silicon では AppleRawMaxCapacity</summary>
    public int MaxCapacity { get; private set; }

    /// <summary>バッテリー健全性 (%)。MaxCapacity / DesignCapacity × 100</summary>
    public int Health { get; private set; }

    /// <summary>接続されている AC アダプタの定格電力 (W)。接続されていない場合は 0</summary>
    public int AcWatts { get; private set; }

    /// <summary>充電器が供給している電流 (mA)</summary>
    public int ChargingCurrent { get; private set; }

    /// <summary>充電器が供給している電圧 (mV)</summary>
    public int ChargingVoltage { get; private set; }

    /// <summary>最適化充電 (Optimized Battery Charging) が有効かどうか</summary>
    public bool OptimizedChargingEngaged { get; private set; }

    private readonly uint batteryService;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private BatteryDetail()
    {
        var matching = IOServiceMatching("AppleSmartBattery");
        if (matching != IntPtr.Zero)
        {
            batteryService = IOServiceGetMatchingService(0, matching);
            Supported = batteryService != 0;
        }

        if (Supported)
        {
            Update();
            if (DesignCapacity == 0)
            {
                Supported = false;
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static BatteryDetail Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        if (!Supported)
        {
            return false;
        }

        AcWatts = GetAcAdapterWatts();

        Voltage = GetPropertyDouble("Voltage") / 1000.0;
        Amperage = GetPropertyInt("Amperage");
        Temperature = GetPropertyDouble("Temperature") / 100.0;
        CycleCount = GetPropertyInt("CycleCount");
        CurrentCapacity = GetPropertyInt("AppleRawCurrentCapacity");
        DesignCapacity = GetPropertyInt("DesignCapacity");
        MaxCapacity = GetPropertyInt(IsArmMac() ? "AppleRawMaxCapacity" : "MaxCapacity");

        if (DesignCapacity > 0)
        {
            Health = (int)Math.Round(100.0 * MaxCapacity / DesignCapacity);
        }

        var chargerData = GetChargerData();
        if (chargerData is not null)
        {
            ChargingCurrent = chargerData.Value.current;
            ChargingVoltage = chargerData.Value.voltage;
        }

        OptimizedChargingEngaged = GetPropertyInt("OptimizedBatteryChargingEngaged") == 1;

        return true;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private int GetPropertyInt(string name)
    {
        var keyPtr = CFStringCreateWithCString(IntPtr.Zero, name, kCFStringEncodingUTF8);
        var valuePtr = IORegistryEntryCreateCFProperty(batteryService, keyPtr, IntPtr.Zero, 0);
        CFRelease(keyPtr);

        if (valuePtr == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            if (CFGetTypeID(valuePtr) == CFNumberGetTypeID())
            {
                if (CFNumberGetValue(valuePtr, kCFNumberSInt32Type, out var result))
                {
                    return result;
                }
            }
        }
        finally
        {
            CFRelease(valuePtr);
        }

        return 0;
    }

    private double GetPropertyDouble(string name)
    {
        var keyPtr = CFStringCreateWithCString(IntPtr.Zero, name, kCFStringEncodingUTF8);
        var valuePtr = IORegistryEntryCreateCFProperty(batteryService, keyPtr, IntPtr.Zero, 0);
        CFRelease(keyPtr);

        if (valuePtr == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            if (CFGetTypeID(valuePtr) == CFNumberGetTypeID())
            {
                var result = 0.0;
                if (CFNumberGetValue(valuePtr, kCFNumberFloat64Type, ref result))
                {
                    return result;
                }

                if (CFNumberGetValue(valuePtr, kCFNumberSInt32Type, out var intResult))
                {
                    return intResult;
                }
            }
        }
        finally
        {
            CFRelease(valuePtr);
        }

        return 0;
    }

    private (int current, int voltage)? GetChargerData()
    {
        var keyPtr = CFStringCreateWithCString(IntPtr.Zero, "ChargerData", kCFStringEncodingUTF8);
        var valuePtr = IORegistryEntryCreateCFProperty(batteryService, keyPtr, IntPtr.Zero, 0);
        CFRelease(keyPtr);

        if (valuePtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (CFGetTypeID(valuePtr) == CFDictionaryGetTypeID())
            {
                var currentKey = CFStringCreateWithCString(IntPtr.Zero, "ChargingCurrent", kCFStringEncodingUTF8);
                var currentPtr = CFDictionaryGetValue(valuePtr, currentKey);
                CFRelease(currentKey);

                var voltageKey = CFStringCreateWithCString(IntPtr.Zero, "ChargingVoltage", kCFStringEncodingUTF8);
                var voltagePtr = CFDictionaryGetValue(valuePtr, voltageKey);
                CFRelease(voltageKey);

                int current = 0, voltage = 0;
                if (currentPtr != IntPtr.Zero && CFNumberGetValue(currentPtr, kCFNumberSInt32Type, out var c))
                {
                    current = c;
                }

                if (voltagePtr != IntPtr.Zero && CFNumberGetValue(voltagePtr, kCFNumberSInt32Type, out var v))
                {
                    voltage = v;
                }

                return (current, voltage);
            }
        }
        finally
        {
            CFRelease(valuePtr);
        }

        return null;
    }

    private static int GetAcAdapterWatts()
    {
        var adapterDetails = IOPSCopyExternalPowerAdapterDetails();
        if (adapterDetails == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var wattsKey = CFStringCreateWithCString(IntPtr.Zero, kIOPSPowerAdapterWattsKey, kCFStringEncodingUTF8);
            var wattsPtr = CFDictionaryGetValue(adapterDetails, wattsKey);
            CFRelease(wattsKey);

            if (wattsPtr != IntPtr.Zero && CFNumberGetValue(wattsPtr, kCFNumberSInt32Type, out var watts))
            {
                return watts;
            }
        }
        finally
        {
            CFRelease(adapterDetails);
        }

        return 0;
    }

    private static bool IsArmMac() =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
        System.Runtime.InteropServices.Architecture.Arm64;
}
