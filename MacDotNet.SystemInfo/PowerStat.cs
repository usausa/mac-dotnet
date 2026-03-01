namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class PowerStat
{
    public bool Supported { get; }

    // Cumulative CPU energy consumption (J)
    public double Cpu { get; private set; }

    // Cumulative GPU energy consumption (J)
    public double Gpu { get; private set; }

    // Cumulative ANE (Apple Neural Engine) energy consumption (J)
    public double Ane { get; private set; }

    // Cumulative RAM energy consumption (J)
    public double Ram { get; private set; }

    // Cumulative PCI energy consumption (J)
    public double Pci { get; private set; }

    public double Total => Cpu + Gpu + Ane + Ram + Pci;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    internal PowerStat()
    {
        Supported = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
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

        var channels = GetEnergyModelChannels();
        if (channels == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var subscription = IOReportCreateSubscription(IntPtr.Zero, channels, out var subDict, 0, IntPtr.Zero);
            if (subDict != IntPtr.Zero)
            {
                CFRelease(subDict);
            }

            if (subscription == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var samples = IOReportCreateSamples(subscription, channels, IntPtr.Zero);
                if (samples == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    return ParseSamples(samples);
                }
                finally
                {
                    CFRelease(samples);
                }
            }
            finally
            {
                CFRelease(subscription);
            }
        }
        finally
        {
            CFRelease(channels);
        }
    }

    //--------------------------------------------------------------------------------
    // Parse
    //--------------------------------------------------------------------------------

    private bool ParseSamples(IntPtr samples)
    {
        var channelsKey = CFStringCreateWithCString(IntPtr.Zero, "IOReportChannels", kCFStringEncodingUTF8);
        var channelsArray = CFDictionaryGetValue(samples, channelsKey);
        CFRelease(channelsKey);

        if ((channelsArray == IntPtr.Zero) || (CFGetTypeID(channelsArray) != CFArrayGetTypeID()))
        {
            return false;
        }

        var cpuEnergy = 0d;
        var gpuEnergy = 0d;
        var aneEnergy = 0d;
        var ramEnergy = 0d;
        var pciEnergy = 0d;

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
            if (String.IsNullOrEmpty(channelName))
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
            else if (channelName.StartsWith("PCI", StringComparison.Ordinal) && channelName.EndsWith("Energy", StringComparison.Ordinal))
            {
                pciEnergy = joules;
            }
        }

        Cpu = cpuEnergy;
        Gpu = gpuEnergy;
        Ane = aneEnergy;
        Ram = ramEnergy;
        Pci = pciEnergy;

        return true;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static IntPtr GetEnergyModelChannels()
    {
        try
        {
            var groupStr = CFStringCreateWithCString(IntPtr.Zero, "Energy Model", kCFStringEncodingUTF8);
            if (groupStr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                var channel = IOReportCopyChannelsInGroup(groupStr, IntPtr.Zero, 0, 0, 0);
                if (channel == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                try
                {
                    return CFDictionaryCreateMutableCopy(IntPtr.Zero, 0, channel);
                }
                finally
                {
                    CFRelease(channel);
                }
            }
            finally
            {
                CFRelease(groupStr);
            }
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
            _ => value / 1_000_000_000.0
        };
    }
}
