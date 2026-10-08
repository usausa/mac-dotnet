namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

internal sealed class IOReportSampler : IDisposable
{
    public static readonly IntPtr ChannelsKey = CFSTR("IOReportChannels");

    private readonly string group;

    private readonly string? subGroup;

    private SafeCFTypeHandle? channels;

    private SafeCFTypeHandle? subscription;

    private bool disposed;

    public bool IsOpen => (channels is not null) && (subscription is not null);

    public IntPtr Channels => channels?.Value ?? IntPtr.Zero;

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

internal sealed class IOReportChannelLayout : IDisposable
{
    private SafeCFTypeHandle?[] names = [];

    private SafeCFTypeHandle?[] groups = [];

    public void Dispose()
    {
        Release();
    }

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

    private static bool IsSame(IntPtr value, SafeCFTypeHandle? recorded)
    {
        var expected = recorded?.Value ?? IntPtr.Zero;
        return (value == expected) || ((value != IntPtr.Zero) && (expected != IntPtr.Zero) && CFEqual(value, expected));
    }
}
