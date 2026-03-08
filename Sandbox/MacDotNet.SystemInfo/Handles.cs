namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;
using static MacDotNet.SystemInfo.NativeMethods;

/// <summary>
/// CoreFoundation ハンドル (IntPtr) の using 対応ラッパー。<br/>
/// スコープ終了時に自動で CFRelease を呼び出す。
/// </summary>
/// <example>
/// <code>
/// using var key   = CFRef.CreateString("PropertyName");
/// using var value = new CFRef(IORegistryEntryCreateCFProperty(service, key, IntPtr.Zero, 0));
/// if (!value.IsValid) return;
/// // try-finally 不要
/// </code>
/// </example>
internal readonly ref struct CFRef(IntPtr ptr)
{
    public static CFRef Zero => default;

    public IntPtr Ptr { get; } = ptr;

    public bool IsValid => Ptr != IntPtr.Zero;

    /// <summary>CFStringCreateWithCString のショートカット。</summary>
    public static CFRef CreateString(string s) =>
        new(CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8));

    public static implicit operator IntPtr(CFRef r) => r.Ptr;

    public void Dispose()
    {
        if (IsValid)
        {
            CFRelease(Ptr);
        }
    }

    //------------------------------------------------------------------------
    // CF 文字列変換 / CF string conversion
    //------------------------------------------------------------------------

    /// <summary>
    /// この CFRef が保持する CFStringRef をマネージ文字列に変換する。
    /// <para>Converts the CFStringRef held by this CFRef to a managed string.</para>
    /// </summary>
    public string? ToManagedString() => NativeMethods.ToManagedString(Ptr);

    //------------------------------------------------------------------------
    // CFDictionary 操作 / CFDictionary operations
    //------------------------------------------------------------------------

    /// <summary>
    /// この CFDictionary から CFString 値をマネージ文字列として取得する。
    /// キーが存在しないか CFString 以外の型の場合は null を返す。
    /// <para>
    /// Retrieves a CFString value from this CFDictionary as a managed string.
    /// Returns null if the key is absent or the value is not a CFString.
    /// </para>
    /// </summary>
    public string? GetString(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return null;
        }

        var val = CFDictionaryGetValue(Ptr, cfKey);
        return val != IntPtr.Zero && CFGetTypeID(val) == CFStringGetTypeID() ? NativeMethods.ToManagedString(val) : null;
    }

    /// <summary>
    /// この CFDictionary から CFNumber 値を 64 ビット符号なし整数として取得する。
    /// キーが存在しないか CFNumber 以外の型の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFNumber value from this CFDictionary as a 64-bit unsigned integer.
    /// Returns 0 if the key is absent or the value is not a CFNumber.
    /// </para>
    /// </summary>
    public ulong GetUInt64(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        var val = CFDictionaryGetValue(Ptr, cfKey);
        if (val == IntPtr.Zero || CFGetTypeID(val) != CFNumberGetTypeID())
        {
            return 0;
        }

        ulong result = 0;
        CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
        return result;
    }

    /// <summary>
    /// この CFDictionary から CFNumber 値を 64 ビット整数として取得する。
    /// キーが存在しないか CFNumber 以外の型の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFNumber value from this CFDictionary as a 64-bit integer.
    /// Returns 0 if the key is absent or the value is not a CFNumber.
    /// </para>
    /// </summary>
    public long GetInt64(string key)
    {
        using var cfKey = CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        var val = CFDictionaryGetValue(Ptr, cfKey);
        if (val == IntPtr.Zero || CFGetTypeID(val) != CFNumberGetTypeID())
        {
            return 0;
        }

        long result = 0;
        CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
        return result;
    }
}

/// <summary>
/// IOKit IntPtr ハンドル (主にイテレータ) の using 対応ラッパー。<br/>
/// スコープ終了時に自動で IOObjectRelease(IntPtr) を呼び出す。
/// </summary>
/// <example>
/// <code>
/// var iterPtr = IntPtr.Zero;
/// IOServiceGetMatchingServices(0, IOServiceMatching("IOMedia"), ref iterPtr);
/// using var iter = new IORef(iterPtr);
/// uint raw;
/// while ((raw = IOIteratorNext(iter)) != 0)
/// {
///     using var entry = new IOObj(raw);
///     // try-finally 不要
/// }
/// </code>
/// </example>
internal readonly ref struct IORef(IntPtr ptr)
{
    public static IORef Zero => default;

    public IntPtr Ptr { get; } = ptr;

    public bool IsValid => Ptr != IntPtr.Zero;

    public static implicit operator IntPtr(IORef r) => r.Ptr;

    public void Dispose()
    {
        if (IsValid)
        {
            IOObjectRelease(Ptr);
        }
    }
}

/// <summary>
/// IOKit uint ハンドルの using 対応ラッパー。<br/>
/// スコープ終了時に自動で IOObjectRelease(uint) を呼び出す。
/// </summary>
/// <example>
/// <code>
/// using var entry = new IOObj(IOIteratorNext(iter));
/// if (!entry.IsValid) continue;
/// var model = entry.GetString("model");
/// </code>
/// </example>
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
            IOObjectRelease(Handle);
        }
    }

    //------------------------------------------------------------------------
    // IOKit プロパティ取得 / IOKit property accessors
    //------------------------------------------------------------------------

    /// <summary>
    /// IOKit オブジェクトのクラス名を取得する。失敗時は null を返す。
    /// <para>Returns the IOKit class name of this object. Returns null on failure.</para>
    /// </summary>
    public unsafe string? GetClassName()
    {
        const int IO_NAME_LENGTH = 128;
        byte* buf = stackalloc byte[IO_NAME_LENGTH];
        return IOObjectGetClass(Handle, buf) == KERN_SUCCESS
            ? Marshal.PtrToStringUTF8((IntPtr)buf)
            : null;
    }

    /// <summary>
    /// IOKit エントリから CFString プロパティを取得してマネージ文字列に変換する。
    /// プロパティが存在しないか CFString 以外の型の場合は null を返す。
    /// <para>
    /// Retrieves a CFString property from this IOKit entry and converts it to a managed string.
    /// Returns null if the property is absent or is not a CFString.
    /// </para>
    /// </summary>
    public string? GetString(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return null;
        }

        using var val = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        return val.IsValid && CFGetTypeID(val) == CFStringGetTypeID() ? ToManagedString(val) : null;
    }

    /// <summary>
    /// IOKit エントリから CFBoolean プロパティを取得する。
    /// プロパティが存在しないか CFBoolean 以外の型の場合は false を返す。
    /// <para>
    /// Retrieves a CFBoolean property from this IOKit entry.
    /// Returns false if the property is absent or is not a CFBoolean.
    /// </para>
    /// </summary>
    public bool GetBoolean(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return false;
        }

        using var val = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        return val.IsValid && CFBooleanGetValue(val);
    }

    /// <summary>
    /// IOKit エントリから CFNumber プロパティを 64 ビット符号なし整数として取得する。
    /// プロパティが存在しないか CFNumber 以外の型の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFNumber property from this IOKit entry as a 64-bit unsigned integer.
    /// Returns 0 if the property is absent or is not a CFNumber.
    /// </para>
    /// </summary>
    public ulong GetUInt64(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        using var val = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!val.IsValid || CFGetTypeID(val) != CFNumberGetTypeID())
        {
            return 0;
        }

        ulong result = 0;
        CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
        return result;
    }

    /// <summary>
    /// IOKit エントリから CFData プロパティを取得し、先頭 4 バイトを uint32 として返す。
    /// データが 4 バイト未満の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFData property from this IOKit entry and returns the first 4 bytes as a uint32.
    /// Returns 0 if the data is shorter than 4 bytes.
    /// </para>
    /// </summary>
    public uint GetDataUInt32(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return 0;
        }

        using var val = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!val.IsValid || CFGetTypeID(val) != CFDataGetTypeID())
        {
            return 0;
        }

        var len = CFDataGetLength(val);
        if (len < 4)
        {
            return 0;
        }

        var ptr = CFDataGetBytePtr(val);
        return (uint)Marshal.ReadInt32(ptr);
    }

    /// <summary>
    /// IOKit エントリから CFDictionary プロパティを取得して CFRef として返す。
    /// 呼び出し元は using var で受け取ることで自動解放できる。
    /// プロパティが存在しないか CFDictionary 以外の型の場合は CFRef.Zero を返す。
    /// <para>
    /// Retrieves a CFDictionary property from this IOKit entry, returning it as a CFRef.
    /// The caller should use <c>using var</c> to ensure automatic release.
    /// Returns CFRef.Zero if absent or not a CFDictionary.
    /// </para>
    /// </summary>
    public CFRef GetDictionary(string key)
    {
        using var cfKey = CFRef.CreateString(key);
        if (!cfKey.IsValid)
        {
            return CFRef.Zero;
        }

        var val = new CFRef(IORegistryEntryCreateCFProperty(Handle, cfKey, IntPtr.Zero, 0));
        if (!val.IsValid)
        {
            return CFRef.Zero;
        }

        if (CFGetTypeID(val) != CFDictionaryGetTypeID())
        {
            val.Dispose();
            return CFRef.Zero;
        }

        return val; // 所有権を呼び出し元へ移譲 / ownership transferred to caller
    }
}
