namespace MacDotNet.SystemInfo;

using System.Runtime.InteropServices;

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
    // Constants
    //------------------------------------------------------------------------

    // Mach kernel success return code (mach/kern_return.h)
    public const int KERN_SUCCESS = 0;

    // Flavor argument for host_statistics64 (mach/host_info.h)
    public const int HOST_VM_INFO64 = 4;

    // Size of vm_statistics64 in natural_t units
    public const int HOST_VM_INFO64_COUNT = 40;

    // Flavor argument for host_processor_info (mach/processor_info.h)
    public const int PROCESSOR_CPU_LOAD_INFO = 2;

    // CPU state indices (mach/machine.h)
    public const int CPU_STATE_USER = 0;    // User mode
    public const int CPU_STATE_SYSTEM = 1;  // Kernel mode
    public const int CPU_STATE_IDLE = 2;    // Idle
    public const int CPU_STATE_NICE = 3;    // User mode with nice priority
    public const int CPU_STATE_MAX = 4;     // Number of CPU states

    // CFStringEncoding: UTF-8 (CFString.h)
    public const uint kCFStringEncodingUTF8 = 0x08000100;

    // CFNumber type identifiers (CFNumber.h)
    public const int kCFNumberSInt32Type = 3;   // 32-bit signed integer
    public const int kCFNumberSInt64Type = 4;   // 64-bit signed integer

    // getfsstat mode flags (sys/mount.h)
    public const int MNT_NOWAIT = 2;  // Asynchronous: return cached values immediately

    // Address family constants (sys/socket.h)
    public const byte AF_LINK = 18;  // BSD data-link layer

    // Type argument for proc_listpids (sys/proc_info.h)
    public const uint PROC_ALL_PIDS = 1;  // All processes

    // Flavor arguments for proc_pidinfo (sys/proc_info.h)
    public const int PROC_PIDTBSDINFO = 3;  // BSD process info (proc_bsdinfo)
    public const int PROC_PIDTASKINFO = 4;  // Task info (proc_taskinfo)
    public const int RUSAGE_INFO_V2 = 2;

    // BSD process status values (sys/proc.h)
    public const uint SIDL = 1;
    public const uint SRUN = 2;
    public const uint SSLEEP = 3;
    public const uint SSTOP = 4;
    public const uint SZOMB = 5;

    // Buffer size for proc_pidpath (sys/proc_info.h)
    public const uint PROC_PIDPATHINFO_MAXSIZE = 4096;

    // Selector for IOConnectCallStructMethod
    public const uint KERNEL_INDEX_SMC = 2;

    // SMC command codes
    public const byte SMC_CMD_READ_BYTES = 5;    // Read key value as bytes
    public const byte SMC_CMD_READ_KEYINFO = 9;  // Read key metadata
    public const byte SMC_CMD_READ_INDEX = 8;    // Read key by index

    // SMC data type codes (4-char big-endian uint32)
    public const uint DATA_TYPE_FLT = 0x666C7420;   // "flt " — 32-bit IEEE 754 float
    public const uint DATA_TYPE_SP78 = 0x73703738;  // "sp78" — signed fixed-point Q7.8 (temperature)
    public const uint DATA_TYPE_FPE2 = 0x66706532;  // "fpe2" — unsigned fixed-point Qn.2 (e.g. fan RPM)
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
    internal struct rusage_info_v2
    {
        public byte ri_uuid0;
        public byte ri_uuid1;
        public byte ri_uuid2;
        public byte ri_uuid3;
        public byte ri_uuid4;
        public byte ri_uuid5;
        public byte ri_uuid6;
        public byte ri_uuid7;
        public byte ri_uuid8;
        public byte ri_uuid9;
        public byte ri_uuid10;
        public byte ri_uuid11;
        public byte ri_uuid12;
        public byte ri_uuid13;
        public byte ri_uuid14;
        public byte ri_uuid15;
        public ulong ri_user_time;
        public ulong ri_system_time;
        public ulong ri_pkg_idle_wkups;
        public ulong ri_interrupt_wkups;
        public ulong ri_pageins;
        public ulong ri_wired_size;
        public ulong ri_resident_size;
        public ulong ri_phys_footprint;
        public ulong ri_proc_start_abstime;
        public ulong ri_proc_exit_abstime;
        public ulong ri_child_user_time;
        public ulong ri_child_system_time;
        public ulong ri_child_pkg_idle_wkups;
        public ulong ri_child_interrupt_wkups;
        public ulong ri_child_pageins;
        public ulong ri_child_elapsed_abstime;
        public ulong ri_diskio_bytesread;
        public ulong ri_diskio_byteswritten;
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
    public static extern uint mach_task_self();

    [DllImport("libSystem.dylib")]
    public static extern int mach_port_deallocate(uint task, uint name);

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

    // ReSharper disable once StringLiteralTypo
    [DllImport("libc", EntryPoint = "statfs")]
    public static extern unsafe int statfs_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path, statfs* buf);

    [DllImport("libc")]
    public static extern int getifaddrs(out IntPtr ifap);

    [DllImport("libc")]
    public static extern void freeifaddrs(IntPtr ifa);

    //------------------------------------------------------------------------
    // libproc
    //------------------------------------------------------------------------

    [DllImport("libproc")]
    public static extern unsafe int proc_listpids(uint type, uint typeinfo, int* buffer, int buffersize);

    [DllImport("libproc")]
    public static extern unsafe int proc_pidinfo(int pid, int flavor, ulong arg, void* buffer, int buffersize);

    [DllImport("libproc")]
    public static extern unsafe int proc_pidpath(int pid, byte* buffer, uint buffersize);

    [DllImport("libproc")]
    public static extern unsafe int proc_pid_rusage(int pid, int flavor, rusage_info_v2* buffer);

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
    public static extern void CFDictionarySetValue(IntPtr theDict, IntPtr key, IntPtr value);

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
    public static extern bool CFNumberGetValue(IntPtr number, int theType, ref ulong valuePtr);

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

    [DllImport(IOKitLib)]
    public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, ref IntPtr existing);

    [DllImport(IOKitLib)]
    public static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport(IOKitLib)]
    public static extern uint IOIteratorNext(IntPtr iterator);

    [DllImport(IOKitLib)]
    public static extern int IORegistryEntryGetChildIterator(uint entry, [MarshalAs(UnmanagedType.LPUTF8Str)] string plane, out IntPtr iterator);

    [DllImport(IOKitLib)]
    public static extern int IOObjectRelease(IntPtr @object);

    [DllImport(IOKitLib)]
    public static extern int IOObjectRelease(uint @object);

    [DllImport(IOKitLib)]
    public static extern IntPtr IOServiceMatching([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(IOKitLib)]
    public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, out uint existing);

    [DllImport(IOKitLib)]
    public static extern uint IOIteratorNext(uint iterator);

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
    public static extern int IORegistryEntryGetRegistryEntryID(uint entry, out ulong entryID);

    //------------------------------------------------------------------------
    // IOReport
    //------------------------------------------------------------------------

    private const string IOReportLib = "/usr/lib/libIOReport.dylib";

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

    [DllImport(IOReportLib)]
    public static extern void IOReportMergeChannels(IntPtr a, IntPtr b, IntPtr nil_);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportChannelGetSubGroup(IntPtr channel);

    [DllImport(IOReportLib)]
    public static extern int IOReportStateGetCount(IntPtr channel);

    [DllImport(IOReportLib)]
    public static extern IntPtr IOReportStateGetNameForIndex(IntPtr channel, int index);

    [DllImport(IOReportLib)]
    public static extern long IOReportStateGetResidency(IntPtr channel, int index);

    [DllImport(CoreFoundationLib)]
    public static extern nuint CFArrayGetTypeID();

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDictionaryCreateMutableCopy(IntPtr allocator, IntPtr capacity, IntPtr theDict);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int theType, ref double valuePtr);

    //------------------------------------------------------------------------
    // SystemConfiguration
    //------------------------------------------------------------------------

    private const string SystemConfigurationLib = "/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration";

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkInterfaceGetBSDName(IntPtr networkInterface);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCPreferencesCreate(IntPtr allocator, IntPtr name, IntPtr prefsID);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceCopyAll(IntPtr prefs);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceGetName(IntPtr service);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceGetInterface(IntPtr service);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkInterfaceGetInterfaceType(IntPtr networkInterface);

    [DllImport(SystemConfigurationLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SCNetworkServiceGetEnabled(IntPtr service);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCNetworkServiceGetServiceID(IntPtr service);

    [DllImport(SystemConfigurationLib)]
    public static extern IntPtr SCPreferencesPathGetValue(IntPtr prefs, IntPtr path);

    //------------------------------------------------------------------------
    // Helper
    //------------------------------------------------------------------------

    public static unsafe int GetSystemControlInt32(string name)
    {
        int value;
        var len = (IntPtr)sizeof(int);
        return sysctlbyname(name, &value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
    }

    public static unsafe long GetSystemControlInt64(string name)
    {
        long value;
        var len = (IntPtr)sizeof(long);
        return sysctlbyname(name, &value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
    }

    public static unsafe ulong GetSystemControlUInt64(string name)
    {
        ulong value;
        var len = (IntPtr)sizeof(ulong);
        return sysctlbyname(name, &value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
    }

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
        return sysctlbyname(name, buffer, ref len, IntPtr.Zero, 0) == 0 ? Marshal.PtrToStringUTF8((IntPtr)buffer) : null;
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
