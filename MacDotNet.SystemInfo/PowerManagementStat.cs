namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

// NSProcessInfoThermalState
public enum ThermalState
{
    Unknown = -1,
    Nominal = 0,
    Fair = 1,
    Serious = 2,
    Critical = 3
}

public sealed class PowerManagementStat
{
    private static readonly Lazy<ProcessInfoContext> Context = new(CreateContext);

    public DateTime UpdateAt { get; private set; }

    // Thermal

    public bool ThermalStateSupported { get; }

    public ThermalState ThermalState { get; private set; }

    // kIOPMThermalLevel* value (0 = Normal, 100 = Danger, 10 = Crisis), -1 when not published
    public int ThermalWarningLevel { get; private set; }

    // Low power mode

    public bool LowPowerModeSupported { get; }

    public bool IsLowPowerModeEnabled { get; private set; }

    // CPU power limits (-1 when not published)

    // %
    public int CpuSpeedLimit { get; private set; }

    public int CpuAvailableCpus { get; private set; }

    // %
    public int CpuSchedulerLimit { get; private set; }

    // Sleep assertions

    public bool PreventUserIdleSystemSleep { get; private set; }

    public bool PreventUserIdleDisplaySleep { get; private set; }

    public bool PreventSystemSleep { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal PowerManagementStat()
    {
        var context = Context.Value;
        ThermalStateSupported = context.ThermalStateSelector != IntPtr.Zero;
        LowPowerModeSupported = context.LowPowerModeSelector != IntPtr.Zero;

        Update();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        var context = Context.Value;

        ThermalState = ThermalStateSupported
            ? ToThermalState(objc_msgSend(context.ProcessInfo, context.ThermalStateSelector))
            : ThermalState.Unknown;
        IsLowPowerModeEnabled = LowPowerModeSupported && objc_msgSend_bool(context.ProcessInfo, context.LowPowerModeSelector);

        ThermalWarningLevel = IOPMGetThermalWarningLevel(out var level) == kIOReturnSuccess ? (int)level : -1;

        ReadCpuPowerStatus();
        ReadAssertionsStatus();

        UpdateAt = DateTime.Now;

        return true;
    }

    private void ReadCpuPowerStatus()
    {
        if ((IOPMCopyCPUPowerStatus(out var statusRef) != kIOReturnSuccess) || (statusRef == IntPtr.Zero))
        {
            CpuSpeedLimit = -1;
            CpuAvailableCpus = -1;
            CpuSchedulerLimit = -1;
            return;
        }

        using var status = new CFRef(statusRef);
        CpuSpeedLimit = status.TryGetInt64("CPU_Speed_Limit", out var speedLimit) ? (int)speedLimit : -1;
        CpuAvailableCpus = status.TryGetInt64("CPU_Available_CPUs", out var availableCpus) ? (int)availableCpus : -1;
        CpuSchedulerLimit = status.TryGetInt64("CPU_Scheduler_Limit", out var schedulerLimit) ? (int)schedulerLimit : -1;
    }

    private void ReadAssertionsStatus()
    {
        if ((IOPMCopyAssertionsStatus(out var assertionsRef) != kIOReturnSuccess) || (assertionsRef == IntPtr.Zero))
        {
            PreventUserIdleSystemSleep = false;
            PreventUserIdleDisplaySleep = false;
            PreventSystemSleep = false;
            return;
        }

        using var assertions = new CFRef(assertionsRef);
        PreventUserIdleSystemSleep = assertions.GetInt64("PreventUserIdleSystemSleep") != 0;
        PreventUserIdleDisplaySleep = assertions.GetInt64("PreventUserIdleDisplaySleep") != 0;
        PreventSystemSleep = assertions.GetInt64("PreventSystemSleep") != 0;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static ThermalState ToThermalState(long value) => value switch
    {
        0 => ThermalState.Nominal,
        1 => ThermalState.Fair,
        2 => ThermalState.Serious,
        3 => ThermalState.Critical,
        _ => ThermalState.Unknown
    };

    private static ProcessInfoContext CreateContext()
    {
        try
        {
            if (!NativeLibrary.TryLoad(FoundationLib, out _))
            {
                return default;
            }

            var cls = objc_getClass("NSProcessInfo");
            if (cls == IntPtr.Zero)
            {
                return default;
            }

            var processInfo = objc_msgSend(cls, sel_registerName("processInfo"));
            if (processInfo == IntPtr.Zero)
            {
                return default;
            }

            var respondsToSelector = sel_registerName("respondsToSelector:");
            var thermalState = sel_registerName("thermalState");
            var lowPowerMode = sel_registerName("isLowPowerModeEnabled");

            return new ProcessInfoContext(
                processInfo,
                objc_msgSend_bool(processInfo, respondsToSelector, thermalState) ? thermalState : IntPtr.Zero,
                objc_msgSend_bool(processInfo, respondsToSelector, lowPowerMode) ? lowPowerMode : IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
            return default;
        }
        catch (EntryPointNotFoundException)
        {
            return default;
        }
    }

    private readonly record struct ProcessInfoContext(IntPtr ProcessInfo, IntPtr ThermalStateSelector, IntPtr LowPowerModeSelector);
}
