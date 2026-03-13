namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

internal readonly ref struct MachPortRef(uint port)
{
    public static MachPortRef Zero => default;

    public uint Port { get; } = port;

    public bool IsValid => Port != 0;

    public static implicit operator uint(MachPortRef r) => r.Port;

    public void Dispose()
    {
        if (IsValid)
        {
            _ = mach_port_deallocate(mach_task_self(), Port);
        }
    }
}

internal readonly ref struct CFRef(IntPtr pointer)
{
    public static CFRef Zero => default;

    public IntPtr Pointer { get; } = pointer;

    public bool IsValid => Pointer != IntPtr.Zero;

    public static CFRef CreateString(string s) => new(CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8));

    public static implicit operator IntPtr(CFRef r) => r.Pointer;

    public void Dispose()
    {
        if (IsValid)
        {
            CFRelease(Pointer);
        }
    }

    //------------------------------------------------------------------------
    // CFString
    //------------------------------------------------------------------------

    public string? GetString() => ToManagedString(Pointer);

    public bool GetBoolean() => CFBooleanGetValue(Pointer);

    //------------------------------------------------------------------------
    // CFDictionary
    //------------------------------------------------------------------------

    public string? GetString(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return null;
        }

        var value = CFDictionaryGetValue(Pointer, cfKey);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFStringGetTypeID()))
        {
            return null;
        }

        return ToManagedString(value);
    }

    public ulong GetUInt64(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        var value = CFDictionaryGetValue(Pointer, cfKey);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return 0;
        }

        ulong result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
    }

    public long GetInt64(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        var value = CFDictionaryGetValue(Pointer, cfKey);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return 0;
        }

        long result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
    }
}

internal readonly ref struct IORef(IntPtr pointer)
{
    public static IORef Zero => default;

    public IntPtr Pointer { get; } = pointer;

    public bool IsValid => Pointer != IntPtr.Zero;

    public static implicit operator IntPtr(IORef r) => r.Pointer;

    public void Dispose()
    {
        if (IsValid)
        {
            _ = IOObjectRelease(Pointer);
        }
    }
}

internal readonly ref struct IOObj(uint handle)
{
    public static IOObj Zero => default;

    public uint Handle { get; } = handle;

    public bool IsValid => Handle != 0;

    public static implicit operator uint(IOObj o) => o.Handle;

    public void Dispose()
    {
        if (IsValid)
        {
            _ = IOObjectRelease(Handle);
        }
    }

    //------------------------------------------------------------------------
    // Property accessor
    //------------------------------------------------------------------------

    public unsafe string? GetClassName()
    {
        var buffer = stackalloc byte[128];
        return IOObjectGetClass(Handle, buffer) == KERN_SUCCESS ? Marshal.PtrToStringUTF8((IntPtr)buffer) : null;
    }

    public string? GetString(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return null;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFStringGetTypeID()))
        {
            return null;
        }

        return value.GetString();
    }

    public bool GetBoolean(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return false;
        }

        using var val = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        return val.GetBoolean();
    }

    public ulong GetUInt64(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return 0;
        }

        ulong result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
    }

    // TODO dataみなおし
    public uint GetDataUInt32(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFDataGetTypeID()))
        {
            return 0;
        }

        var len = CFDataGetLength(value);
        if (len < 4)
        {
            return 0;
        }

        var ptr = CFDataGetBytePtr(value);
        return (uint)Marshal.ReadInt32(ptr);
    }

    public CFRef GetDictionary(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return CFRef.Zero;
        }

#pragma warning disable CA2000
        // ownership transferred to caller
        var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
#pragma warning restore CA2000
        if (!value.IsValid)
        {
            return CFRef.Zero;
        }

        if (CFGetTypeID(value) != CFDictionaryGetTypeID())
        {
            value.Dispose();
            return CFRef.Zero;
        }

        return value;
    }
}
