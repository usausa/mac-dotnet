namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class AppleSiliconPower
{
    private IntPtr channels;
    private IntPtr subscription;

    private double prevCpuEnergy;
    private double prevGpuEnergy;
    private double prevAneEnergy;
    private double prevRamEnergy;

    /// <summary>CPU が消費した電力 (W)。2 回以上 Update() を呼んだ後に有効</summary>
    public double CpuPower { get; private set; }

    /// <summary>GPU が消費した電力 (W)。2 回以上 Update() を呼んだ後に有効</summary>
    public double GpuPower { get; private set; }

    /// <summary>ANE (Apple Neural Engine) が消費した電力 (W)。2 回以上 Update() を呼んだ後に有効</summary>
    public double AnePower { get; private set; }

    /// <summary>RAM が消費した電力 (W)。2 回以上 Update() を呼んだ後に有効</summary>
    public double RamPower { get; private set; }

    /// <summary>CPU + GPU + ANE + RAM の合計消費電力 (W)</summary>
    public double TotalPower => CpuPower + GpuPower + AnePower + RamPower;

    /// <summary>Apple Silicon の IOReport エネルギーモニタリングが利用可能かどうか</summary>
    public bool Supported { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private AppleSiliconPower()
    {
        Supported = InitializeReporting();
        Update();
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static AppleSiliconPower Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        if (!Supported || subscription == IntPtr.Zero)
        {
            return false;
        }

        var samples = IOReportCreateSamples(subscription, channels, IntPtr.Zero);
        if (samples == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var channelsKey = CFStringCreateWithCString(IntPtr.Zero, "IOReportChannels", kCFStringEncodingUTF8);
            var channelsArray = CFDictionaryGetValue(samples, channelsKey);
            CFRelease(channelsKey);

            if (channelsArray == IntPtr.Zero || CFGetTypeID(channelsArray) != CFArrayGetTypeID())
            {
                return false;
            }

            double cpuEnergy = 0, gpuEnergy = 0, aneEnergy = 0, ramEnergy = 0;

            var count = CFArrayGetCount(channelsArray);
            for (var i = 0L; i < count; i++)
            {
                var item = CFArrayGetValueAtIndex(channelsArray, i);
                if (item == IntPtr.Zero)
                {
                    continue;
                }

                var groupPtr = IOReportChannelGetGroup(item);
                var group = groupPtr != IntPtr.Zero ? CfStringToManaged(groupPtr) : null;
                if (group != "Energy Model")
                {
                    continue;
                }

                var channelNamePtr = IOReportChannelGetChannelName(item);
                var channelName = channelNamePtr != IntPtr.Zero ? CfStringToManaged(channelNamePtr) : null;
                if (string.IsNullOrEmpty(channelName))
                {
                    continue;
                }

                var unitPtr = IOReportChannelGetUnitLabel(item);
                var unit = unitPtr != IntPtr.Zero ? CfStringToManaged(unitPtr) : null;

                var value = (double)IOReportSimpleGetIntegerValue(item, 0);
                var power = ConvertToPower(value, unit);

                if (channelName.EndsWith("CPU Energy", StringComparison.Ordinal))
                {
                    cpuEnergy = power;
                }
                else if (channelName.EndsWith("GPU Energy", StringComparison.Ordinal))
                {
                    gpuEnergy = power;
                }
                else if (channelName.StartsWith("ANE", StringComparison.Ordinal))
                {
                    aneEnergy = power;
                }
                else if (channelName.StartsWith("DRAM", StringComparison.Ordinal))
                {
                    ramEnergy = power;
                }
            }

            if (prevCpuEnergy > 0)
            {
                CpuPower = cpuEnergy - prevCpuEnergy;
                GpuPower = gpuEnergy - prevGpuEnergy;
                AnePower = aneEnergy - prevAneEnergy;
                RamPower = ramEnergy - prevRamEnergy;
            }

            prevCpuEnergy = cpuEnergy;
            prevGpuEnergy = gpuEnergy;
            prevAneEnergy = aneEnergy;
            prevRamEnergy = ramEnergy;

            return true;
        }
        finally
        {
            CFRelease(samples);
        }
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private bool InitializeReporting()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            return false;
        }

        channels = GetEnergyModelChannels();
        if (channels == IntPtr.Zero)
        {
            return false;
        }

        subscription = IOReportCreateSubscription(IntPtr.Zero, channels, out _, 0, IntPtr.Zero);
        return subscription != IntPtr.Zero;
    }

    private static IntPtr GetEnergyModelChannels()
    {
        try
        {
            var channel = IOReportCopyChannelsInGroup("Energy Model", null, 0, 0, 0);
            if (channel == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return CFDictionaryCreateMutableCopy(IntPtr.Zero, 1, channel);
        }
        catch (EntryPointNotFoundException)
        {
            return IntPtr.Zero;
        }
    }

    private static double ConvertToPower(double value, string? unit)
    {
        return unit switch
        {
            "mJ" => value / 1000.0,
            "uJ" => value / 1_000_000.0,
            "nJ" => value / 1_000_000_000.0,
            _ => value / 1_000_000_000.0,
        };
    }
}
