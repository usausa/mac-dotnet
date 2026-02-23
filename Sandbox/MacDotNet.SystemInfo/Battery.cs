namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// IOPowerSources API から取得したバッテリー情報。ユーザー表示向けのサマリ情報を提供する。
/// 詳細な診断情報 (電圧・電流・温度・サイクル数) は BatteryDetail または BatteryGeneric を使用する。
/// <para>
/// Battery information retrieved via the IOPowerSources API. Provides user-facing summary info.
/// For detailed diagnostics (voltage, current, temperature, cycle count) use BatteryDetail or BatteryGeneric.
/// </para>
/// </summary>
public sealed class Battery
{
    /// <summary>最後に Update() を呼び出した日時<br/>Timestamp of the most recent Update() call</summary>
    public DateTime UpdateAt { get; private set; }

    /// <summary>バッテリー情報の取得に成功したかどうか<br/>Whether battery information was successfully retrieved</summary>
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

    /// <summary>現在のバッテリー容量 (mAh)<br/>Current battery capacity (mAh)</summary>
    public int CurrentCapacity { get; private set; }

    /// <summary>現在の最大バッテリー容量 (mAh)<br/>Current maximum battery capacity (mAh)</summary>
    public int MaxCapacity { get; private set; }

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

    /// <summary>設計上のサイクル数上限。取得できない場合は -1<br/>Design cycle count limit. -1 if unavailable.</summary>
    public int DesignCycleCount { get; private set; } = -1;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal Battery()
    {
        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public unsafe bool Update()
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
                Name = GetStringValue(desc, kIOPSNameKey) ?? "Unknown";
                Type = GetStringValue(desc, kIOPSTypeKey) ?? "Unknown";
                TransportType = GetStringValue(desc, kIOPSTransportTypeKey);
                HardwareSerialNumber = GetStringValue(desc, kIOPSHardwareSerialNumberKey);
                IsPresent = GetBoolValue(desc, kIOPSIsPresentKey);
                PowerSourceState = GetStringValue(desc, kIOPSPowerSourceStateKey) ?? "Unknown";
                IsCharging = GetBoolValue(desc, kIOPSIsChargingKey);
                IsCharged = GetBoolValue(desc, kIOPSIsChargedKey);
                CurrentCapacity = GetIntValue(desc, kIOPSCurrentCapacityKey);
                MaxCapacity = GetIntValue(desc, kIOPSMaxCapacityKey);
                TimeToEmpty = GetIntValue(desc, kIOPSTimeToEmptyKey, -1);
                TimeToFullCharge = GetIntValue(desc, kIOPSTimeToFullChargeKey, -1);
                BatteryHealth = GetStringValue(desc, kIOPSBatteryHealthKey);
                BatteryHealthCondition = GetStringValue(desc, kIOPSBatteryHealthConditionKey);
                DesignCycleCount = GetIntValue(desc, kIOPSDesignCycleCountKey, -1);

                UpdateAt = DateTime.Now;

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

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static unsafe string? GetStringValue(IntPtr dict, string keyName)
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

    private static int GetIntValue(IntPtr dict, string keyName, int defaultValue = 0)
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

    private static bool GetBoolValue(IntPtr dict, string keyName)
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
}
