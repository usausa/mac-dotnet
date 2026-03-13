using System.Runtime.InteropServices;
using static CpuFrequencySample.NativeBindings;

namespace CpuFrequencySample;

/// <summary>
/// CPU全コアの周波数を管理するクラス。
/// Update() を呼び出すと IOReport でサンプリングを行い、
/// 各 CpuCoreFrequency の Frequency プロパティを最新値に更新する。
/// </summary>
public sealed class CpuFrequency
{
    private readonly int[] _eCoreFreqs;
    private readonly int[] _pCoreFreqs;
    private readonly CpuCoreFrequency[] _eCores;
    private readonly CpuCoreFrequency[] _pCores;

    private IntPtr _channels;
    private IntPtr _subscription;
    private IntPtr _prevSample;
    private readonly IntPtr _ioReportChannelsKey; // "IOReportChannels" CFString をキャッシュ

    /// <summary>コアごとの周波数情報リスト。Update() により値が更新される。</summary>
    public IReadOnlyList<CpuCoreFrequency> Cores { get; }

    /// <summary>E-Core の周波数テーブル (MHz)</summary>
    public IReadOnlyList<int> ECoreFrequencyTable => _eCoreFreqs;

    /// <summary>P-Core の周波数テーブル (MHz)</summary>
    public IReadOnlyList<int> PCoreFrequencyTable => _pCoreFreqs;

    /// <summary>
    /// コンストラクタ。AppleARMIODevice から周波数テーブルを取得し、
    /// IOReport のサブスクリプションを作成する。
    /// </summary>
    /// <param name="cpuName">CPU名 (M4/M5 判定に使用)</param>
    /// <exception cref="InvalidOperationException">Apple Silicon でない場合</exception>
    public CpuFrequency(string cpuName)
    {
        var tables = FrequencyTableReader.GetFrequencyTables(cpuName);
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

        _channels = GetChannels();
        if (_channels == IntPtr.Zero)
            throw new InvalidOperationException("IOReport チャンネルの取得に失敗しました。");

        _subscription = IOReportCreateSubscription(IntPtr.Zero, _channels, out IntPtr dict, 0, IntPtr.Zero);
        if (dict != IntPtr.Zero) CFRelease(dict);

        if (_subscription == IntPtr.Zero)
            throw new InvalidOperationException("IOReport サブスクリプションの作成に失敗しました。");

        _ioReportChannelsKey = CreateCFString("IOReportChannels");
        InitializeCoreChannels();
    }

    ~CpuFrequency()
    {
        if (_prevSample != IntPtr.Zero) CFRelease(_prevSample);
        if (_ioReportChannelsKey != IntPtr.Zero) CFRelease(_ioReportChannelsKey);
        if (_channels != IntPtr.Zero) CFRelease(_channels);
        if (_subscription != IntPtr.Zero) CFRelease(_subscription);
    }

    // =====================================================================
    // 初期化
    // =====================================================================

    /// <summary>
    /// 初期サンプルを取得してチャンネル名を列挙し、各コアに ChannelName と FreqTable を設定する。
    /// </summary>
    private void InitializeCoreChannels()
    {
        var sample = IOReportCreateSamples(_subscription, _channels, IntPtr.Zero);
        if (sample == IntPtr.Zero) return;

        _prevSample = sample;

        if (!CFDictionaryGetValueIfPresent(sample, _ioReportChannelsKey, out IntPtr items))
            return;

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

        for (int i = 0; i < eCoreNames.Count && i < _eCores.Length; i++)
        {
            _eCores[i].ChannelName = eCoreNames[i];
            _eCores[i].FreqTable = _eCoreFreqs;
        }
        for (int i = 0; i < pCoreNames.Count && i < _pCores.Length; i++)
        {
            _pCores[i].ChannelName = pCoreNames[i];
            _pCores[i].FreqTable = _pCoreFreqs;
        }
    }

    // =====================================================================
    // Update
    // =====================================================================

    /// <summary>
    /// 前回サンプルとの差分から各コアの Frequency を更新する。
    /// スリープは行わない。呼び出し側が間隔を制御する (例: Thread.Sleep(1000))。
    /// </summary>
    public void Update()
    {
        var current = IOReportCreateSamples(_subscription, _channels, IntPtr.Zero);
        if (current == IntPtr.Zero) return;

        if (_prevSample == IntPtr.Zero)
        {
            _prevSample = current;
            return;
        }

        var diffPtr = IOReportCreateSamplesDelta(_prevSample, current, IntPtr.Zero);
        CFRelease(_prevSample);
        _prevSample = current;

        if (diffPtr == IntPtr.Zero) return;

        for (int i = 0; i < _eCores.Length; i++) _eCores[i].Frequency = 0;
        for (int i = 0; i < _pCores.Length; i++) _pCores[i].Frequency = 0;

        if (CFDictionaryGetValueIfPresent(diffPtr, _ioReportChannelsKey, out IntPtr items))
        {
            long count = CFArrayGetCount(items);
            for (long idx = 0; idx < count; idx++)
            {
                var item = CFArrayGetValueAtIndex(items, idx);
                var channelName = CFStringToString(IOReportChannelGetChannelName(item));
                var core = FindCore(channelName);
                if (core == null) continue;
                core.Frequency = CalculateFrequencies(item, core.FreqTable);
            }
        }

        CFRelease(diffPtr);
    }

    /// <summary>
    /// チャンネル名でコアを検索する。E-Core → P-Core の順に線形探索。
    /// </summary>
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
    // 周波数計算
    // =====================================================================

    /// <summary>
    /// ステートの residency から実効周波数 (MHz) を計算する。
    /// 分母に全ステート (IDLE/DOWN 含む) の合計を使うことで、
    /// アイドル時間を反映した実効周波数を算出する。
    /// Swift版: FrequencyReader.calculateFrequencies(dict:freqs:)
    /// </summary>
    private static double CalculateFrequencies(IntPtr dict, int[] freqs)
    {
        int stateCount = IOReportStateGetCount(dict);

        // IDLE / DOWN / OFF より後の最初のステートを周波数テーブルの起点とする
        int offset = -1;
        for (int i = 0; i < stateCount; i++)
        {
            var name = CFStringToString(IOReportStateGetNameForIndex(dict, i));
            if (name is not ("IDLE" or "DOWN" or "OFF"))
            {
                offset = i;
                break;
            }
        }
        if (offset < 0) return 0;

        double totalTime = 0;
        for (int i = 0; i < stateCount; i++)
            totalTime += IOReportStateGetResidency(dict, i);

        double avgFreq = 0;
        for (int i = 0; i < freqs.Length; i++)
        {
            int key = i + offset;
            if (key >= stateCount) continue;
            double percent = totalTime == 0 ? 0 : (double)IOReportStateGetResidency(dict, key) / totalTime;
            avgFreq += percent * freqs[i];
        }

        return avgFreq;
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
