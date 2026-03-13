using System.Runtime.InteropServices;

namespace CpuFrequencySample;

class Program
{
    static void Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.WriteLine("このプログラムは macOS (Apple Silicon) 専用です。");
            return;
        }

        // ----- CPU名の取得 -----
        string cpuName = GetSysctl("machdep.cpu.brand_string") ?? "Unknown";
        Console.WriteLine($"CPU: {cpuName}");
        Console.WriteLine();

        // ----- CpuFrequency の生成 -----
        var cpu = new CpuFrequency(cpuName);

        // 周波数テーブルの表示
        Console.WriteLine($"E-Core 周波数テーブル: {string.Join(", ", cpu.ECoreFrequencyTable)} MHz");
        Console.WriteLine($"P-Core 周波数テーブル: {string.Join(", ", cpu.PCoreFrequencyTable)} MHz");
        Console.WriteLine($"コア数: {cpu.Cores.Count} "
            + $"(E-Core: {cpu.Cores.Count(c => c.CoreType == CpuCoreType.Efficiency)}, "
            + $"P-Core: {cpu.Cores.Count(c => c.CoreType == CpuCoreType.Performance)})");
        Console.WriteLine();

        // ----- リアルタイム監視 -----
        Console.WriteLine("=== リアルタイム周波数監視 (Ctrl+C で終了) ===");
        Console.WriteLine();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // 1秒待機してから Update() を呼び出す
                Thread.Sleep(1000);
                cpu.Update();

                // ----- 集計は呼び出し側の責務 -----
                var eCores = cpu.Cores.Where(c => c.CoreType == CpuCoreType.Efficiency).ToList();
                var pCores = cpu.Cores.Where(c => c.CoreType == CpuCoreType.Performance).ToList();

                double eAvg = eCores.Count > 0 ? eCores.Average(c => c.Frequency) : 0;
                double pAvg = pCores.Count > 0 ? pCores.Average(c => c.Frequency) : 0;
                double allAvg = cpu.Cores.Count > 0 ? cpu.Cores.Average(c => c.Frequency) : 0;

                // ----- 表示 -----
                Console.Clear();
                Console.WriteLine($"CPU: {cpuName}");
                Console.WriteLine(new string('-', 60));

                // コアごとの周波数
                foreach (var core in cpu.Cores)
                {
                    string bar = MakeBar(core.Frequency, cpu.PCoreFrequencyTable.Max());
                    Console.WriteLine($"  {core.CoreType.ToString()[0]}-Core {core.Number}: "
                        + $"{core.Frequency,7:F1} MHz  {bar}");
                }

                Console.WriteLine(new string('-', 60));

                // 平均
                Console.WriteLine($"  E-Core 平均: {eAvg,7:F1} MHz ({eAvg / 1000.0:F2} GHz)");
                Console.WriteLine($"  P-Core 平均: {pAvg,7:F1} MHz ({pAvg / 1000.0:F2} GHz)");
                Console.WriteLine($"  全体   平均: {allAvg,7:F1} MHz ({allAvg / 1000.0:F2} GHz)");
            }
        }
        catch (OperationCanceledException) { }

        Console.WriteLine();
        Console.WriteLine("終了しました。");
    }

    /// <summary>簡易バーグラフを生成する</summary>
    static string MakeBar(double value, double max, int width = 30)
    {
        int filled = max > 0 ? (int)(value / max * width) : 0;
        filled = Math.Clamp(filled, 0, width);
        return "[" + new string('█', filled) + new string('░', width - filled) + "]";
    }

    // ----- sysctl -----

    [DllImport("libSystem.dylib")]
    private static extern int sysctlbyname(string name, IntPtr oldp, ref nint oldlenp, IntPtr newp, nint newlen);

    private static string? GetSysctl(string name)
    {
        nint len = 0;
        if (sysctlbyname(name, IntPtr.Zero, ref len, IntPtr.Zero, 0) != 0) return null;
        var buf = Marshal.AllocHGlobal((int)len);
        try
        {
            if (sysctlbyname(name, buf, ref len, IntPtr.Zero, 0) != 0) return null;
            return Marshal.PtrToStringUTF8(buf);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }
}
