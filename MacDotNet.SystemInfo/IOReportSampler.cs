namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

// IOReport channels and subscription created once and sampled repeatedly (CpuFrequency, PowerStat)
internal sealed class IOReportSampler : IDisposable
{
    // ReSharper disable once StringLiteralTypo
    public static readonly IntPtr ChannelsKey = CFSTR("IOReportChannels");

    private readonly string group;

    private readonly string? subGroup;

    // Mutable copy of the channels in the group (passed to IOReportCreateSamples)
    private SafeCFTypeHandle? channels;

    private SafeCFTypeHandle? subscription;

    private bool disposed;

    public bool IsOpen => (channels is not null) && (subscription is not null);

    public IntPtr Channels => channels?.Value ?? IntPtr.Zero;

    // The channels and the subscription were recreated by the last CreateSample
    public bool Reopened { get; private set; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    private IOReportSampler(string group, string? subGroup)
    {
        this.group = group;
        this.subGroup = subGroup;
        Open();
    }

    // Does not throw when IOReport is not available (IsOpen = false)
    public static IOReportSampler Create(string group, string? subGroup) => new(group, subGroup);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        subscription?.Dispose();
        channels?.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Sample
    //--------------------------------------------------------------------------------

    // Returns an owned sample (released by the caller), 0 on failure.
    // When sampling fails or the sampler is not open, the channels and the subscription are recreated and sampling is retried once.
    public IntPtr CreateSample()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        Reopened = false;

        var sample = Sample();
        if ((sample != IntPtr.Zero) || !Open())
        {
            return sample;
        }

        Reopened = true;
        return Sample();
    }

    private IntPtr Sample() =>
        (channels is not null) && (subscription is not null) ? IOReportCreateSamples(subscription.Value, channels.Value, IntPtr.Zero) : IntPtr.Zero;

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // Same calls as before: IOReportCopyChannelsInGroup -> CFDictionaryCreateMutableCopy -> IOReportCreateSubscription
    private bool Open()
    {
        Close();

        try
        {
            var copied = CopyChannels(group, subGroup);
            if (copied == IntPtr.Zero)
            {
                return false;
            }

            channels = new SafeCFTypeHandle(copied);

            var created = IOReportCreateSubscription(IntPtr.Zero, copied, out var subscribed, 0, IntPtr.Zero);
            // The subscribed channels are not used
            if (subscribed != IntPtr.Zero)
            {
                CFRelease(subscribed);
            }

            if (created == IntPtr.Zero)
            {
                Close();
                return false;
            }

            subscription = new SafeCFTypeHandle(created);
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            Close();
            return false;
        }
        catch (DllNotFoundException)
        {
            Close();
            return false;
        }
    }

    private void Close()
    {
        subscription?.Dispose();
        subscription = null;
        channels?.Dispose();
        channels = null;
    }

    private static IntPtr CopyChannels(string group, string? subGroup)
    {
        using var groupRef = CFRef.CreateString(group);
        using var subGroupRef = subGroup is null ? CFRef.Zero : CFRef.CreateString(subGroup);
        if (!groupRef.IsValid)
        {
            return IntPtr.Zero;
        }

        using var channel = new CFRef(IOReportCopyChannelsInGroup(groupRef, subGroupRef, 0, 0, 0));
        if (!channel.IsValid)
        {
            return IntPtr.Zero;
        }

        // IOReportCreateSubscription requires a mutable dictionary
        var mutableCopy = CFDictionaryCreateMutableCopy(IntPtr.Zero, 0, channel);
        if (mutableCopy == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (CFDictionaryGetValue(mutableCopy, ChannelsKey) == IntPtr.Zero)
        {
            CFRelease(mutableCopy);
            return IntPtr.Zero;
        }

        return mutableCopy;
    }
}

// Channel names and groups of the sample a channel mapping was built from.
// They are retained, so that later samples can be checked for the same layout without allocation.
internal sealed class IOReportChannelLayout : IDisposable
{
    private SafeCFTypeHandle?[] names = [];

    private SafeCFTypeHandle?[] groups = [];

    public void Dispose()
    {
        Release();
    }

    // Same channel count, and the same name and group at every index
    public bool Matches(IntPtr channels, long count)
    {
        if (count != names.Length)
        {
            return false;
        }

        for (var i = 0; i < names.Length; i++)
        {
            var channel = CFArrayGetValueAtIndex(channels, i);
            if (!IsSame(GetName(channel), names[i]) || !IsSame(GetGroup(channel), groups[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Keeps the names and groups of the channels (retained until the next Record or Dispose)
    public void Record(IntPtr channels, long count)
    {
        Release();

        var newNames = new SafeCFTypeHandle?[count];
        var newGroups = new SafeCFTypeHandle?[count];
        for (var i = 0L; i < count; i++)
        {
            var channel = CFArrayGetValueAtIndex(channels, i);
            newNames[i] = Retain(GetName(channel));
            newGroups[i] = Retain(GetGroup(channel));
        }

        names = newNames;
        groups = newGroups;
    }

    private void Release()
    {
        foreach (var name in names)
        {
            name?.Dispose();
        }

        foreach (var group in groups)
        {
            group?.Dispose();
        }

        names = [];
        groups = [];
    }

    private static IntPtr GetName(IntPtr channel) => channel != IntPtr.Zero ? IOReportChannelGetChannelName(channel) : IntPtr.Zero;

    private static IntPtr GetGroup(IntPtr channel) => channel != IntPtr.Zero ? IOReportChannelGetGroup(channel) : IntPtr.Zero;

    private static SafeCFTypeHandle? Retain(IntPtr value) => value != IntPtr.Zero ? new SafeCFTypeHandle(CFRetain(value)) : null;

    // CFEqual does not accept NULL
    private static bool IsSame(IntPtr value, SafeCFTypeHandle? recorded)
    {
        var expected = recorded?.Value ?? IntPtr.Zero;
        return (value == expected) || ((value != IntPtr.Zero) && (expected != IntPtr.Zero) && CFEqual(value, expected));
    }
}
