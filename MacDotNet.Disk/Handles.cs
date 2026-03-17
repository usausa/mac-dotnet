namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

using static MacDotNet.Disk.NativeMethods;

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

    public string? GetString() => DiskInfo.CfStringToManaged(Pointer);

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

        // CFDictionaryGetValueはGet規則 — 返り値をCFReleaseしてはならない
        // CFDictionaryGetValue follows the Get Rule — the returned value must NOT be CFReleased
        var value = CFDictionaryGetValue(Pointer, cfKey);
        if (value == IntPtr.Zero || CFGetTypeID(value) != CFStringGetTypeID())
        {
            return null;
        }

        return DiskInfo.CfStringToManaged(value);
    }

    public long GetInt64(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        var value = CFDictionaryGetValue(Pointer, cfKey);
        if (value == IntPtr.Zero || CFGetTypeID(value) != CFNumberGetTypeID())
        {
            return 0;
        }

        long result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
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
        if (!value.IsValid || CFGetTypeID(value) != CFStringGetTypeID())
        {
            return null;
        }

        return value.GetString();
    }

    public long GetInt64(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!value.IsValid || CFGetTypeID(value) != CFNumberGetTypeID())
        {
            return 0;
        }

        long result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
    }

    public bool GetBoolean(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return false;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!value.IsValid || CFGetTypeID(value) != CFBooleanGetTypeID())
        {
            return false;
        }

        return CFBooleanGetValue(value);
    }

    public CFRef GetDictionary(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return CFRef.Zero;
        }

        // ownership transferred to caller
#pragma warning disable CA2000
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

    //------------------------------------------------------------------------
    // Recursive search accessor
    //------------------------------------------------------------------------

    public string? SearchString(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return null;
        }

        using var val = new CFRef(IORegistryEntrySearchCFProperty(Handle, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively));
        if (!val.IsValid || CFGetTypeID(val) != CFStringGetTypeID())
        {
            return null;
        }

        return val.GetString();
    }

    public long SearchInt64(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        using var val = new CFRef(IORegistryEntrySearchCFProperty(Handle, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively));
        if (!val.IsValid || CFGetTypeID(val) != CFNumberGetTypeID())
        {
            return 0;
        }

        long result = 0;
        CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
        return result;
    }

    public bool SearchBool(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return false;
        }

        using var val = new CFRef(IORegistryEntrySearchCFProperty(Handle, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively));
        if (!val.IsValid || CFGetTypeID(val) != CFBooleanGetTypeID())
        {
            return false;
        }

        return CFBooleanGetValue(val);
    }

    public CFRef SearchDictionary(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return CFRef.Zero;
        }

        // ownership transferred to caller
#pragma warning disable CA2000
        var val = new CFRef(IORegistryEntrySearchCFProperty(Handle, kIOServicePlane, cfKey, IntPtr.Zero, kIORegistryIterateRecursively));
#pragma warning restore CA2000
        if (!val.IsValid)
        {
            return CFRef.Zero;
        }

        if (CFGetTypeID(val) != CFDictionaryGetTypeID())
        {
            val.Dispose();
            return CFRef.Zero;
        }

        return val;
    }
}
