namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// Apple Silicon 専用の IOReport エネルギーカウンター。
/// CPU・GPU・ANE・RAM の累積エネルギー消費量をジュール単位で提供する。
/// ARM64 以外の環境では Supported = false となり Update() は常に false を返す。
/// <para>
/// Apple Silicon-specific IOReport energy counter.
/// Reports cumulative energy consumption in joules for CPU, GPU, ANE, and RAM.
/// On non-ARM64 platforms, Supported = false and Update() always returns false.
/// </para>
/// </summary>
public sealed class AppleSiliconEnergyCounter
{
    private IntPtr channels;
    private IntPtr subscription;

    /// <summary>CPU の累積エネルギー消費量 (J)<br/>Cumulative CPU energy consumption (J)</summary>
    public double Cpu { get; private set; }

    /// <summary>GPU の累積エネルギー消費量 (J)<br/>Cumulative GPU energy consumption (J)</summary>
    public double Gpu { get; private set; }

    /// <summary>ANE (Apple Neural Engine) の累積エネルギー消費量 (J)<br/>Cumulative ANE (Apple Neural Engine) energy consumption (J)</summary>
    public double Ane { get; private set; }

    /// <summary>RAM の累積エネルギー消費量 (J)<br/>Cumulative RAM energy consumption (J)</summary>
    public double Ram { get; private set; }

    /// <summary>PCI の累積エネルギー消費量 (J)<br/>Cumulative PCI energy consumption (J)</summary>
    public double Pci { get; private set; }

    /// <summary>CPU + GPU + ANE + RAM + PCI の累積エネルギー消費量合計 (J)<br/>Total cumulative energy consumption (CPU + GPU + ANE + RAM + PCI) in joules</summary>
    public double Total => Cpu + Gpu + Ane + Ram + Pci;

    /// <summary>Apple Silicon の IOReport エネルギーモニタリングが利用可能かどうか<br/>Whether IOReport energy monitoring is available (Apple Silicon / ARM64 only)</summary>
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

    /// <summary>
    /// AppleSiliconEnergyCounter インスタンスを生成する。
    /// ARM64 以外または IOReport が利用不可な場合は Supported = false のインスタンスを返す。
    /// <para>
    /// Creates an AppleSiliconEnergyCounter instance.
    /// Returns an instance with Supported = false on non-ARM64 or when IOReport is unavailable.
    /// </para>
    /// </summary>
    public static AppleSiliconEnergyCounter Create() => new();

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    /// <summary>
    /// IOReport からエネルギーサンプルを取得して各プロパティを更新する。
    /// 成功時は true、Supported が false またはサンプル取得失敗時は false を返す。
    /// <para>
    /// Fetches an energy sample from IOReport and updates each property.
    /// Returns true on success, false when Supported is false or sampling fails.
    /// </para>
    /// </summary>
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

            double cpuEnergy = 0, gpuEnergy = 0, aneEnergy = 0, ramEnergy = 0, pciEnergy = 0;

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

        subscription = IOReportCreateSubscription(IntPtr.Zero, channels, out var subDict, 0, IntPtr.Zero);
        if (subDict != IntPtr.Zero)
        {
            CFRelease(subDict);
        }

        return subscription != IntPtr.Zero;
    }

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
            "nJ" => value / 1_000_000_000.0,
            _ => value / 1_000_000_000.0,
        };
    }
}
