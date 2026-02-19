namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
#pragma warning disable IDE1006
#pragma warning disable CA2101
#pragma warning disable CA5392
#pragma warning disable CS8981
internal static class NativeMethods
{
    // 成功コード (mach/kern_return.h)
    public const int KERN_SUCCESS = 0;

    // COM HRESULT成功コード
    public const int S_OK = 0;

    // CFString エンコーディング
    public const uint kCFStringEncodingUTF8 = 0x08000100;

    // CFNumber タイプ
    public const int kCFNumberSInt64Type = 4;

    // IORegistryエントリ検索オプション
    public const uint kIORegistryIterateRecursively = 0x00000001;

    // IOServiceプレーン名
    public const string kIOServicePlane = "IOService";

    //------------------------------------------------------------------------
    // IOKit
    //------------------------------------------------------------------------

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IOServiceMatching(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOServiceGetMatchingServices(
        uint mainPort, nint matching, ref nint existing);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOIteratorNext(nint iterator);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOObjectRelease(nint @object);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOObjectRelease(uint @object);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOServiceGetMatchingService(
        uint mainPort, nint matching);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IORegistryEntryCreateCFProperty(
        uint entry, nint key, nint allocator, uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern nint IORegistryEntrySearchCFProperty(
        uint entry,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string plane,
        nint key,
        nint allocator,
        uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IORegistryEntryGetName(
        uint entry, byte* name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IOCreatePlugInInterfaceForService(
        uint service,
        nint pluginType,
        nint interfaceType,
        nint* theInterface,
        int* theScore);

    //------------------------------------------------------------------------
    // CoreFoundation
    //------------------------------------------------------------------------

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFStringCreateWithCString(
        nint alloc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr,
        uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFStringGetCStringPtr(nint theString, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFStringGetLength(nint theString);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern unsafe bool CFStringGetCString(
        nint theString, byte* buffer, nint bufferSize, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(nint number, int theType, ref long valuePtr);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(nint boolean);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFGetTypeID(nint cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFStringGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFNumberGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFDictionaryGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFBooleanGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFDictionaryGetValue(nint theDict, nint key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFDictionarySetValue")]
    public static extern void CoreFoundationSetDictionaryValue(nint theDict, nint key, nint value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern void CFRelease(nint cf);

#pragma warning disable SA1117
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nint CFUUIDGetConstantUUIDWithBytes(
        nint alloc,
        byte byte0, byte byte1, byte byte2, byte byte3,
        byte byte4, byte byte5, byte byte6, byte byte7,
        byte byte8, byte byte9, byte byte10, byte byte11,
        byte byte12, byte byte13, byte byte14, byte byte15);
#pragma warning restore SA1117
}

// COM-like QueryInterfaceで使用するUUID構造体 (CoreFoundation CFUUIDBytes互換)
// フィールド名はAppleオリジナル定義に合わせる
#pragma warning disable SA1307
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct CFUUIDBytes
{
    public byte byte0;
    public byte byte1;
    public byte byte2;
    public byte byte3;
    public byte byte4;
    public byte byte5;
    public byte byte6;
    public byte byte7;
    public byte byte8;
    public byte byte9;
    public byte byte10;
    public byte byte11;
    public byte byte12;
    public byte byte13;
    public byte byte14;
    public byte byte15;
}
#pragma warning restore SA1307
#pragma warning restore IDE1006
#pragma warning restore CA2101
#pragma warning restore CA5392
#pragma warning restore CS8981
