namespace Example.Disk.ConsoleApp;

using System.Runtime.Versioning;

using global::MacDotNet.Disk;

[SupportedOSPlatform("macos")]
internal static class Program
{
    public static void Main()
    {
        var disks = DiskInfo.GetInformation();
        if (disks.Count == 0)
        {
            Console.WriteLine("No disks found.");
            return;
        }

        for (var i = 0; i < disks.Count; i++)
        {
            var disk = disks[i];

            Console.WriteLine($"=== Disk [{i}] ===");
            Console.WriteLine($"BSD Name:           {disk.BsdName ?? "(n/a)"}");
            Console.WriteLine($"Device Name:        {disk.DeviceName ?? "(n/a)"}");
            Console.WriteLine($"Model:              {(string.IsNullOrEmpty(disk.Model) ? "(n/a)" : disk.Model)}");
            Console.WriteLine($"Serial Number:      {(string.IsNullOrEmpty(disk.SerialNumber) ? "(n/a)" : disk.SerialNumber)}");
            Console.WriteLine($"Firmware Revision:  {(string.IsNullOrEmpty(disk.FirmwareRevision) ? "(n/a)" : disk.FirmwareRevision)}");
            Console.WriteLine($"Medium Type:        {disk.MediumType ?? "(n/a)"}");
            Console.WriteLine($"Removable:          {disk.Removable}");
            Console.WriteLine($"Ejectable:          {disk.Ejectable}");
            Console.WriteLine($"Physical Block:     {disk.PhysicalBlockSize}");
            Console.WriteLine($"Logical Block:      {disk.LogicalBlockSize}");
            Console.WriteLine($"Disk Size:          {FormatBytes(disk.Size)}");
            Console.WriteLine($"Bus Type:           {disk.BusType}");
            Console.WriteLine($"Bus Location:       {disk.BusLocation ?? "(n/a)"}");
            Console.WriteLine($"Content Type:       {disk.ContentType ?? "(n/a)"}");
            Console.WriteLine($"SMART Type:         {disk.SmartType}");
            Console.WriteLine();

            // I/O統計
            if (disk.IOStatistics is not null)
            {
                var s = disk.IOStatistics;
                Console.WriteLine("--- I/O Statistics ---");
                Console.WriteLine($"Bytes Read:         {FormatBytes((ulong)Math.Max(0, s.BytesRead))}");
                Console.WriteLine($"Bytes Written:      {FormatBytes((ulong)Math.Max(0, s.BytesWritten))}");
                Console.WriteLine($"Ops Read:           {s.OperationsRead}");
                Console.WriteLine($"Ops Written:        {s.OperationsWritten}");
                Console.WriteLine($"Time Read:          {s.TotalTimeRead / 1_000_000} ms");
                Console.WriteLine($"Time Written:       {s.TotalTimeWritten / 1_000_000} ms");
                Console.WriteLine($"Retries Read:       {s.RetriesRead}");
                Console.WriteLine($"Retries Written:    {s.RetriesWritten}");
                Console.WriteLine($"Errors Read:        {s.ErrorsRead}");
                Console.WriteLine($"Errors Written:     {s.ErrorsWritten}");
                Console.WriteLine($"Latency Read:       {s.LatencyTimeRead / 1_000_000} ms");
                Console.WriteLine($"Latency Written:    {s.LatencyTimeWritten / 1_000_000} ms");
                Console.WriteLine();
            }

            // NVMe SMART
            if (disk.Smart is ISmartNvme nvme)
            {
                Console.WriteLine("--- NVMe SMART ---");
                DisplayNvmeSmart(nvme);

                // 繰り返し呼び出しのデモ
                Console.WriteLine();
                Console.WriteLine("--- NVMe SMART (2nd read) ---");
                if (nvme.Update())
                {
                    Console.WriteLine($"Temperature:        {nvme.Temperature} C");
                    Console.WriteLine($"Available Spare:    {nvme.AvailableSpare}%");
                    Console.WriteLine($"Percentage Used:    {nvme.PercentageUsed}%");
                }

                Console.WriteLine();
            }

            // ATA SMART
            if (disk.Smart is ISmartGeneric ata)
            {
                Console.WriteLine("--- ATA SMART ---");
                DisplayAtaSmart(ata);

                // 繰り返し呼び出しのデモ
                Console.WriteLine();
                Console.WriteLine("--- ATA SMART (2nd read) ---");
                if (ata.Update())
                {
                    Console.WriteLine($"Attributes found:   {ata.GetSupportedIds().Count}");
                }

                Console.WriteLine();
            }

            // パーティション情報
            var partitions = disk.GetPartitions().ToList();
            if (partitions.Count > 0)
            {
                Console.WriteLine("--- Partitions ---");
                foreach (var part in partitions)
                {
                    Console.WriteLine($"  [{part.Index}] {part.Name,-16} Size: {FormatBytes(part.Size)}");
                }

                Console.WriteLine();
            }
        }

        // リソース解放
        foreach (var disk in disks)
        {
            disk.Dispose();
        }
    }

    private static void DisplayNvmeSmart(ISmartNvme smart)
    {
        Console.WriteLine($"Critical Warning:   0x{smart.CriticalWarning:X2}");
        Console.WriteLine($"Temperature:        {smart.Temperature} C");
        Console.WriteLine($"Available Spare:    {smart.AvailableSpare}%");
        Console.WriteLine($"Spare Threshold:    {smart.AvailableSpareThreshold}%");
        Console.WriteLine($"Percentage Used:    {smart.PercentageUsed}%");
        Console.WriteLine($"Data Units Read:    {smart.DataUnitRead}");
        Console.WriteLine($"Data Units Written: {smart.DataUnitWritten}");
        Console.WriteLine($"Host Read Cmds:     {smart.HostReadCommands}");
        Console.WriteLine($"Host Write Cmds:    {smart.HostWriteCommands}");
        Console.WriteLine($"Ctrl Busy Time:     {smart.ControllerBusyTime}");
        Console.WriteLine($"Power Cycles:       {smart.PowerCycles}");
        Console.WriteLine($"Power On Hours:     {smart.PowerOnHours}");
        Console.WriteLine($"Unsafe Shutdowns:   {smart.UnsafeShutdowns}");
        Console.WriteLine($"Media Errors:       {smart.MediaErrors}");
        Console.WriteLine($"Error Log Entries:  {smart.ErrorInfoLogEntries}");
        Console.WriteLine($"Warning Temp Time:  {smart.WarningCompositeTemperatureTime} min");
        Console.WriteLine($"Critical Temp Time: {smart.CriticalCompositeTemperatureTime} min");

        // 追加温度センサー
        for (var j = 0; j < smart.TemperatureSensors.Length; j++)
        {
            if (smart.TemperatureSensors[j] > -273)
            {
                Console.WriteLine($"Temp Sensor {j}:      {smart.TemperatureSensors[j]} C");
            }
        }
    }

    private static void DisplayAtaSmart(ISmartGeneric smart)
    {
        var ids = smart.GetSupportedIds();
        if (ids.Count > 0)
        {
            Console.WriteLine($"{"ID",4} {"Cur",4} {"Wst",4} {"Raw",12}  Flags");
            Console.WriteLine(new string('-', 40));
            foreach (var id in ids)
            {
                var attr = smart.GetAttribute(id);
                if (attr is not null)
                {
                    var a = attr.Value;
                    Console.WriteLine($"{a.Id,4} {a.CurrentValue,4} {a.WorstValue,4} {a.RawValue,12}  0x{a.Flags:X4}");
                }
            }
        }
        else
        {
            Console.WriteLine("No SMART attributes found.");
        }
    }

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1UL << 40 => $"{bytes / (double)(1UL << 40):F2} TiB",
        >= 1UL << 30 => $"{bytes / (double)(1UL << 30):F2} GiB",
        >= 1UL << 20 => $"{bytes / (double)(1UL << 20):F2} MiB",
        >= 1UL << 10 => $"{bytes / (double)(1UL << 10):F2} KiB",
        _ => $"{bytes} B"
    };
}
