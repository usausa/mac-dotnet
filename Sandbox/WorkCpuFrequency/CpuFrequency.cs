using System.Runtime.InteropServices;
using static CpuFrequencySample.NativeBindings;

namespace CpuFrequencySample;

/// <summary>
/// CPU全コアの周波数を管理するクラス。
/// Update() を呼び出すと IOReport でサンプリングを行い、
/// 各 CpuCoreFrequency の Frequency プロパティを最新値に更新する。
///
/// Swift版 FrequencyReader の測定ロジックを内包するが、
/// 平均計算などの集計は呼び出し側の責務とする。
/// </summary>
public sealed class CpuFrequency : IDisposable
{
    private readonly int[] _eCoreFreqs;   // E-Core 周波数テーブル (MHz)
    private readonly int[] _pCoreFreqs;   // P-Core 周波数テーブル (MHz)

    // IOReport チャンネル名 → CpuCoreFrequency のマッピング
    // "CPU Core Performance States" チャンネルをアルファベット順にソートしてインデックスを割り当て
    private readonly Dictionary<string, CpuCoreFrequency> _channelToCoreMap;

    private IntPtr _channels;
    private IntPtr _subscription;
    private IntPtr _prevSample;

    /// <summary>1回サンプリング × 1000ms = 1秒</summary>
    private const int MeasurementCount = 1;
    private const int StepMs = 1000;

    /// <summary>コアごとの周波数情報リスト。Update() により値が更新される。</summary>
    public IReadOnlyList<CpuCoreFrequency> Cores { get; }

    /// <summary>
    /// コンストラクタ。AppleARMIODevice から周波数テーブルを取得し、
    /// IOReport のサブスクリプションを作成する。
    /// </summary>
    /// <param name="cpuName">CPU名 (M4/M5 判定に使用)</param>
    /// <exception cref="InvalidOperationException">Apple Silicon でない場合</exception>
    public CpuFrequency(string cpuName)
    {
        // ----- 周波数テーブルの取得 (Swift版: SystemKit.getFrequencies) -----
        var tables = FrequencyTableReader.GetFrequencyTables(cpuName);
        if (tables == null)
            throw new InvalidOperationException("周波数テーブルの取得に失敗しました。");

        _eCoreFreqs = tables.Value.eCoreFreqs;
        _pCoreFreqs = tables.Value.pCoreFreqs;

        if (_eCoreFreqs.Length == 0 && _pCoreFreqs.Length == 0)
            throw new InvalidOperationException("周波数テーブルが空です。Apple Silicon Mac で実行してください。");

        // ----- コア数の取得 (sysctl) -----
        int eCoreCount = GetSysctlInt("hw.perflevel1.logicalcpu") ?? 0;
        int pCoreCount = GetSysctlInt("hw.perflevel0.logicalcpu") ?? 0;

        // ----- Cores リストの構築 -----
        var cores = new List<CpuCoreFrequency>();
        for (int i = 0; i < eCoreCount; i++)
            cores.Add(new CpuCoreFrequency(i, CpuCoreType.Efficiency));
        for (int i = 0; i < pCoreCount; i++)
            cores.Add(new CpuCoreFrequency(i, CpuCoreType.Performance));
        Cores = cores.AsReadOnly();

        // ----- IOReport サブスクリプションの作成 (Swift版: FrequencyReader.setup) -----
        _channels = GetChannels();
        if (_channels == IntPtr.Zero)
            throw new InvalidOperationException("IOReport チャンネルの取得に失敗しました。");

        _subscription = IOReportCreateSubscription(IntPtr.Zero, _channels, out IntPtr dict, 0, IntPtr.Zero);
        if (dict != IntPtr.Zero) CFRelease(dict);

        if (_subscription == IntPtr.Zero)
            throw new InvalidOperationException("IOReport サブスクリプションの作成に失敗しました。");

        // ----- チャンネル名 → コアのマッピングを構築 -----
        // IOReport のチャンネル名は "ECPU000", "ECPU010", "PCPU000", "PCPU100" 等の形式。
        // アルファベット順にソートして各コア種別の連番 (0, 1, 2, ...) に割り当てる。
        _channelToCoreMap = BuildChannelToCoreMap(cores);
    }

    /// <summary>
    /// 初期サンプルを取得してチャンネル名を列挙し、コアへのマッピングを構築する。
    /// </summary>
    private Dictionary<string, CpuCoreFrequency> BuildChannelToCoreMap(List<CpuCoreFrequency> cores)
    {
        var map = new Dictionary<string, CpuCoreFrequency>();
        var sample = IOReportCreateSamples(_subscription, _channels, IntPtr.Zero);
        if (sample == IntPtr.Zero) return map;

        // 初回サンプルをデルタ計算のベースとして保持
        _prevSample = sample;

        var allSamples = CollectIOSamples(sample);

        // "CPU Core Performance States" サブグループのチャンネルのみ対象
        var eCoreChannels = allSamples
            .Where(s => s.SubGroup == "CPU Core Performance States"
                     && s.Channel.StartsWith("ECPU", StringComparison.Ordinal))
            .Select(s => s.Channel)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var pCoreChannels = allSamples
            .Where(s => s.SubGroup == "CPU Core Performance States"
                     && s.Channel.StartsWith("PCPU", StringComparison.Ordinal))
            .Select(s => s.Channel)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var eCores = cores.Where(c => c.CoreType == CpuCoreType.Efficiency).ToList();
        var pCores = cores.Where(c => c.CoreType == CpuCoreType.Performance).ToList();

        for (int i = 0; i < eCoreChannels.Count && i < eCores.Count; i++)
            map[eCoreChannels[i]] = eCores[i];

        for (int i = 0; i < pCoreChannels.Count && i < pCores.Count; i++)
            map[pCoreChannels[i]] = pCores[i];

        return map;
    }

    /// <summary>
    /// IOReport でサンプリングを行い、各コアの Frequency を最新値に更新する。
    /// 1000ms (1000ms × 1回) のブロッキング処理。
    ///
    /// Swift版: FrequencyReader.read() に対応。
    /// ただし E-Core/P-Core 平均の算出は行わず、コア単位の値のみ更新する。
    /// </summary>
    public void Update()
    {
        // コア種別ごとに測定値を蓄積するバッファ
        // key: ("ECPU0" などのチャンネル名) → value: 測定ごとの周波数リスト
        var accumulator = new Dictionary<string, List<double>>();

        foreach (var samples in GetSamples())
        {
            foreach (var sample in samples)
            {
                if (sample.Group != "CPU Stats") continue;
                if (sample.SubGroup != "CPU Core Performance States") continue;

                if (!_channelToCoreMap.TryGetValue(sample.Channel, out var core)) continue;

                int[] freqTable = core.CoreType == CpuCoreType.Efficiency ? _eCoreFreqs : _pCoreFreqs;
                double freq = CalculateFrequencies(sample.Delta, freqTable);

                if (!accumulator.TryGetValue(sample.Channel, out var list))
                {
                    list = new List<double>(MeasurementCount);
                    accumulator[sample.Channel] = list;
                }
                list.Add(freq);
            }
        }

        // ----- 全コアを 0 にリセットしてから測定結果を反映 -----
        foreach (var core in Cores)
            core.Frequency = 0;

        foreach (var (channelName, measurements) in accumulator)
        {
            if (!_channelToCoreMap.TryGetValue(channelName, out var core)) continue;
            if (measurements.Count == 0) continue;

            double avg = measurements.Sum() / measurements.Count;
            int[] freqTable = core.CoreType == CpuCoreType.Efficiency ? _eCoreFreqs : _pCoreFreqs;
            double minFreq = freqTable.Length > 0 ? freqTable.Min() : 0;
            core.Frequency = Math.Max(avg, minFreq);
        }
    }

    /// <summary>E-Core の周波数テーブル (MHz)</summary>
    public IReadOnlyList<int> ECoreFrequencyTable => _eCoreFreqs;

    /// <summary>P-Core の周波数テーブル (MHz)</summary>
    public IReadOnlyList<int> PCoreFrequencyTable => _pCoreFreqs;

    // =====================================================================
    // 以下、IOReport を使った内部実装 (Swift版 FrequencyReader と同一ロジック)
    // =====================================================================

    /// <summary>
    /// ステートの residency から加重平均周波数を計算する。
    /// Swift版: FrequencyReader.calculateFrequencies(dict:freqs:)
    /// </summary>
    private double CalculateFrequencies(IntPtr dict, int[] freqs)
    {
        var items = GetResidencies(dict);

        int offset = -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Name is not ("IDLE" or "DOWN" or "OFF"))
            {
                offset = i;
                break;
            }
        }
        if (offset < 0) return 0;

        double usage = 0;
        for (int i = offset; i < items.Count; i++)
            usage += items[i].Residency;

        double avgFreq = 0;
        for (int i = 0; i < freqs.Length; i++)
        {
            int key = i + offset;
            if (key >= items.Count) continue;
            double percent = usage == 0 ? 0 : items[key].Residency / usage;
            avgFreq += percent * freqs[i];
        }

        return avgFreq;
    }

    /// <summary>
    /// Swift版: FrequencyReader.getResidencies(dict:)
    /// </summary>
    private static List<(string Name, double Residency)> GetResidencies(IntPtr dict)
    {
        int count = IOReportStateGetCount(dict);
        var result = new List<(string, double)>(count);
        for (int i = 0; i < count; i++)
        {
            var namePtr = IOReportStateGetNameForIndex(dict, i);
            var name = CFStringToString(namePtr) ?? "";
            var residency = IOReportStateGetResidency(dict, i);
            result.Add((name, (double)residency));
        }
        return result;
    }

    /// <summary>
    /// Swift版: FrequencyReader.getChannels()
    /// </summary>
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

    /// <summary>
    /// Swift版: FrequencyReader.getSamples()
    /// </summary>
    private IEnumerable<List<IOSample>> GetSamples()
    {
        var initial = TakeSample();
        if (initial == null) yield break;

        var prev = (_prevSample != IntPtr.Zero)
            ? _prevSample
            : initial.Value;

        for (int i = 0; i < MeasurementCount; i++)
        {
            Thread.Sleep(StepMs);
            var next = TakeSample();
            if (next == null) continue;

            var diffPtr = IOReportCreateSamplesDelta(prev, next.Value, IntPtr.Zero);
            if (diffPtr != IntPtr.Zero)
            {
                yield return CollectIOSamples(diffPtr);
                CFRelease(diffPtr);
            }
            prev = next.Value;
        }
        _prevSample = prev;
    }

    /// <summary>
    /// Swift版: FrequencyReader.getSample()
    /// </summary>
    private IntPtr? TakeSample()
    {
        var sample = IOReportCreateSamples(_subscription, _channels, IntPtr.Zero);
        return sample == IntPtr.Zero ? null : sample;
    }

    /// <summary>
    /// Swift版: FrequencyReader.collectIOSamples(data:)
    /// </summary>
    private static List<IOSample> CollectIOSamples(IntPtr data)
    {
        var key = CreateCFString("IOReportChannels");
        if (!CFDictionaryGetValueIfPresent(data, key, out IntPtr items))
        {
            CFRelease(key);
            return [];
        }
        CFRelease(key);

        long count = CFArrayGetCount(items);
        var samples = new List<IOSample>((int)count);
        for (long idx = 0; idx < count; idx++)
        {
            var item = CFArrayGetValueAtIndex(items, idx);
            var group = CFStringToString(IOReportChannelGetGroup(item)) ?? "";
            var subGroup = CFStringToString(IOReportChannelGetSubGroup(item)) ?? "";
            var channel = CFStringToString(IOReportChannelGetChannelName(item)) ?? "";
            samples.Add(new IOSample(group, subGroup, channel, item));
        }
        return samples;
    }

    public void Dispose()
    {
        if (_channels != IntPtr.Zero) { CFRelease(_channels); _channels = IntPtr.Zero; }
        if (_prevSample != IntPtr.Zero) { CFRelease(_prevSample); _prevSample = IntPtr.Zero; }
    }

    private record IOSample(string Group, string SubGroup, string Channel, IntPtr Delta);

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
