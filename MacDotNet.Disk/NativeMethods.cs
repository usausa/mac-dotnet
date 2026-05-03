namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
#pragma warning disable IDE1006
#pragma warning disable CA2101
#pragma warning disable CA5392
#pragma warning disable CS8981
internal static class NativeMethods
{
    //------------------------------------------------------------------------
    // Constants
    //------------------------------------------------------------------------

    // Mach kernel success return code (mach/kern_return.h)
    public const int KERN_SUCCESS = 0;

    // COM QueryInterface success HRESULT
    public const int S_OK = 0;

    // CFStringEncoding: UTF-8 (CFString.h)
    public const uint kCFStringEncodingUTF8 = 0x08000100;

    // CFNumber type identifier for 64-bit signed integer (CFNumber.h)
    public const int kCFNumberSInt64Type = 4;

    // IORegistry search option: iterate recursively (IORegistryExplorer.h)
    public const uint kIORegistryIterateRecursively = 0x00000001;

    //------------------------------------------------------------------------
    // Struct
    //------------------------------------------------------------------------

    // UUID byte layout for COM-like QueryInterface calls (CoreFoundation CFUUIDBytes)
    // Field names follow the original Apple definition
#pragma warning disable SA1307
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CFUUIDBytes
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

    //------------------------------------------------------------------------
    // IOKit
    //------------------------------------------------------------------------

    private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    [DllImport(IOKitLib)]
    public static extern IntPtr IOServiceMatching([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(IOKitLib)]
    public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, ref uint existing);

    [DllImport(IOKitLib)]
    public static extern uint IOIteratorNext(uint iterator);

    [DllImport(IOKitLib)]
    public static extern int IOObjectRelease(uint @object);

    [DllImport(IOKitLib)]
    public static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

    [DllImport(IOKitLib)]
    public static extern IntPtr IORegistryEntrySearchCFProperty(
        uint entry,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string plane,
        IntPtr key,
        IntPtr allocator,
        uint options);

    [DllImport(IOKitLib)]
    public static extern unsafe int IORegistryEntryGetName(uint entry, byte* name);

    [DllImport(IOKitLib)]
    public static extern unsafe int IOCreatePlugInInterfaceForService(
        uint service,
        IntPtr pluginType,
        IntPtr interfaceType,
        IntPtr* theInterface,
        int* theScore);

    [DllImport(IOKitLib)]
    public static extern unsafe int IOObjectGetClass(uint @object, byte* className);

    //------------------------------------------------------------------------
    // CoreFoundation
    //------------------------------------------------------------------------

    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFStringCreateWithCString(
        IntPtr alloc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr,
        uint encoding);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundationLib)]
    public static extern unsafe bool CFStringGetCString(IntPtr theString, byte* buffer, IntPtr bufferSize, uint encoding);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, ref long valuePtr);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(IntPtr boolean);

    [DllImport(CoreFoundationLib)]
    public static extern UIntPtr CFGetTypeID(IntPtr cf);

    [DllImport(CoreFoundationLib)]
    public static extern UIntPtr CFStringGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern UIntPtr CFNumberGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern UIntPtr CFDictionaryGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern UIntPtr CFBooleanGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport(CoreFoundationLib)]
    public static extern void CFRelease(IntPtr cf);

#pragma warning disable SA1117
    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFUUIDGetConstantUUIDWithBytes(
        IntPtr alloc,
        byte byte0, byte byte1, byte byte2, byte byte3,
        byte byte4, byte byte5, byte byte6, byte byte7,
        byte byte8, byte byte9, byte byte10, byte byte11,
        byte byte12, byte byte13, byte byte14, byte byte15);
#pragma warning restore SA1117

    //------------------------------------------------------------------------
    // Helper
    //------------------------------------------------------------------------

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

    public static unsafe string? ToManagedString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
        {
            return null;
        }

        var ptr = CFStringGetCStringPtr(cfString, kCFStringEncodingUTF8);
        if (ptr != IntPtr.Zero)
        {
            return Marshal.PtrToStringUTF8(ptr);
        }

        var length = CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        var bufferSize = (int)((length * 4) + 1);
        if (bufferSize <= 1024)
        {
            var buffer = stackalloc byte[bufferSize];
            return CFStringGetCString(cfString, buffer, bufferSize, kCFStringEncodingUTF8) ? Marshal.PtrToStringUTF8((IntPtr)buffer) : null;
        }
        else
        {
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                fixed (byte* p = buffer)
                {
                    return CFStringGetCString(cfString, p, bufferSize, kCFStringEncodingUTF8) ? Marshal.PtrToStringUTF8((IntPtr)p) : null;
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
