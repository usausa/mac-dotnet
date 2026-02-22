namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class AppleSiliconEnergyCounter
{
    private IntPtr channels;
    private IntPtr subscription;

    /// <summary>CPU の累積エネルギー消費量 (J)</summary>
    public double Cpu { get; private set; }

    /// <summary>GPU の累積エネルギー消費量 (J)</summary>
    public double Gpu { get; private set; }

    /// <summary>ANE (Apple Neural Engine) の累積エネルギー消費量 (J)</summary>
    public double Ane { get; private set; }

    /// <summary>RAM の累積エネルギー消費量 (J)</summary>
    public double Ram { get; private set; }

    /// <summary>CPU + GPU + ANE + RAM の累積エネルギー消費量合計 (J)</summary>
    public double Total => Cpu + Gpu + Ane + Ram;

    /// <summary>Apple Silicon の IOReport エネルギーモニタリングが利用可能かどうか</summary>
    public bool Supported { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private AppleSiliconEnergyCounter()
    {
        Supported = InitializeReporting();
        Update();
    }

    //--------------------------------------------------------------------------------
    // Factory
    //--------------------------------------------------------------------------------

    public static AppleSiliconEnergyCounter Create() => new();

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
                var joules = ConvertToJoules(value, unit);

                if (channelName.EndsWith("CPU Energy", StringComparison.Ordinal))
                {
                    cpuEnergy = joules;
                }
                else if (channelName.EndsWith("GPU Energy", StringComparison.Ordinal))
                {
                    gpuEnergy = joules;
                }
                else if (channelName.StartsWith("ANE", StringComparison.Ordinal))
                {
                    aneEnergy = joules;
                }
                else if (channelName.StartsWith("DRAM", StringComparison.Ordinal))
                {
                    ramEnergy = joules;
                }
            }

            Cpu = cpuEnergy;
            Gpu = gpuEnergy;
            Ane = aneEnergy;
            Ram = ramEnergy;

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

    private static double ConvertToJoules(double value, string? unit)
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
