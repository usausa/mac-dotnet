namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

using static MacDotNet.SystemInfo.NativeMethods;

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

    public bool ContainsKey(string key)
    {
        using var cfKey = CreateString(key);
        return ContainsKey(cfKey.Pointer);
    }

    public bool ContainsKey(IntPtr key) => (key != IntPtr.Zero) && CFDictionaryContainsKey(Pointer, key);

    public string? GetString(string key)
    {
        using var cfKey = CreateString(key);
        return GetString(cfKey.Pointer);
    }

    public string? GetString(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return null;
        }

        var value = CFDictionaryGetValue(Pointer, key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFStringGetTypeID()))
        {
            return null;
        }

        return ToManagedString(value);
    }

    public ulong GetUInt64(string key)
    {
        using var cfKey = CreateString(key);
        return GetUInt64(cfKey.Pointer);
    }

    public ulong GetUInt64(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return 0;
        }

        var value = CFDictionaryGetValue(Pointer, key);
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
        return GetInt64(cfKey.Pointer);
    }

    public long GetInt64(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return 0;
        }

        var value = CFDictionaryGetValue(Pointer, key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return 0;
        }

        long result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
    }

    public bool TryGetInt64(string key, out long result)
    {
        using var cfKey = CreateString(key);
        return TryGetInt64(cfKey.Pointer, out result);
    }

    public bool TryGetInt64(IntPtr key, out long result)
    {
        result = 0;

        if (key == IntPtr.Zero)
        {
            return false;
        }

        var value = CFDictionaryGetValue(Pointer, key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return false;
        }

        return CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
    }

    public bool GetBoolean(string key)
    {
        using var cfKey = CreateString(key);
        return GetBoolean(cfKey.Pointer);
    }

    public bool GetBoolean(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return false;
        }

        var value = CFDictionaryGetValue(Pointer, key);
        if (value == IntPtr.Zero)
        {
            return false;
        }

        var typeId = CFGetTypeID(value);
        if (typeId == CFBooleanGetTypeID())
        {
            return CFBooleanGetValue(value);
        }

        if (typeId == CFNumberGetTypeID())
        {
            long result = 0;
            return CFNumberGetValue(value, kCFNumberSInt64Type, ref result) && (result != 0);
        }

        return false;
    }

    public CFRef GetDictionary(string key)
    {
        using var cfKey = CreateString(key);
        return GetDictionary(cfKey.Pointer);
    }

    public CFRef GetDictionary(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return Zero;
        }

        var value = CFDictionaryGetValue(Pointer, key);
        if ((value == IntPtr.Zero) || (CFGetTypeID(value) != CFDictionaryGetTypeID()))
        {
            return Zero;
        }

        return new CFRef(CFRetain(value));
    }
}

internal readonly ref struct IORef(uint handle)
{
    public static IORef Zero => default;

    public uint Handle { get; } = handle;

    public bool IsValid => Handle != 0;

    public static implicit operator uint(IORef r) => r.Handle;

    public void Dispose()
    {
        if (IsValid)
        {
            _ = IOObjectRelease(Handle);
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

    // ReSharper disable once RedundantUnsafeContext
    public unsafe string? GetClassName()
    {
        var buffer = stackalloc byte[128];
        return IOObjectGetClass(Handle, buffer) == KERN_SUCCESS ? Marshal.PtrToStringUTF8((IntPtr)buffer) : null;
    }

    public string? GetString(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        return GetString(cfKey.Pointer);
    }

    public string? GetString(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return null;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, key, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFStringGetTypeID()))
        {
            return null;
        }

        return value.GetString();
    }

    public bool GetBoolean(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        return GetBoolean(cfKey.Pointer);
    }

    public bool GetBoolean(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return false;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, key, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFBooleanGetTypeID()))
        {
            return false;
        }

        return value.GetBoolean();
    }

    public ulong GetUInt64(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        return GetUInt64(cfKey.Pointer);
    }

    public ulong GetUInt64(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return 0;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, key, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return 0;
        }

        ulong result = 0;
        CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
        return result;
    }

    public bool TryGetInt64(string key, out long result)
    {
        using var cfKey = CFRef.CreateString(key);
        return TryGetInt64(cfKey.Pointer, out result);
    }

    public bool TryGetInt64(IntPtr key, out long result)
    {
        result = 0;

        if (key == IntPtr.Zero)
        {
            return false;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, key, IntPtr.Zero, 0));
        if (!value.IsValid || (CFGetTypeID(value) != CFNumberGetTypeID()))
        {
            return false;
        }

        return CFNumberGetValue(value, kCFNumberSInt64Type, ref result);
    }

    public uint GetDataUInt32(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        return GetDataUInt32(cfKey.Pointer);
    }

    public uint GetDataUInt32(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return 0;
        }

        using var value = new CFRef(IORegistryEntryCreateCFProperty(Handle, key, IntPtr.Zero, 0));
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
        return GetDictionary(cfKey.Pointer);
    }

    public CFRef GetDictionary(IntPtr key)
    {
        if (key == IntPtr.Zero)
        {
            return CFRef.Zero;
        }

        // Ownership of the returned dictionary is transferred to the caller
        var value = IORegistryEntryCreateCFProperty(Handle, key, IntPtr.Zero, 0);
        if ((value != IntPtr.Zero) && (CFGetTypeID(value) != CFDictionaryGetTypeID()))
        {
            CFRelease(value);
            return CFRef.Zero;
        }

        return new CFRef(value);
    }
}

internal readonly ref struct AutoreleasePool(IntPtr pool)
{
    public IntPtr Pool { get; } = pool;

    public static AutoreleasePool Push() => new(objc_autoreleasePoolPush());

    public void Dispose()
    {
        if (Pool != IntPtr.Zero)
        {
            objc_autoreleasePoolPop(Pool);
        }
    }
}

//------------------------------------------------------------------------
// Held handles
//------------------------------------------------------------------------

// io_object_t (io_service_t, io_registry_entry_t)
internal sealed class SafeIOObjectHandle : SafeHandle
{
    public SafeIOObjectHandle(uint value)
        : base(IntPtr.Zero, true)
    {
        SetHandle((IntPtr)value);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public uint Value => (uint)handle;

    protected override bool ReleaseHandle() => IOObjectRelease((uint)handle) == KERN_SUCCESS;
}

// io_connect_t
internal sealed class SafeIOConnectHandle : SafeHandle
{
    public SafeIOConnectHandle(uint value)
        : base(IntPtr.Zero, true)
    {
        SetHandle((IntPtr)value);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public uint Value => (uint)handle;

    protected override bool ReleaseHandle() => IOServiceClose((uint)handle) == KERN_SUCCESS;
}

// CFTypeRef (owned reference)
internal sealed class SafeCFTypeHandle : SafeHandle
{
    public SafeCFTypeHandle(IntPtr value)
        : base(IntPtr.Zero, true)
    {
        SetHandle(value);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public IntPtr Value => handle;

    protected override bool ReleaseHandle()
    {
        CFRelease(handle);
        return true;
    }
}

internal sealed class SafeMachPortHandle : SafeHandle
{
    public SafeMachPortHandle(uint value)
        : base(IntPtr.Zero, true)
    {
        SetHandle((IntPtr)value);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public uint Value => (uint)handle;

    protected override bool ReleaseHandle() => mach_port_deallocate(MachTask.Self, (uint)handle) == KERN_SUCCESS;
}

internal static class MachTask
{
    public static readonly uint Self = mach_task_self();
}
