namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

public sealed class PowerStat : IDisposable
{
    private readonly IOReportSampler? sampler;

    private readonly IOReportChannelLayout layout = new();

    private EnergyChannel[] channelKinds = [];

    private double[] channelDivisors = [];

    private bool sampled;

    private bool disposed;

    public bool Supported { get; }

    // Cumulative CPU energy consumption (J)
    public double Cpu { get; private set; }

    public DateTime CpuChangedAt { get; private set; }

    // Cumulative GPU energy consumption (J)
    public double Gpu { get; private set; }

    public DateTime GpuChangedAt { get; private set; }

    // Cumulative ANE (Apple Neural Engine) energy consumption (J)
    public double Ane { get; private set; }

    public DateTime AneChangedAt { get; private set; }

    // Cumulative RAM energy consumption (J)
    public double Ram { get; private set; }

    public DateTime RamChangedAt { get; private set; }

    // Cumulative PCI energy consumption (J)
    public double Pci { get; private set; }

    public DateTime PciChangedAt { get; private set; }

    public double Total => Cpu + Gpu + Ane + Ram + Pci;

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private PowerStat()
    {
        Supported = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (Supported)
        {
            sampler = IOReportSampler.Create("Energy Model", null);
        }

        Update();
    }

    internal static PowerStat Create() => new();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        layout.Dispose();
        sampler?.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!Supported || (sampler is null))
        {
            return false;
        }

        using var sample = new CFRef(sampler.CreateSample());
        if (!sample.IsValid)
        {
            return false;
        }

        return ParseSamples(sample, sampler.Reopened);
    }

    //--------------------------------------------------------------------------------
    // Parse
    //--------------------------------------------------------------------------------

    private bool ParseSamples(IntPtr samples, bool reopened)
    {
        var channelsArray = CFDictionaryGetValue(samples, IOReportSampler.ChannelsKey);
        if ((channelsArray == IntPtr.Zero) || (CFGetTypeID(channelsArray) != CFArrayGetTypeID()))
        {
            return false;
        }

        var count = CFArrayGetCount(channelsArray);
        if (reopened || !layout.Matches(channelsArray, count))
        {
            ClassifyChannels(channelsArray, count);
        }

        var cpuEnergy = 0d;
        var gpuEnergy = 0d;
        var aneEnergy = 0d;
        var ramEnergy = 0d;
        var pciEnergy = 0d;

        for (var i = 0; i < channelKinds.Length; i++)
        {
            var kind = channelKinds[i];
            if (kind == EnergyChannel.None)
            {
                continue;
            }

            var value = (double)IOReportSimpleGetIntegerValue(CFArrayGetValueAtIndex(channelsArray, i), 0);
            var joules = value / channelDivisors[i];

            switch (kind)
            {
                case EnergyChannel.Cpu:
                    cpuEnergy = joules;
                    break;
                case EnergyChannel.Gpu:
                    gpuEnergy = joules;
                    break;
                case EnergyChannel.Ane:
                    aneEnergy = joules;
                    break;
                case EnergyChannel.Ram:
                    ramEnergy = joules;
                    break;
                case EnergyChannel.Pci:
                    pciEnergy = joules;
                    break;
            }
        }

        if (sampled)
        {
            var now = DateTime.Now;
            if (!cpuEnergy.Equals(Cpu))
            {
                CpuChangedAt = now;
            }
            if (!gpuEnergy.Equals(Gpu))
            {
                GpuChangedAt = now;
            }
            if (!aneEnergy.Equals(Ane))
            {
                AneChangedAt = now;
            }
            if (!ramEnergy.Equals(Ram))
            {
                RamChangedAt = now;
            }
            if (!pciEnergy.Equals(Pci))
            {
                PciChangedAt = now;
            }
        }

        Cpu = cpuEnergy;
        Gpu = gpuEnergy;
        Ane = aneEnergy;
        Ram = ramEnergy;
        Pci = pciEnergy;
        sampled = true;

        return true;
    }

    private void ClassifyChannels(IntPtr channelsArray, long count)
    {
        var kinds = new EnergyChannel[count];
        var divisors = new double[count];
        for (var i = 0L; i < count; i++)
        {
            var item = CFArrayGetValueAtIndex(channelsArray, i);
            if (item == IntPtr.Zero)
            {
                continue;
            }

            var groupPtr = IOReportChannelGetGroup(item);
            var group = groupPtr != IntPtr.Zero ? ToManagedString(groupPtr) : null;
            if (group != "Energy Model")
            {
                continue;
            }

            var channelNamePtr = IOReportChannelGetChannelName(item);
            var channelName = channelNamePtr != IntPtr.Zero ? ToManagedString(channelNamePtr) : null;
            if (String.IsNullOrEmpty(channelName))
            {
                continue;
            }

            var unitPtr = IOReportChannelGetUnitLabel(item);
            var unit = unitPtr != IntPtr.Zero ? ToManagedString(unitPtr) : null;

            kinds[i] = ToEnergyChannel(channelName);
            divisors[i] = GetJouleDivisor(unit);
        }

        channelKinds = kinds;
        channelDivisors = divisors;
        layout.Record(channelsArray, count);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static EnergyChannel ToEnergyChannel(string channelName)
    {
        if (channelName.EndsWith("CPU Energy", StringComparison.Ordinal))
        {
            return EnergyChannel.Cpu;
        }
        if (channelName.EndsWith("GPU Energy", StringComparison.Ordinal))
        {
            return EnergyChannel.Gpu;
        }
        if (channelName.StartsWith("ANE", StringComparison.Ordinal))
        {
            return EnergyChannel.Ane;
        }
        if (channelName.StartsWith("DRAM", StringComparison.Ordinal))
        {
            return EnergyChannel.Ram;
        }
        if (channelName.StartsWith("PCI", StringComparison.Ordinal) && channelName.EndsWith("Energy", StringComparison.Ordinal))
        {
            return EnergyChannel.Pci;
        }

        return EnergyChannel.None;
    }

    private static double GetJouleDivisor(string? unit)
    {
        return unit switch
        {
            "mJ" => 1000.0,
            "uJ" => 1_000_000.0,
            _ => 1_000_000_000.0
        };
    }

    private enum EnergyChannel
    {
        None = 0,
        Cpu,
        Gpu,
        Ane,
        Ram,
        Pci
    }
}
