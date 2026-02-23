namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// IOPowerSources と IOKit/AppleSmartBattery の両方を直接参照し、
/// ユーザー表示向けのサマリ情報と診断監視向けの詳細情報を統合したバッテリークラス。
/// <para>
/// Unified battery class that combines data from both IOPowerSources and IOKit/AppleSmartBattery.
/// Provides both user-facing summary information and diagnostic detail in a single object.
/// When AppleSmartBattery is available (DetailSupported = true), precise capacity and health data
/// from the hardware register is used; otherwise IOPowerSources values serve as a fallback.
/// </para>
/// </summary>
public sealed class BatteryGeneric
{
    private readonly uint batteryService;

    //--------------------------------------------------------------------------------
    // Common / 共通
    //--------------------------------------------------------------------------------

    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    //--------------------------------------------------------------------------------
    // IOPowerSources - ユーザー表示向けサマリ情報 / User-facing summary info
    //--------------------------------------------------------------------------------

    /// <summary>IOPowerSources からバッテリー情報を取得できるかどうか<br/>Whether battery info is available from IOPowerSources</summary>
    public bool Supported { get; private set; }

    /// <summary>電源の名前。例: "InternalBattery-0"<br/>Power source name. Example: "InternalBattery-0"</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>電源の種類。例: "InternalBattery"<br/>Power source type. Example: "InternalBattery"</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>電源の接続方式。例: "Internal"。取得できない場合は null<br/>Transport type. Example: "Internal". Returns null if unavailable.</summary>
    public string? TransportType { get; private set; }

    /// <summary>バッテリーのハードウェアシリアル番号。取得できない場合は null<br/>Battery hardware serial number. Returns null if unavailable.</summary>
    public string? HardwareSerialNumber { get; private set; }

    /// <summary>バッテリーが物理的に接続されているかどうか<br/>Whether the battery is physically present</summary>
    public bool IsPresent { get; private set; }

    /// <summary>現在の電源状態。例: "AC Power"、"Battery Power"<br/>Current power source state. Example: "AC Power", "Battery Power"</summary>
    public string PowerSourceState { get; private set; } = string.Empty;

    /// <summary>AC 電源に接続されているかどうか<br/>Whether the system is connected to AC power</summary>
    public bool IsACConnected => string.Equals(PowerSourceState, kIOPSACPowerValue, StringComparison.Ordinal);

    /// <summary>充電中かどうか<br/>Whether the battery is currently charging</summary>
    public bool IsCharging { get; private set; }

    /// <summary>満充電かどうか<br/>Whether the battery is fully charged</summary>
    public bool IsCharged { get; private set; }

    /// <summary>バッテリー残量 (%)。CurrentCapacity / MaxCapacity × 100<br/>Battery level (%). CurrentCapacity / MaxCapacity × 100</summary>
    public int BatteryPercent => MaxCapacity > 0 ? (int)(100.0 * CurrentCapacity / MaxCapacity) : 0;

    /// <summary>放電完了までの推定時間 (分)。不明の場合は -1<br/>Estimated time to empty in minutes. -1 if unknown.</summary>
    public int TimeToEmpty { get; private set; } = -1;

    /// <summary>満充電までの推定時間 (分)。不明の場合は -1<br/>Estimated time to full charge in minutes. -1 if unknown.</summary>
    public int TimeToFullCharge { get; private set; } = -1;

    /// <summary>バッテリー健全性の評価。例: "Good"<br/>Battery health assessment. Example: "Good"</summary>
    public string? BatteryHealth { get; private set; }

    /// <summary>バッテリー健全性の詳細条件。例: "Check Battery"<br/>Battery health condition detail. Example: "Check Battery"</summary>
    public string? BatteryHealthCondition { get; private set; }

    /// <summary>設計上のサイクル数上限 (IOPowerSources 経由)。取得できない場合は -1<br/>Design cycle count limit from IOPowerSources. -1 if unavailable.</summary>
    public int DesignCycleCount { get; private set; } = -1;

    //--------------------------------------------------------------------------------
    // IOKit/AppleSmartBattery - 診断監視向け詳細情報 / Diagnostic detail info
    //--------------------------------------------------------------------------------

    /// <summary>IOKit/AppleSmartBattery から詳細情報を取得できるかどうか<br/>Whether detailed info is available from IOKit/AppleSmartBattery</summary>
    public bool DetailSupported { get; private set; }

    /// <summary>現在のバッテリー容量 (mAh)。DetailSupported の場合は AppleRawCurrentCapacity、それ以外は IOPowerSources の値<br/>Current battery capacity (mAh). AppleRawCurrentCapacity when DetailSupported; falls back to IOPowerSources.</summary>
    public int CurrentCapacity { get; private set; }

    /// <summary>現在の最大バッテリー容量 (mAh)。DetailSupported の場合は AppleRawMaxCapacity、それ以外は IOPowerSources の値<br/>Current maximum battery capacity (mAh). AppleRawMaxCapacity when DetailSupported; falls back to IOPowerSources.</summary>
    public int MaxCapacity { get; private set; }

    /// <summary>設計上のバッテリー容量 (mAh)。DetailSupported の場合のみ有効<br/>Design (rated) battery capacity (mAh). Valid only when DetailSupported is true.</summary>
    public int DesignCapacity { get; private set; }

    /// <summary>充放電サイクル数。DetailSupported の場合は AppleSmartBattery の値、それ以外は DesignCycleCount<br/>Charge/discharge cycle count. AppleSmartBattery value when DetailSupported; otherwise DesignCycleCount.</summary>
    public int CycleCount => DetailSupported ? rawCycleCount : DesignCycleCount;

    private int rawCycleCount;

    /// <summary>バッテリー健全性 (%)。MaxCapacity / DesignCapacity × 100。DetailSupported の場合のみ有効<br/>Battery health (%). MaxCapacity / DesignCapacity × 100. Valid only when DetailSupported is true.</summary>
    public int Health { get; private set; }

    /// <summary>現在のバッテリー電圧 (V)。DetailSupported の場合のみ有効<br/>Current battery voltage (V). Valid only when DetailSupported is true.</summary>
    public double Voltage { get; private set; }

    /// <summary>現在の電流 (mA)。負の値は放電を示す。DetailSupported の場合のみ有効<br/>Current in mA. Negative indicates discharging. Valid only when DetailSupported is true.</summary>
    public int Amperage { get; private set; }

    /// <summary>バッテリー温度 (°C)。DetailSupported の場合のみ有効<br/>Battery temperature (°C). Valid only when DetailSupported is true.</summary>
    public double Temperature { get; private set; }

    /// <summary>接続されている AC アダプタの定格電力 (W)。DetailSupported の場合のみ有効<br/>Rated wattage of the connected AC adapter (W). Valid only when DetailSupported is true.</summary>
    public int AcWatts { get; private set; }

    /// <summary>充電器が供給している電流 (mA)。DetailSupported の場合のみ有効<br/>Charging current supplied by the charger (mA). Valid only when DetailSupported is true.</summary>
    public int ChargingCurrent { get; private set; }

    /// <summary>充電器が供給している電圧 (mV)。DetailSupported の場合のみ有効<br/>Charging voltage supplied by the charger (mV). Valid only when DetailSupported is true.</summary>
    public int ChargingVoltage { get; private set; }

    /// <summary>最適化充電 (Optimized Battery Charging) が有効かどうか。DetailSupported の場合のみ有効<br/>Whether Optimized Battery Charging is engaged. Valid only when DetailSupported is true.</summary>
    public bool OptimizedChargingEngaged { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private BatteryGeneric()
    {
        var matching = IOServiceMatching("AppleSmartBattery");
        if (matching != IntPtr.Zero)
        {
            batteryService = IOServiceGetMatchingService(0, matching);
        }

        Update();

        if (DetailSupported && DesignCapacity == 0)
        {
            DetailSupported = false;
        }
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static BatteryGeneric Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        var r1 = UpdateFromPowerSources();
        var r2 = UpdateFromRegistry();
        if (r1 || r2)
        {
            UpdateAt = DateTime.Now;
        }

        return r1 || r2;
    }

    //--------------------------------------------------------------------------------
    // IOPowerSources
    //--------------------------------------------------------------------------------

    private unsafe bool UpdateFromPowerSources()
    {
        var blob = IOPSCopyPowerSourcesInfo();
        if (blob == IntPtr.Zero)
        {
            Supported = false;
            return false;
        }

        try
        {
            var sources = IOPSCopyPowerSourcesList(blob);
            if (sources == IntPtr.Zero)
            {
                Supported = false;
                return false;
            }

            try
            {
                var count = CFArrayGetCount(sources);
                if (count == 0)
                {
                    Supported = false;
                    return false;
                }

                var ps = CFArrayGetValueAtIndex(sources, 0);
                var desc = IOPSGetPowerSourceDescription(blob, ps);
                if (desc == IntPtr.Zero)
                {
                    Supported = false;
                    return false;
                }

                Supported = true;
                Name = PsGetString(desc, kIOPSNameKey) ?? "Unknown";
                Type = PsGetString(desc, kIOPSTypeKey) ?? "Unknown";
                TransportType = PsGetString(desc, kIOPSTransportTypeKey);
                HardwareSerialNumber = PsGetString(desc, kIOPSHardwareSerialNumberKey);
                IsPresent = PsGetBool(desc, kIOPSIsPresentKey);
                PowerSourceState = PsGetString(desc, kIOPSPowerSourceStateKey) ?? "Unknown";
                IsCharging = PsGetBool(desc, kIOPSIsChargingKey);
                IsCharged = PsGetBool(desc, kIOPSIsChargedKey);
                TimeToEmpty = PsGetInt(desc, kIOPSTimeToEmptyKey, -1);
                TimeToFullCharge = PsGetInt(desc, kIOPSTimeToFullChargeKey, -1);
                BatteryHealth = PsGetString(desc, kIOPSBatteryHealthKey);
                BatteryHealthCondition = PsGetString(desc, kIOPSBatteryHealthConditionKey);
                DesignCycleCount = PsGetInt(desc, kIOPSDesignCycleCountKey, -1);

                // Capacity is also read from IOPowerSources as fallback when DetailSupported is false
                if (!DetailSupported)
                {
                    CurrentCapacity = PsGetInt(desc, kIOPSCurrentCapacityKey);
                    MaxCapacity = PsGetInt(desc, kIOPSMaxCapacityKey);
                }

                return true;
            }
            finally
            {
                CFRelease(sources);
            }
        }
        finally
        {
            CFRelease(blob);
        }
    }

    private static unsafe string? PsGetString(IntPtr dict, string keyName)
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, keyName, kCFStringEncodingUTF8);
        if (key == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var value = CFDictionaryGetValue(dict, key);
            if (value == IntPtr.Zero)
            {
                return null;
            }

            var buffer = stackalloc byte[256];
            return CFStringGetCString(value, buffer, 256, kCFStringEncodingUTF8)
                ? Marshal.PtrToStringUTF8((IntPtr)buffer)
                : null;
        }
        finally
        {
            CFRelease(key);
        }
    }

    private static int PsGetInt(IntPtr dict, string keyName, int defaultValue = 0)
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, keyName, kCFStringEncodingUTF8);
        if (key == IntPtr.Zero)
        {
            return defaultValue;
        }

        try
        {
            var value = CFDictionaryGetValue(dict, key);
            if (value == IntPtr.Zero)
            {
                return defaultValue;
            }

            return CFNumberGetValue(value, kCFNumberSInt32Type, out var result) ? result : defaultValue;
        }
        finally
        {
            CFRelease(key);
        }
    }

    private static bool PsGetBool(IntPtr dict, string keyName)
    {
        var key = CFStringCreateWithCString(IntPtr.Zero, keyName, kCFStringEncodingUTF8);
        if (key == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var value = CFDictionaryGetValue(dict, key);
            return value != IntPtr.Zero && CFBooleanGetValue(value);
        }
        finally
        {
            CFRelease(key);
        }
    }

    //--------------------------------------------------------------------------------
    // IOKit/AppleSmartBattery
    //--------------------------------------------------------------------------------

    private bool UpdateFromRegistry()
    {
        if (batteryService == 0)
        {
            DetailSupported = false;
            return false;
        }

        AcWatts = GetAcAdapterWatts();

        Voltage = RegGetDouble("Voltage") / 1000.0;
        Amperage = RegGetInt("Amperage");
        Temperature = RegGetDouble("Temperature") / 100.0;
        rawCycleCount = RegGetInt("CycleCount");
        CurrentCapacity = RegGetInt("AppleRawCurrentCapacity");
        DesignCapacity = RegGetInt("DesignCapacity");
        MaxCapacity = RegGetInt(IsArmMac() ? "AppleRawMaxCapacity" : "MaxCapacity");

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

        OptimizedChargingEngaged = RegGetInt("OptimizedBatteryChargingEngaged") == 1;

        DetailSupported = true;
        return true;
    }

    private int RegGetInt(string name)
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

    private double RegGetDouble(string name)
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
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
}
