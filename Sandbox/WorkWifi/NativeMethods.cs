namespace WorkWifi;

using System.Runtime.InteropServices;

/// <summary>
/// macOS Objective-Cランタイムおよび CoreFoundation の P/Invoke 定義.
/// CoreWLAN フレームワークへのアクセスに使用する.
/// </summary>
internal static partial class NativeMethods
{
    // ---------------------------------------------------------------------------
    // Objective-C ランタイム (/usr/lib/libobjc.A.dylib)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Objective-C クラスオブジェクトを名前で取得する.
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr objc_getClass(string name);

    /// <summary>
    /// Objective-C セレクタを名前で登録/取得する.
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr sel_registerName(string name);

    /// <summary>
    /// Objective-C メッセージ送信 (引数なし、IntPtr 戻り値).
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    internal static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Objective-C メッセージ送信 (IntPtr 引数1つ、IntPtr 戻り値).
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    internal static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    /// <summary>
    /// Objective-C メッセージ送信 (IntPtr 引数2つ、IntPtr 戻り値).
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    internal static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    /// <summary>
    /// Objective-C メッセージ送信 (nint 戻り値、引数なし).
    /// NSInteger を返すプロパティの取得に使用.
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    internal static partial nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Objective-C メッセージ送信 (nuint 戻り値、引数なし).
    /// NSUInteger を返すプロパティの取得に使用.
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    internal static partial nuint objc_msgSend_nuint(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Objective-C メッセージ送信 (bool 引数1つ、IntPtr 戻り値).
    /// supportsSecurity: 等の BOOL 引数メソッドに使用.
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool objc_msgSend_bool(IntPtr receiver, IntPtr selector, nint arg1);

    /// <summary>
    /// Objective-C メッセージ送信 (bool 戻り値、引数なし).
    /// ibss 等の BOOL プロパティ取得に使用.
    /// </summary>
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool objc_msgSend_bool_noargs(IntPtr receiver, IntPtr selector);

    // ---------------------------------------------------------------------------
    // CoreFoundation フレームワーク (/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// CoreFoundation オブジェクトを解放する.
    /// </summary>
    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial void CFRelease(IntPtr cf);

    // ---------------------------------------------------------------------------
    // CoreWLAN フレームワークのロード
    // CoreWLAN は Objective-C フレームワークであるため、dlopen でロードしてクラスを利用可能にする.
    // ---------------------------------------------------------------------------

    [LibraryImport("/usr/lib/libSystem.B.dylib", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr dlopen(string path, int mode);

    /// <summary>
    /// RTLD_LAZY — シンボルを遅延解決する.
    /// </summary>
    internal const int RTLD_LAZY = 0x1;

    // ---------------------------------------------------------------------------
    // CWSecurity 列挙値 (Apple CWSecurity enum)
    // Wi-Fi ネットワークがサポートするセキュリティプロトコルを示す.
    // ---------------------------------------------------------------------------

    /// <summary>セキュリティなし (オープンネットワーク).</summary>
    internal const nint kCWSecurityNone = 0;

    /// <summary>WEP セキュリティ.</summary>
    internal const nint kCWSecurityWEP = 1;

    /// <summary>WPA Personal (PSK).</summary>
    internal const nint kCWSecurityWPAPersonal = 2;

    /// <summary>WPA Personal Mixed (TKIP + AES).</summary>
    internal const nint kCWSecurityWPAPersonalMixed = 3;

    /// <summary>WPA2 Personal (PSK).</summary>
    internal const nint kCWSecurityWPA2Personal = 4;

    /// <summary>WPA2/WPA3 Personal Transitional.</summary>
    internal const nint kCWSecurityPersonalTransition = 5;  // WPA2/WPA3 Transitional

    /// <summary>WPA3 Personal (SAE).</summary>
    internal const nint kCWSecurityWPA3Personal = 6;

    /// <summary>WPA3 Enterprise.</summary>
    internal const nint kCWSecurityWPA3Enterprise = 11;

    /// <summary>Dynamic WEP (802.1X).</summary>
    internal const nint kCWSecurityDynamicWEP = 7;

    /// <summary>WPA Enterprise.</summary>
    internal const nint kCWSecurityWPAEnterprise = 8;

    /// <summary>WPA Enterprise Mixed.</summary>
    internal const nint kCWSecurityWPAEnterpriseMixed = 9;

    /// <summary>WPA2 Enterprise.</summary>
    internal const nint kCWSecurityWPA2Enterprise = 10;

    /// <summary>WPA3 Enterprise (192-bit).</summary>
    internal const nint kCWSecurityWPA3EnterpriseTransition = 12;

    /// <summary>不明なセキュリティタイプ.</summary>
    internal static readonly nint KCWSecurityUnknown = nint.MaxValue;

    // ---------------------------------------------------------------------------
    // CWChannelBand 列挙値 (Apple CWChannelBand enum)
    // Wi-Fi チャンネルの周波数帯域を示す.
    // ---------------------------------------------------------------------------

    /// <summary>不明な帯域.</summary>
    internal const nint kCWChannelBandUnknown = 0;

    /// <summary>2.4 GHz 帯域.</summary>
    internal const nint kCWChannelBand2GHz = 1;

    /// <summary>5 GHz 帯域.</summary>
    internal const nint kCWChannelBand5GHz = 2;

    /// <summary>6 GHz 帯域 (Wi-Fi 6E).</summary>
    internal const nint kCWChannelBand6GHz = 3;

    // ---------------------------------------------------------------------------
    // CWChannelWidth 列挙値 (Apple CWChannelWidth enum)
    // Wi-Fi チャンネルの帯域幅を示す.
    // ---------------------------------------------------------------------------

    /// <summary>不明な帯域幅.</summary>
    internal const nint kCWChannelWidthUnknown = 0;

    /// <summary>20 MHz 帯域幅.</summary>
    internal const nint kCWChannelWidth20MHz = 1;

    /// <summary>40 MHz 帯域幅.</summary>
    internal const nint kCWChannelWidth40MHz = 2;

    /// <summary>80 MHz 帯域幅.</summary>
    internal const nint kCWChannelWidth80MHz = 3;

    /// <summary>160 MHz 帯域幅.</summary>
    internal const nint kCWChannelWidth160MHz = 4;
}
