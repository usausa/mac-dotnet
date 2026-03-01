namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

// TODO delete
#pragma warning disable SA1611
#pragma warning disable SA1615
#pragma warning disable SA1629

// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CA2101
#pragma warning disable CA5392
#pragma warning disable CS8981
internal static class NativeMethods
{
    //------------------------------------------------------------------------
    // Constants / 定数
    //------------------------------------------------------------------------

    // 成功コード (mach/kern_return.h) / Mach kernel success return code
    public const int KERN_SUCCESS = 0;

    // host_statistics64 の flavor 引数 (mach/host_info.h) / Flavor argument for host_statistics64
    public const int HOST_VM_INFO64 = 4;

    // vm_statistics64 のサイズ (natural_t 単位) / Size of vm_statistics64 in natural_t units
    public const int HOST_VM_INFO64_COUNT = 40;

    // host_processor_info の flavor 引数 (mach/processor_info.h) / Flavor argument for host_processor_info
    public const int PROCESSOR_CPU_LOAD_INFO = 2;

    // CPU 状態インデックス (mach/machine.h) / CPU state indices (mach/machine.h)
    public const int CPU_STATE_USER = 0;    // ユーザーモード / User mode
    public const int CPU_STATE_SYSTEM = 1;  // カーネルモード / Kernel mode
    public const int CPU_STATE_IDLE = 2;    // アイドル / Idle
    public const int CPU_STATE_NICE = 3;    // nice 付きユーザーモード / User mode with nice priority
    public const int CPU_STATE_MAX = 4;     // 状態数 / Number of CPU states

    // CFStringEncoding: UTF-8 (CFString.h)
    public const uint kCFStringEncodingUTF8 = 0x08000100;

    // CFNumberType (CFNumber.h) / CFNumber type identifiers
    public const int kCFNumberSInt32Type = 3;   // 32-bit signed integer
    public const int kCFNumberSInt64Type = 4;   // 64-bit signed integer

    // IOPowerSources ディクショナリキー (IOPSKeys.h) / IOPowerSources dictionary keys
    public const string kIOPSNameKey = "Name";
    public const string kIOPSTypeKey = "Type";
    public const string kIOPSTransportTypeKey = "Transport Type";
    public const string kIOPSHardwareSerialNumberKey = "Hardware Serial Number";
    public const string kIOPSIsPresentKey = "Is Present";
    public const string kIOPSPowerSourceStateKey = "Power Source State";
    public const string kIOPSIsChargingKey = "Is Charging";
    public const string kIOPSIsChargedKey = "Is Charged";
    public const string kIOPSCurrentCapacityKey = "Current Capacity";
    public const string kIOPSMaxCapacityKey = "Max Capacity";
    public const string kIOPSTimeToEmptyKey = "Time to Empty";
    public const string kIOPSTimeToFullChargeKey = "Time to Full Charge";
    public const string kIOPSBatteryHealthKey = "BatteryHealth";
    public const string kIOPSBatteryHealthConditionKey = "BatteryHealthCondition";
    public const string kIOPSDesignCycleCountKey = "DesignCycleCount9C";
    public const string kIOPSACPowerValue = "AC Power";  // PowerSourceState value when on AC / AC 電源時の値

    // getfsstat モード (sys/mount.h) / getfsstat mode flags
    public const int MNT_WAIT = 1;    // 同期: ファイルシステム統計の更新を待つ / Synchronous: wait for filesystem stats update
    public const int MNT_NOWAIT = 2;  // 非同期: キャッシュ値を即返す / Asynchronous: return cached values immediately

    // マウントフラグ (sys/mount.h) / Mount flags
    public const uint MNT_RDONLY = 0x00000001;  // 読み取り専用 / Read-only filesystem
    public const uint MNT_LOCAL = 0x00001000;   // ローカルFS / Local filesystem (not network)

    // アドレスファミリー定数 (sys/socket.h) / Address family constants
    public const byte AF_INET = 2;   // IPv4
    public const byte AF_LINK = 18;  // BSD データリンク / BSD data-link layer
    public const byte AF_INET6 = 30; // IPv6

    // インターフェースフラグ定数 (net/if.h) / Interface flag constants (IFF_*)
    public const uint IFF_UP = 0x1;           // インターフェースが有効 / Interface is up
    public const uint IFF_BROADCAST = 0x2;    // ブロードキャスト対応 / Supports broadcast
    public const uint IFF_LOOPBACK = 0x8;     // ループバック / Loopback interface
    public const uint IFF_POINTOPOINT = 0x10; // P2P リンク / Point-to-point link
    public const uint IFF_RUNNING = 0x40;     // リソース割り当て済み / Resources allocated
    public const uint IFF_MULTICAST = 0x8000; // マルチキャスト対応 / Supports multicast

    // インターフェース種別定数 (net/if_types.h) / Interface type constants
    public const byte IFT_ETHER = 0x06;      // 有線 Ethernet / Wired Ethernet
    public const byte IFT_LOOP = 0x18;       // ループバック / Loopback
    public const byte IFT_GIF = 0x37;        // 汎用トンネル / Generic tunnel
    public const byte IFT_STF = 0x39;        // 6to4 トンネル / 6-to-4 tunnel
    public const byte IFT_IEEE80211 = 0x47;  // Wi-Fi (IEEE 802.11)
    public const byte IFT_BRIDGE = 0xD1;     // ブリッジ / Bridge
    public const byte IFT_CELLULAR = 0xFF;   // セルラー / Cellular

    // inet_ntop 用バッファサイズ定数 (netinet/in.h) / Buffer size constants for inet_ntop
    public const uint INET_ADDRSTRLEN = 16;   // IPv4 アドレス文字列の最大長 / Max length of an IPv4 address string
    public const uint INET6_ADDRSTRLEN = 46;  // IPv6 アドレス文字列の最大長 / Max length of an IPv6 address string

    // proc_listpids type (sys/proc_info.h) / Type argument for proc_listpids
    public const uint PROC_ALL_PIDS = 1;  // すべてのプロセス / All processes

    // proc_pidinfo flavor (sys/proc_info.h) / Flavor arguments for proc_pidinfo
    public const int PROC_PIDTBSDINFO = 3;  // BSD プロセス情報 (proc_bsdinfo) / BSD process info
    public const int PROC_PIDTASKINFO = 4;  // タスク情報 (proc_taskinfo) / Task info

    // proc_pidpath バッファサイズ (sys/proc_info.h) / Buffer size for proc_pidpath
    public const uint PROC_PIDPATHINFO_MAXSIZE = 4096;

    // SMC selector: IOConnectCallStructMethod の selector 引数 / Selector for IOConnectCallStructMethod
    public const uint KERNEL_INDEX_SMC = 2;

    // SMC コマンド / SMC command codes
    public const byte SMC_CMD_READ_BYTES = 5;    // キー値をバイト列として読み取る / Read key value as bytes
    public const byte SMC_CMD_READ_KEYINFO = 9;  // キーのメタ情報を読み取る / Read key metadata
    public const byte SMC_CMD_READ_INDEX = 8;    // インデックスでキーを読み取る / Read key by index

    // SMC データ型定数: 4 文字をビッグエンディアン uint32 にエンコード / SMC data type codes (4-char big-endian uint32)
    public const uint DATA_TYPE_FLT = 0x666C7420;   // "flt " — 32-bit IEEE 754 float
    public const uint DATA_TYPE_SP78 = 0x73703738;  // "sp78" — signed fixed-point Q7.8 (温度 / temperature)
    public const uint DATA_TYPE_FPE2 = 0x66706532;  // "fpe2" — unsigned fixed-point Qn.2 (RPM 等 / e.g. fan RPM)
    public const uint DATA_TYPE_IOFT = 0x696F6674;  // "ioft" — signed fixed-point Q16.16
    public const uint DATA_TYPE_UI8 = 0x75693820;   // "ui8 " — 8-bit unsigned integer
    public const uint DATA_TYPE_UI16 = 0x75693136;  // "ui16" — 16-bit unsigned integer
    public const uint DATA_TYPE_UI32 = 0x75693332;  // "ui32" — 32-bit unsigned integer

    //------------------------------------------------------------------------
    // Struct
    //------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct timeval
    {
        public long tv_sec;
        public long tv_usec;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct timeval_boot
    {
        public long tv_sec;
        public int tv_usec;
        private readonly int _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct vm_statistics64
    {
        public uint free_count;
        public uint active_count;
        public uint inactive_count;
        public uint wire_count;
        public ulong zero_fill_count;
        public ulong reactivations;
        public ulong pageins;
        public ulong pageouts;
        public ulong faults;
        public ulong cow_faults;
        public ulong lookups;
        public ulong hits;
        public ulong purges;
        public uint purgeable_count;
        public uint speculative_count;
        public ulong decompressions;
        public ulong compressions;
        public ulong swapins;
        public ulong swapouts;
        public uint compressor_page_count;
        public uint throttled_count;
        public uint external_page_count;
        public uint internal_page_count;
        public ulong total_uncompressed_pages_in_compressor;
        public ulong swapped_count;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct xsw_usage
    {
        public ulong xsu_total;
        public ulong xsu_avail;
        public ulong xsu_used;
        public int xsu_pagesize;
        public int xsu_encrypted;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct fsid_t
    {
        public int val0;
        public int val1;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct statfs
    {
        public uint f_bsize;
        public int f_iosize;
        public ulong f_blocks;
        public ulong f_bfree;
        public ulong f_bavail;
        public ulong f_files;
        public ulong f_ffree;
        public fsid_t f_fsid;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct ifaddrs
    {
        public IntPtr ifa_next;
        public IntPtr ifa_name;
        public uint ifa_flags;
        public IntPtr ifa_addr;
        public IntPtr ifa_netmask;
        public IntPtr ifa_dstaddr;
        public IntPtr ifa_data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct sockaddr
    {
        public byte sa_len;
        public byte sa_family;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct sockaddr_dl
    {
        public byte sdl_len;
        public byte sdl_family;
        public ushort sdl_index;
        public byte sdl_type;
        public byte sdl_nlen;
        public byte sdl_alen;
        public byte sdl_slen;

        public const int DataOffset = 8;
    }

    public static class sockaddr_in
    {
        public const int AddrOffset = 4;
    }

    public static class sockaddr_in6
    {
        public const int AddrOffset = 8;
        public const int ScopeIdOffset = 24;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct if_data
    {
        public byte ifi_type;
        public byte ifi_typelen;
        public byte ifi_physical;
        public byte ifi_addrlen;
        public byte ifi_hdrlen;
        public byte ifi_recvquota;
        public byte ifi_xmitquota;
        public byte ifi_unused1;
        public uint ifi_mtu;
        public uint ifi_metric;
        public uint ifi_baudrate;
        public uint ifi_ipackets;
        public uint ifi_ierrors;
        public uint ifi_opackets;
        public uint ifi_oerrors;
        public uint ifi_collisions;
        public uint ifi_ibytes;
        public uint ifi_obytes;
        public uint ifi_imcasts;
        public uint ifi_omcasts;
        public uint ifi_iqdrops;
        public uint ifi_noproto;
        public uint ifi_recvtiming;
        public uint ifi_xmittiming;
        public long ifi_lastchange_tv_sec;
        public int ifi_lastchange_tv_usec;
        private int ifi_lastchange_pad;
        public uint ifi_unused2;
        public uint ifi_hwassist;
        public uint ifi_reserved1;
        public uint ifi_reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct proc_bsdinfo
    {
        public uint pbi_flags;
        public uint pbi_status;
        public uint pbi_xstatus;
        public uint pbi_pid;
        public uint pbi_ppid;
        public uint pbi_uid;
        public uint pbi_gid;
        public uint pbi_ruid;
        public uint pbi_rgid;
        public uint pbi_svuid;
        public uint pbi_svgid;
        public uint rfu_1;
        public fixed byte pbi_comm[16];
        public fixed byte pbi_name[32];
        public uint pbi_nfiles;
        public uint pbi_pgid;
        public uint pbi_pjobc;
        public uint e_tdev;
        public uint e_tpgid;
        public int pbi_nice;
        public ulong pbi_start_tvsec;
        public ulong pbi_start_tvusec;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct proc_taskinfo
    {
        public ulong pti_virtual_size;
        public ulong pti_resident_size;
        public ulong pti_total_user;
        public ulong pti_total_system;
        public ulong pti_threads_user;
        public ulong pti_threads_system;
        public int pti_policy;
        public int pti_faults;
        public int pti_pageins;
        public int pti_cow_faults;
        public int pti_messages_sent;
        public int pti_messages_received;
        public int pti_syscalls_mach;
        public int pti_syscalls_unix;
        public int pti_csw;
        public int pti_threadnum;
        public int pti_numrunning;
        public int pti_priority;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SMCKeyData_keyInfo_t
    {
        public uint dataSize;
        public uint dataType;
        public byte dataAttributes;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal unsafe struct SMCKeyData_t
    {
        [FieldOffset(0)]
        public uint key;

        [FieldOffset(28)]
        public SMCKeyData_keyInfo_t keyInfo;

        [FieldOffset(40)]
        public byte result;

        [FieldOffset(41)]
        public byte status;

        [FieldOffset(42)]
        public byte data8;

        [FieldOffset(44)]
        public uint data32;

        [FieldOffset(48)]
        public fixed byte bytes[32];
    }

    //------------------------------------------------------------------------
    // Mach
    //------------------------------------------------------------------------

    [DllImport("libSystem.dylib")]
    public static extern uint mach_host_self();

    [DllImport("libSystem.dylib")]
    public static extern uint task_self_trap();

    [DllImport("libSystem.dylib")]
    public static extern int host_processor_info(uint host, int flavor, out int processorCount, out IntPtr processorInfo, out int processorInfoCnt);

    [DllImport("libSystem.dylib")]
    public static extern int vm_deallocate(uint targetTask, IntPtr address, IntPtr size);

    [DllImport("libSystem.dylib")]
    public static extern unsafe int host_statistics64(uint host_priv, int flavor, vm_statistics64* host_info_out, ref int host_info_outCnt);

    [DllImport("libSystem.dylib")]
    public static extern int host_page_size(uint host, out nuint page_size);

    //------------------------------------------------------------------------
    // libc
    //------------------------------------------------------------------------

    [DllImport("libc")]
    public static extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)] string name, ref timeval oldp, ref int oldlen, IntPtr newp, int newlen);

    [DllImport("libc")]
    public static extern unsafe int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)] string name, void* oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

    [DllImport("libc")]
    public static extern unsafe int getloadavg(double* loadavg, int nelem);

    [DllImport("libc")]
    public static extern unsafe int getfsstat(statfs* buf, int bufsize, int mode);

    [DllImport("libc", EntryPoint = "statfs")]
    public static extern unsafe int statfs_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path, statfs* buf);

    [DllImport("libc")]
    public static extern int getifaddrs(out IntPtr ifap);

    [DllImport("libc")]
    public static extern void freeifaddrs(IntPtr ifa);

    [DllImport("libc")]
    public static extern unsafe IntPtr inet_ntop(int af, void* src, byte* dst, uint size);

    //------------------------------------------------------------------------
    // libproc
    //------------------------------------------------------------------------

    [DllImport("libproc")]
    public static extern unsafe int proc_listpids(uint type, uint typeinfo, int* buffer, int buffersize);

    [DllImport("libproc")]
    public static extern unsafe int proc_pidinfo(int pid, int flavor, ulong arg, void* buffer, int buffersize);

    [DllImport("libproc")]
    public static extern unsafe int proc_pidpath(int pid, byte* buffer, uint buffersize);

    //------------------------------------------------------------------------
    // CoreFoundation
    //------------------------------------------------------------------------

    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundationLib)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationLib)]
    public static extern long CFArrayGetCount(IntPtr theArray);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr, uint encoding);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFStringGetCStringPtr(IntPtr theString, uint encoding);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundationLib)]
    public static extern unsafe bool CFStringGetCString(IntPtr theString, byte* buffer, IntPtr bufferSize, uint encoding);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, out int valuePtr);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, ref long valuePtr);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(IntPtr boolean);

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFGetTypeID(IntPtr cf);

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFStringGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFNumberGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFDictionaryGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFDataGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDataGetLength(IntPtr theData);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr theData);

    //------------------------------------------------------------------------
    // IOKit
    //------------------------------------------------------------------------

    private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string IOReportLib = "/usr/lib/libIOReport.dylib";

    [DllImport(IOKitLib)]
    public static extern IntPtr IOPSCopyPowerSourcesInfo();

    [DllImport(IOKitLib)]
    public static extern IntPtr IOPSCopyPowerSourcesList(IntPtr blob);

    [DllImport(IOKitLib)]
    public static extern IntPtr IOPSGetPowerSourceDescription(IntPtr blob, IntPtr ps);

    [DllImport(IOKitLib)]
    public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, ref IntPtr existing);

    [DllImport(IOKitLib)]
    public static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport(IOKitLib)]
    public static extern uint IOIteratorNext(IntPtr iterator);

    [DllImport(IOKitLib)]
    public static extern int IOObjectRelease(IntPtr @object);

    [DllImport(IOKitLib)]
    public static extern int IOObjectRelease(uint @object);

    [DllImport(IOKitLib)]
    public static extern IntPtr IOServiceMatching([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(IOKitLib)]
    public static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

    [DllImport(IOKitLib)]
    public static extern int IOServiceOpen(uint service, uint owningTask, uint type, out uint connect);

    [DllImport(IOKitLib)]
    public static extern int IOServiceClose(uint connect);

    [DllImport(IOKitLib)]
    public static extern unsafe int IOConnectCallStructMethod(uint connection, uint selector, void* inputStruct, nuint inputStructCnt, void* outputStruct, nuint* outputStructCnt);

    [DllImport(IOKitLib)]
    public static extern int IORegistryEntryCreateCFProperties(uint entry, out IntPtr properties, IntPtr allocator, uint options);

    [DllImport(IOKitLib)]
    public static extern unsafe int IORegistryEntryGetName(uint entry, byte* name);

    [DllImport(IOKitLib)]
    public static extern unsafe int IOObjectGetClass(uint @object, byte* className);

    [DllImport(IOKitLib)]
    public static extern int IORegistryEntryGetParentEntry(uint entry, [MarshalAs(UnmanagedType.LPUTF8Str)] string plane, out uint parent);

    [DllImport(IOKitLib)]
    public static extern IntPtr IOPSCopyExternalPowerAdapterDetails();

    //------------------------------------------------------------------------
    // SystemConfiguration
    //------------------------------------------------------------------------

    private const string SystemConfigurationLib = "/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration";

    /// <summary>ユーザーが設定可能なハードウェアネットワークインターフェースの一覧を CFArrayRef で返す。呼び出し元が CFRelease する必要がある</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkInterfaceCopyAll();

    /// <summary>インターフェースの BSD 名 (例: "en0") を CFStringRef で返す。所有権は呼び出し元に移らないため CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkInterfaceGetBSDName(IntPtr networkInterface);

    /// <summary>インターフェースのローカライズされた表示名 (例: "Ethernet"、"Wi-Fi") を CFStringRef で返す。CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkInterfaceGetLocalizedDisplayName(IntPtr networkInterface);

    /// <summary>SystemConfiguration の設定ファイルを開く。戻り値は CFRelease が必要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCPreferencesCreate(IntPtr allocator, IntPtr name, IntPtr prefsID);

    /// <summary>System Settings のネットワーク設定に登録されているネットワークサービスの一覧を CFArrayRef で返す。CFRelease が必要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceCopyAll(IntPtr prefs);

    /// <summary>ネットワークサービスのユーザー表示名 (例: "Ethernet"、"Wi-Fi") を CFStringRef で返す。CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceGetName(IntPtr service);

    /// <summary>ネットワークサービスに紐付くハードウェアインターフェースを返す。CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceGetInterface(IntPtr service);

    /// <summary>インターフェースの SC レベルの種別を CFStringRef で返す。例: "Ethernet"、"IEEE80211"、"Bridge"。CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkInterfaceGetInterfaceType(IntPtr networkInterface);

    /// <summary>ネットワークサービスが有効かどうかを返す (System Settings で有効/無効切り替え可能)</summary>
    [DllImport(SystemConfigurationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SCNetworkServiceGetEnabled(IntPtr service);

    /// <summary>ネットワークサービスの UUID 文字列を CFStringRef で返す。CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceGetServiceID(IntPtr service);

    /// <summary>SC preferences の指定パスの値 (CFPropertyListRef) を返す。CFRelease 不要</summary>
    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCPreferencesPathGetValue(IntPtr prefs, IntPtr path);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportCopyChannelsInGroup(IntPtr group, IntPtr subgroup, ulong a, ulong b, ulong c);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportCreateSubscription(IntPtr a, IntPtr channels, out IntPtr b, ulong c, IntPtr d);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportCreateSamples(IntPtr subscription, IntPtr channels, IntPtr a);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportChannelGetGroup(IntPtr channel);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportChannelGetChannelName(IntPtr channel);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportChannelGetUnitLabel(IntPtr channel);

    [DllImport(IOReportLib)]
    public static extern long IOReportSimpleGetIntegerValue(IntPtr channel, int idx);

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFArrayGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDictionaryCreateMutableCopy(IntPtr allocator, IntPtr capacity, IntPtr theDict);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, ref double valuePtr);

    // Additional IOPowerSources Keys
    public const string kIOPSPowerAdapterWattsKey = "Watts";

    // CFNumberType for double
    public const int kCFNumberFloat64Type = 6;

    //------------------------------------------------------------------------
    // Helper / ヘルパーメソッド
    //------------------------------------------------------------------------

    /// <summary>
    /// CFStringRef をマネージ文字列に変換する。
    /// まず CFStringGetCStringPtr で高速パスを試み、失敗した場合はバッファを確保して変換する。
    /// cfString が IntPtr.Zero の場合は null を返す。
    /// <para>
    /// Converts a CFStringRef to a managed string.
    /// Tries the fast path via CFStringGetCStringPtr first; falls back to buffer allocation if needed.
    /// Returns null if cfString is IntPtr.Zero.
    /// </para>
    /// </summary>
    public static unsafe string? CfStringToManaged(IntPtr cfString)
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

        var bufSize = (length * 4) + 1;
        var buf = stackalloc byte[(int)bufSize];
        return CFStringGetCString(cfString, buf, bufSize, kCFStringEncodingUTF8)
            ? Marshal.PtrToStringUTF8((IntPtr)buf)
            : null;
    }

    /// <summary>
    /// sysctlbyname で 32 ビット整数値を取得する。取得失敗時は 0 を返す。
    /// <para>Reads a 32-bit integer value via sysctlbyname. Returns 0 on failure.</para>
    /// </summary>
    public static unsafe int GetSystemControlInt32(string name)
    {
        int value;
        var len = (IntPtr)sizeof(int);
        return sysctlbyname(name, &value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
    }

    /// <summary>
    /// sysctlbyname で 64 ビット符号付き整数値を取得する。取得失敗時は 0 を返す。
    /// <para>Reads a 64-bit signed integer value via sysctlbyname. Returns 0 on failure.</para>
    /// </summary>
    public static unsafe long GetSystemControlInt64(string name)
    {
        long value;
        var len = (IntPtr)sizeof(long);
        return sysctlbyname(name, &value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
    }

    /// <summary>
    /// sysctlbyname で 64 ビット符号なし整数値を取得する。取得失敗時は 0 を返す。
    /// <para>Reads a 64-bit unsigned integer value via sysctlbyname. Returns 0 on failure.</para>
    /// </summary>
    public static unsafe ulong GetSystemControlUInt64(string name)
    {
        ulong value;
        var len = (IntPtr)sizeof(ulong);
        return sysctlbyname(name, &value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
    }

    /// <summary>
    /// sysctlbyname で文字列値を取得する。1024 バイトを超える場合は null を返す。
    /// <para>Reads a string value via sysctlbyname. Returns null if the value exceeds 1024 bytes.</para>
    /// </summary>
    public static unsafe string? GetSystemControlString(string name)
    {
        var len = IntPtr.Zero;
        if ((sysctlbyname(name, null, ref len, IntPtr.Zero, 0) != 0) || (len <= 0))
        {
            return null;
        }

        if (len > 1024)
        {
            return null;
        }

        var allocatedSize = len;
        var buffer = stackalloc byte[(int)allocatedSize];
        return sysctlbyname(name, buffer, ref len, IntPtr.Zero, 0) == 0
            ? Marshal.PtrToStringUTF8((IntPtr)buffer)
            : null;
    }

    //------------------------------------------------------------------------
    // IOKit property helpers / IOKit プロパティヘルパー
    //------------------------------------------------------------------------

    /// <summary>
    /// IOKit オブジェクトのクラス名を取得する。失敗時は null を返す。
    /// <para>Returns the IOKit class name of an object. Returns null on failure.</para>
    /// </summary>
    public static unsafe string? GetIokitClassName(uint @object)
    {
        const int IO_NAME_LENGTH = 128;
        var buf = stackalloc byte[IO_NAME_LENGTH];
        return IOObjectGetClass(@object, buf) == KERN_SUCCESS
            ? Marshal.PtrToStringUTF8((IntPtr)buf)
            : null;
    }

    /// <summary>
    /// IOKit エントリから CFString プロパティを取得してマネージ文字列に変換する。
    /// プロパティが存在しないか CFString 以外の型の場合は null を返す。
    /// <para>
    /// Retrieves a CFString property from an IOKit entry and converts it to a managed string.
    /// Returns null if the property is absent or is not a CFString.
    /// </para>
    /// </summary>
    public static unsafe string? GetIokitString(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return CFGetTypeID(val) == CFStringGetTypeID() ? CfStringToManaged(val) : null;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>
    /// IOKit エントリから CFBoolean プロパティを取得する。
    /// プロパティが存在しないか CFBoolean 以外の型の場合は false を返す。
    /// <para>
    /// Retrieves a CFBoolean property from an IOKit entry.
    /// Returns false if the property is absent or is not a CFBoolean.
    /// </para>
    /// </summary>
    public static bool GetIokitBoolean(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return false;
            }

            var result = CFBooleanGetValue(val);
            CFRelease(val);
            return result;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>
    /// IOKit エントリから CFNumber プロパティを 64 ビット整数として取得する。
    /// プロパティが存在しないか CFNumber 以外の型の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFNumber property from an IOKit entry as a 64-bit integer.
    /// Returns 0 if the property is absent or is not a CFNumber.
    /// </para>
    /// </summary>
    public static long GetIokitNumber(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                if (CFGetTypeID(val) != CFNumberGetTypeID())
                {
                    return 0;
                }

                long result = 0;
                CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
                return result;
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>
    /// IOKit エントリから CFData プロパティを取得し、先頭 4 バイトをリトルエンディアン uint32 として返す。
    /// データが 4 バイト未満の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFData property from an IOKit entry and interprets the first 4 bytes as a little-endian uint32.
    /// Returns 0 if the data is shorter than 4 bytes.
    /// </para>
    /// </summary>
    public static uint GetIokitDataUInt32LE(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                if (CFGetTypeID(val) != CFDataGetTypeID())
                {
                    return 0;
                }

                var len = CFDataGetLength(val);
                if (len < 4)
                {
                    return 0;
                }

                var ptr = CFDataGetBytePtr(val);
                return (uint)(Marshal.ReadByte(ptr, 0)
                    | (Marshal.ReadByte(ptr, 1) << 8)
                    | (Marshal.ReadByte(ptr, 2) << 16)
                    | (Marshal.ReadByte(ptr, 3) << 24));
            }
            finally
            {
                CFRelease(val);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>IOKit エントリから CFDictionary プロパティを取得する。戻り値が非 Zero の場合は呼び出し元が CFRelease する必要がある</summary>
    public static IntPtr GetIokitDictionary(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            var val = IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
            if (val == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (CFGetTypeID(val) != CFDictionaryGetTypeID())
            {
                CFRelease(val);
                return IntPtr.Zero;
            }

            return val;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>
    /// CFDictionary から CFNumber 値を 64 ビット整数として取得する。
    /// キーが存在しないか CFNumber 以外の型の場合は 0 を返す。
    /// <para>
    /// Retrieves a CFNumber value from a CFDictionary as a 64-bit integer.
    /// Returns 0 if the key is absent or the value is not a CFNumber.
    /// </para>
    /// </summary>
    public static long GetIokitDictNumber(IntPtr dict, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            if (CFGetTypeID(val) != CFNumberGetTypeID())
            {
                return 0;
            }

            long result = 0;
            CFNumberGetValue(val, kCFNumberSInt64Type, ref result);
            return result;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>
    /// CFDictionary から CFString 値をマネージ文字列として取得する。
    /// キーが存在しないか CFString 以外の型の場合は null を返す。
    /// <para>
    /// Retrieves a CFString value from a CFDictionary as a managed string.
    /// Returns null if the key is absent or the value is not a CFString.
    /// </para>
    /// </summary>
    public static string? GetIokitDictString(IntPtr dict, string key)
    {
        var cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
        if (cfKey == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, cfKey);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            return CFGetTypeID(val) == CFStringGetTypeID() ? CfStringToManaged(val) : null;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }
}
