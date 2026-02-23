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

    // マウントフラグ (sys/mount.h) / Mount flags
    public const uint MNT_RDONLY = 0x00000001;  // 読み取り専用 / Read-only filesystem
    public const uint MNT_LOCAL = 0x00001000;   // ローカルFS / Local filesystem

    // getfsstat モード (sys/mount.h) / getfsstat mode flags
    public const int MNT_NOWAIT = 2;  // 非同期: キャッシュ値を即返す / Return cached values immediately

    //------------------------------------------------------------------------
    // IOKit
    //------------------------------------------------------------------------

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern IntPtr IOServiceMatching([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, ref uint existing);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOIteratorNext(uint iterator);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IOObjectRelease(uint @object);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern IntPtr IORegistryEntrySearchCFProperty(
        uint entry,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string plane,
        IntPtr key,
        IntPtr allocator,
        uint options);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IORegistryEntryGetName(uint entry, byte* name);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IOCreatePlugInInterfaceForService(
        uint service,
        IntPtr pluginType,
        IntPtr interfaceType,
        IntPtr* theInterface,
        int* theScore);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern int IORegistryEntryGetParentEntry(uint entry, [MarshalAs(UnmanagedType.LPUTF8Str)] string plane, out uint parent);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    public static extern unsafe int IOObjectGetClass(uint @object, byte* className);

    //------------------------------------------------------------------------
    // CoreFoundation
    //------------------------------------------------------------------------

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFStringCreateWithCString(
        IntPtr alloc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr,
        uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFStringGetLength(IntPtr theString);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern unsafe bool CFStringGetCString(IntPtr theString, byte* buffer, IntPtr bufferSize, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, ref long valuePtr);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(IntPtr boolean);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFGetTypeID(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFStringGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFNumberGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFDictionaryGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern nuint CFBooleanGetTypeID();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFDictionarySetValue")]
    public static extern void CoreFoundationSetDictionaryValue(IntPtr theDict, IntPtr key, IntPtr value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern void CFRelease(IntPtr cf);

#pragma warning disable SA1117
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    public static extern IntPtr CFUUIDGetConstantUUIDWithBytes(
        IntPtr alloc,
        byte byte0, byte byte1, byte byte2, byte byte3,
        byte byte4, byte byte5, byte byte6, byte byte7,
        byte byte8, byte byte9, byte byte10, byte byte11,
        byte byte12, byte byte13, byte byte14, byte byte15);
#pragma warning restore SA1117

    // COM-like インターフェースの Release を呼び出す共通ヘルパー
    public static unsafe void ReleasePlugInInterface(IntPtr ppInterface)
    {
        if (ppInterface == IntPtr.Zero)
        {
            return;
        }

        var vtable = *(IntPtr*)ppInterface;
        var releaseFn = (delegate* unmanaged<IntPtr, uint>)(*((IntPtr*)vtable + 3));
        releaseFn(ppInterface);
    }

    //------------------------------------------------------------------------
    // libc (statfs)
    //------------------------------------------------------------------------

    [DllImport("libc")]
    public static extern unsafe int getfsstat(statfs_disk* buf, int bufsize, int mode);
}

// マウント済みファイルシステム情報 (sys/mount.h statfs)
// GetVolumes が使用する最小限フィールドのみ定義する
#pragma warning disable SA1307
#pragma warning disable SA1310
#pragma warning disable SA1300
[StructLayout(LayoutKind.Sequential)]
internal struct fsid_disk
{
    public int val0;
    public int val1;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct statfs_disk
{
    public uint f_bsize;
    public int f_iosize;
    public ulong f_blocks;
    public ulong f_bfree;
    public ulong f_bavail;
    public ulong f_files;
    public ulong f_ffree;
    public fsid_disk f_fsid;
    public uint f_owner;
    public uint f_type;
    public uint f_flags;
    public uint f_fssubtype;
    public fixed byte f_fstypename[16];
    public fixed byte f_mntonname[1024];
    public fixed byte f_mntfromname[1024];
    public uint f_flags_ext;
    public fixed uint f_reserved[7];
}
#pragma warning restore SA1300
#pragma warning restore SA1310
#pragma warning restore SA1307

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
