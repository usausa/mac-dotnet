using System.Runtime.InteropServices;

namespace CpuFrequencySample;

/// <summary>
/// macOS ネイティブ API (CoreFoundation, IOKit, IOReport) の P/Invoke 定義。
/// Swift版 bridge.h に定義された関数宣言に対応する。
/// </summary>
static class NativeBindings
{
    // =========================================================================
    // CoreFoundation
    // =========================================================================

    const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // --- CFString ---

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string cStr, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern bool CFStringGetCString(IntPtr theString, IntPtr buffer, long bufferSize, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern long CFStringGetLength(IntPtr theString);

    public const uint kCFStringEncodingUTF8 = 0x08000100;

    public static IntPtr CreateCFString(string s)
    {
        return CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8);
    }

    public static string? CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero) return null;
        var length = CFStringGetLength(cfString) * 4 + 1;
        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (CFStringGetCString(cfString, buffer, length, kCFStringEncodingUTF8))
                return Marshal.PtrToStringUTF8(buffer);
            return null;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    // --- CFDictionary ---

    [DllImport(CoreFoundation)]
    public static extern int CFDictionaryGetCount(IntPtr theDict);

    [DllImport(CoreFoundation)]
    public static extern bool CFDictionaryGetValueIfPresent(IntPtr theDict, IntPtr key, out IntPtr value);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDictionaryCreateMutableCopy(IntPtr allocator, long capacity, IntPtr theDict);

    // --- CFArray ---

    [DllImport(CoreFoundation)]
    public static extern long CFArrayGetCount(IntPtr theArray);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);

    // --- CFData ---

    [DllImport(CoreFoundation)]
    public static extern long CFDataGetLength(IntPtr theData);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr theData);

    // --- CFRelease / CFRetain ---

    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRetain(IntPtr cf);

    // =========================================================================
    // IOKit
    // =========================================================================

    const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";

    public const uint kIOMasterPortDefault = 0;

    [DllImport(IOKit)]
    public static extern IntPtr IOServiceMatching(string name);

    [DllImport(IOKit)]
    public static extern int IOServiceGetMatchingServices(uint masterPort, IntPtr matching, out uint existing);

    [DllImport(IOKit)]
    public static extern uint IOIteratorNext(uint iterator);

    [DllImport(IOKit)]
    public static extern int IOObjectRelease(uint obj);

    [DllImport(IOKit)]
    public static extern int IORegistryEntryGetName(uint entry, IntPtr name);

    [DllImport(IOKit)]
    public static extern int IORegistryEntryCreateCFProperties(
        uint entry, out IntPtr properties, IntPtr allocator, uint options);

    // =========================================================================
    // IOReport  (bridge.h と同じシグネチャ)
    // =========================================================================

    const string IOReport = "/usr/lib/libIOReport.dylib";

    /// <summary>IOReportCopyChannelsInGroup — 指定グループ/サブグループのチャンネルを取得</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportCopyChannelsInGroup(
        IntPtr group, IntPtr subGroup, ulong c, ulong d, ulong e);

    /// <summary>IOReportMergeChannels — 2つのチャンネル辞書をマージ</summary>
    [DllImport(IOReport)]
    public static extern void IOReportMergeChannels(IntPtr a, IntPtr b, IntPtr nil_);

    /// <summary>IOReportCreateSubscription — チャンネルへのサブスクリプションを作成</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportCreateSubscription(
        IntPtr a, IntPtr channels, out IntPtr dict, ulong d, IntPtr e);

    /// <summary>IOReportCreateSamples — 現在のサンプルを取得</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportCreateSamples(IntPtr subscription, IntPtr channels, IntPtr c);

    /// <summary>IOReportCreateSamplesDelta — 2つのサンプル間の差分を計算</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportCreateSamplesDelta(IntPtr prev, IntPtr next, IntPtr c);

    /// <summary>IOReportChannelGetGroup — チャンネルのグループ名を取得</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportChannelGetGroup(IntPtr channel);

    /// <summary>IOReportChannelGetSubGroup — チャンネルのサブグループ名を取得</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportChannelGetSubGroup(IntPtr channel);

    /// <summary>IOReportChannelGetChannelName — チャンネル名を取得</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportChannelGetChannelName(IntPtr channel);

    /// <summary>IOReportStateGetCount — ステート数を取得</summary>
    [DllImport(IOReport)]
    public static extern int IOReportStateGetCount(IntPtr channel);

    /// <summary>IOReportStateGetNameForIndex — ステート名を取得</summary>
    [DllImport(IOReport)]
    public static extern IntPtr IOReportStateGetNameForIndex(IntPtr channel, int index);

    /// <summary>IOReportStateGetResidency — ステートの滞留時間を取得</summary>
    [DllImport(IOReport)]
    public static extern long IOReportStateGetResidency(IntPtr channel, int index);
}
