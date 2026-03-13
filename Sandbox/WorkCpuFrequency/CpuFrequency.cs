using System.Runtime.InteropServices;
using static CpuFrequencySample.NativeBindings;

namespace CpuFrequencySample;

/// <summary>
/// CPU全コアの周波数を管理するクラス。
/// Update() を呼び出すと IOReport を Open/Close してサンプリングを行い、
/// 各 CpuCoreFrequency の Frequency プロパティを最新値に更新する。
///
/// ネイティブハンドルはメンバに持たず、Update() 内で完結させる。
/// 前回サンプルとの差分は各コアが long[] でレジデンシー値を保持することで実現する。
/// </summary>
public sealed class CpuFrequency
{
    private readonly int[] _eCoreFreqs;
    private readonly int[] _pCoreFreqs;
    private readonly CpuCoreFrequency[] _eCores;
    private readonly CpuCoreFrequency[] _pCores;
    private bool _initialized;

    /// <summary>コアごとの周波数情報リスト。Update() により値が更新される。</summary>
    public IReadOnlyList<CpuCoreFrequency> Cores { get; }

    /// <summary>E-Core の最大周波数 (MHz)</summary>
    public int MaxECoreFrequency => _eCoreFreqs.Length > 0 ? _eCoreFreqs[^1] : 0;

    /// <summary>P-Core の最大周波数 (MHz)</summary>
    public int MaxPCoreFrequency => _pCoreFreqs.Length > 0 ? _pCoreFreqs[^1] : 0;

    /// <summary>
    /// コンストラクタ。周波数テーブルとコア一覧を構築する。
    /// IOReport の初期化は Update() 初回呼び出し時に行う。
    /// </summary>
    public CpuFrequency(string cpuName)
    {
        var tables = GetFrequencyTables(cpuName);
        if (tables == null)
            throw new InvalidOperationException("周波数テーブルの取得に失敗しました。");

        _eCoreFreqs = tables.Value.eCoreFreqs;
        _pCoreFreqs = tables.Value.pCoreFreqs;

        if (_eCoreFreqs.Length == 0 && _pCoreFreqs.Length == 0)
            throw new InvalidOperationException("周波数テーブルが空です。Apple Silicon Mac で実行してください。");

        int eCoreCount = GetSysctlInt("hw.perflevel1.logicalcpu") ?? 0;
        int pCoreCount = GetSysctlInt("hw.perflevel0.logicalcpu") ?? 0;

        _eCores = new CpuCoreFrequency[eCoreCount];
        _pCores = new CpuCoreFrequency[pCoreCount];
        for (int i = 0; i < eCoreCount; i++)
            _eCores[i] = new CpuCoreFrequency(i, CpuCoreType.Efficiency);
        for (int i = 0; i < pCoreCount; i++)
            _pCores[i] = new CpuCoreFrequency(i, CpuCoreType.Performance);

        var allCores = new CpuCoreFrequency[eCoreCount + pCoreCount];
        _eCores.CopyTo(allCores, 0);
        _pCores.CopyTo(allCores, eCoreCount);
        Cores = allCores;
    }

    // =====================================================================
    // Update
    // =====================================================================

    /// <summary>
    /// IOReport を Open して各コアの Frequency を更新し、Close する。
    /// スリープは行わない。呼び出し側が間隔を制御する (例: Thread.Sleep(1000))。
    /// </summary>
    public void Update()
    {
        var channels = GetChannels();
        if (channels == IntPtr.Zero) return;

        var subscription = IOReportCreateSubscription(IntPtr.Zero, channels, out IntPtr dict, 0, IntPtr.Zero);
        if (dict != IntPtr.Zero) CFRelease(dict);

        if (subscription == IntPtr.Zero)
        {
            CFRelease(channels);
            return;
        }

        var sample = IOReportCreateSamples(subscription, channels, IntPtr.Zero);
        if (sample != IntPtr.Zero)
        {
            var key = CreateCFString("IOReportChannels");
            if (CFDictionaryGetValueIfPresent(sample, key, out IntPtr items))
            {
                if (!_initialized)
                {
                    // 初回: チャンネル名・レジデンシーバッファ・オフセットを各コアに設定し、
                    // 現在のレジデンシー値を PrevResidencies として保存する
                    SetupCoreChannels(items);
                    _initialized = true;
                }
                else
                {
                    UpdateFrequencies(items);
                }
            }
            CFRelease(key);
            CFRelease(sample);
        }

        CFRelease(subscription);
        CFRelease(channels);
    }

    // =====================================================================
    // 初期化 (初回 Update 時のみ)
    // =====================================================================

    /// <summary>
    /// CFArray を走査してチャンネル名をコアに割り当て、
    /// 初期レジデンシー値を PrevResidencies に格納する。
    /// </summary>
    private void SetupCoreChannels(IntPtr items)
    {
        var eCoreNames = new List<string>();
        var pCoreNames = new List<string>();

        long count = CFArrayGetCount(items);
        for (long idx = 0; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            var subGroup = CFStringToString(IOReportChannelGetSubGroup(item));
            if (subGroup != "CPU Core Performance States") continue;

            var channelName = CFStringToString(IOReportChannelGetChannelName(item));
            if (channelName == null) continue;

            if (channelName.StartsWith("ECPU", StringComparison.Ordinal))
            {
                if (!eCoreNames.Contains(channelName)) eCoreNames.Add(channelName);
            }
            else if (channelName.StartsWith("PCPU", StringComparison.Ordinal))
            {
                if (!pCoreNames.Contains(channelName)) pCoreNames.Add(channelName);
            }
        }

        eCoreNames.Sort(StringComparer.Ordinal);
        pCoreNames.Sort(StringComparer.Ordinal);

        AssignChannels(_eCores, eCoreNames, _eCoreFreqs, items, count);
        AssignChannels(_pCores, pCoreNames, _pCoreFreqs, items, count);
    }

    private static void AssignChannels(
        CpuCoreFrequency[] cores, List<string> channelNames, int[] freqTable,
        IntPtr items, long itemCount)
    {
        for (int ci = 0; ci < channelNames.Count && ci < cores.Length; ci++)
        {
            var core = cores[ci];
            core.ChannelName = channelNames[ci];
            core.FreqTable = freqTable;

            // 対応する CFArray アイテムを検索してバッファを初期化
            for (long idx = 0; idx < itemCount; idx++)
            {
                var item = CFArrayGetValueAtIndex(items, idx);
                var ch = CFStringToString(IOReportChannelGetChannelName(item));
                if (ch != core.ChannelName) continue;

                int stateCount = IOReportStateGetCount(item);
                core.PrevResidencies = new long[stateCount];
                core.CurrResidencies = new long[stateCount];

                for (int s = 0; s < stateCount; s++)
                {
                    if (core.ResidencyOffset < 0)
                    {
                        var name = CFStringToString(IOReportStateGetNameForIndex(item, s));
                        if (name is not ("IDLE" or "DOWN" or "OFF"))
                            core.ResidencyOffset = s;
                    }
                    core.PrevResidencies[s] = IOReportStateGetResidency(item, s);
                }
                break;
            }
        }
    }

    // =====================================================================
    // 周波数更新 (2回目以降の Update)
    // =====================================================================

    /// <summary>
    /// 現在のレジデンシー値を読み取り、前回値との差分から周波数を算出する。
    /// </summary>
    private void UpdateFrequencies(IntPtr items)
    {
        for (int i = 0; i < _eCores.Length; i++) _eCores[i].Frequency = 0;
        for (int i = 0; i < _pCores.Length; i++) _pCores[i].Frequency = 0;

        long count = CFArrayGetCount(items);
        for (long idx = 0; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            var channelName = CFStringToString(IOReportChannelGetChannelName(item));
            var core = FindCore(channelName);
            if (core == null || core.CurrResidencies.Length == 0) continue;

            int stateCount = IOReportStateGetCount(item);
            for (int s = 0; s < stateCount && s < core.CurrResidencies.Length; s++)
                core.CurrResidencies[s] = IOReportStateGetResidency(item, s);

            core.Frequency = CalculateFrequencies(core.CurrResidencies, core.PrevResidencies, core.FreqTable, core.ResidencyOffset);

            // バッファをスワップ: 今回値が次回の前回値になる (コピー不要)
            (core.PrevResidencies, core.CurrResidencies) = (core.CurrResidencies, core.PrevResidencies);
        }
    }

    /// <summary>チャンネル名でコアを線形検索する。</summary>
    private CpuCoreFrequency? FindCore(string? channelName)
    {
        if (channelName == null) return null;
        for (int i = 0; i < _eCores.Length; i++)
            if (_eCores[i].ChannelName == channelName) return _eCores[i];
        for (int i = 0; i < _pCores.Length; i++)
            if (_pCores[i].ChannelName == channelName) return _pCores[i];
        return null;
    }

    // =====================================================================
    // 周波数計算 (アロケーションなし)
    // =====================================================================

    /// <summary>
    /// 前回・今回のレジデンシー差分から実効周波数 (MHz) を計算する。
    /// 全ステートの差分合計を分母にすることでアイドル時間を反映した実効周波数を算出する。
    /// </summary>
    private static double CalculateFrequencies(long[] curr, long[] prev, int[] freqs, int offset)
    {
        if (offset < 0) return 0;

        double totalDelta = 0;
        for (int i = 0; i < curr.Length; i++)
            totalDelta += curr[i] - prev[i];

        double avgFreq = 0;
        for (int i = 0; i < freqs.Length; i++)
        {
            int key = i + offset;
            if (key >= curr.Length) continue;
            double delta = curr[key] - prev[key];
            double percent = totalDelta == 0 ? 0 : delta / totalDelta;
            avgFreq += percent * freqs[i];
        }

        return avgFreq;
    }

    // =====================================================================
    // 周波数テーブル取得 (Swift版: SystemKit.getFrequencies())
    // =====================================================================

    /// <summary>
    /// AppleARMIODevice の "pmgr" から E-Core / P-Core の周波数テーブルを取得する。
    /// </summary>
    private static (int[] eCoreFreqs, int[] pCoreFreqs)? GetFrequencyTables(string cpuName)
    {
        var matching = IOServiceMatching("AppleARMIODevice");
        if (matching == IntPtr.Zero) return null;

        if (IOServiceGetMatchingServices(kIOMasterPortDefault, matching, out uint iterator) != 0)
            return null;

        var isM4OrLater = cpuName.Contains("M4", StringComparison.OrdinalIgnoreCase)
                       || cpuName.Contains("M5", StringComparison.OrdinalIgnoreCase);

        int[] eFreqs = [];
        int[] pFreqs = [];

        uint child;
        while ((child = IOIteratorNext(iterator)) != 0)
        {
            try
            {
                var nameBuffer = Marshal.AllocHGlobal(128);
                try
                {
                    if (IORegistryEntryGetName(child, nameBuffer) != 0) continue;
                    if (Marshal.PtrToStringUTF8(nameBuffer) != "pmgr") continue;
                }
                finally { Marshal.FreeHGlobal(nameBuffer); }

                if (IORegistryEntryCreateCFProperties(child, out IntPtr propsRef, IntPtr.Zero, 0) != 0)
                    continue;

                var eKey = CreateCFString("voltage-states1-sram");
                if (CFDictionaryGetValueIfPresent(propsRef, eKey, out IntPtr eData))
                    eFreqs = ConvertCFDataToFrequencyArray(eData, isM4OrLater);
                CFRelease(eKey);

                var pKey = CreateCFString("voltage-states5-sram");
                if (CFDictionaryGetValueIfPresent(propsRef, pKey, out IntPtr pData))
                    pFreqs = ConvertCFDataToFrequencyArray(pData, isM4OrLater);
                CFRelease(pKey);

                CFRelease(propsRef);
            }
            finally { IOObjectRelease(child); }
        }

        IOObjectRelease(iterator);
        return (eFreqs, pFreqs);
    }

    /// <summary>
    /// CFData からバイト列を読み取り、8バイトチャンクごとに周波数 (MHz) へ変換する。
    /// Swift版: helpers.swift の convertCFDataToArr()
    /// </summary>
    private static int[] ConvertCFDataToFrequencyArray(IntPtr cfData, bool isM4)
    {
        var length = (int)CFDataGetLength(cfData);
        var ptr = CFDataGetBytePtr(cfData);

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);

        uint multiplier = isM4 ? 1000u : 1_000_000u;

        var result = new List<int>();
        for (int i = 0; i + 7 < length; i += 8)
        {
            uint v = (uint)bytes[i]
                   | ((uint)bytes[i + 1] << 8)
                   | ((uint)bytes[i + 2] << 16)
                   | ((uint)bytes[i + 3] << 24);
            result.Add((int)(v / multiplier));
        }
        return result.ToArray();
    }

    // =====================================================================
    // IOReport チャンネル取得 (Swift版: FrequencyReader.getChannels())
    // =====================================================================

    private static IntPtr GetChannels()
    {
        var channelDefs = new[]
        {
            ("CPU Stats", "CPU Complex Performance States"),
            ("CPU Stats", "CPU Core Performance States"),
        };

        var channels = new List<IntPtr>();
        foreach (var (gname, sname) in channelDefs)
        {
            var gStr = CreateCFString(gname);
            var sStr = CreateCFString(sname);
            var channel = IOReportCopyChannelsInGroup(gStr, sStr, 0, 0, 0);
            CFRelease(gStr);
            CFRelease(sStr);
            if (channel != IntPtr.Zero)
                channels.Add(channel);
        }
        if (channels.Count == 0) return IntPtr.Zero;

        var merged = channels[0];
        for (int i = 1; i < channels.Count; i++)
            IOReportMergeChannels(merged, channels[i], IntPtr.Zero);

        int size = CFDictionaryGetCount(merged);
        var mutableCopy = CFDictionaryCreateMutableCopy(IntPtr.Zero, size, merged);

        var key = CreateCFString("IOReportChannels");
        bool hasChannels = CFDictionaryGetValueIfPresent(mutableCopy, key, out _);
        CFRelease(key);

        return hasChannels ? mutableCopy : IntPtr.Zero;
    }

    // =====================================================================
    // sysctl ヘルパー
    // =====================================================================

    [DllImport("libSystem.dylib")]
    private static extern int sysctlbyname(string name, IntPtr oldp, ref nint oldlenp, IntPtr newp, nint newlen);

    private static int? GetSysctlInt(string name)
    {
        nint len = sizeof(int);
        var buf = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            if (sysctlbyname(name, buf, ref len, IntPtr.Zero, 0) != 0) return null;
            return Marshal.ReadInt32(buf);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }
}
