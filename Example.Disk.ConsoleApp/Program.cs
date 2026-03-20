namespace Example.Disk.ConsoleApp;

using global::MacDotNet.Disk;

internal static class Program
{
    public static void Main()
    {
        var disks = DiskInfo.GetInformation();
        foreach (var disk in disks)
        {
            using (disk)
            {
                Console.WriteLine($"[disk{disk.Index}] {disk.Model}");
                Console.WriteLine($"  BsdName:           {disk.BsdName}");
                Console.WriteLine($"  SerialNumber:      {disk.SerialNumber}");
                Console.WriteLine($"  FirmwareRevision:  {disk.FirmwareRevision}");
                Console.WriteLine($"  Size:              {disk.Size / 1024 / 1024 / 1024} GB");
                Console.WriteLine($"  MediumType:        {disk.MediumType}");
                Console.WriteLine($"  BusType:           {disk.BusType}");
                Console.WriteLine($"  BusLocation:       {disk.BusLocation}");
                Console.WriteLine($"  ContentType:       {disk.ContentType}");
                Console.WriteLine($"  SmartType:         {disk.SmartType}");

                disk.Smart.Update();
                if (disk.SmartType == SmartType.Nvme)
                {
                    PrintNvmeSmart((ISmartNvme)disk.Smart);
                }
                else if (disk.SmartType == SmartType.Generic)
                {
                    PrintGenericSmart((ISmartGeneric)disk.Smart);
                }

                Console.WriteLine();
            }
        }
    }

    private static void PrintNvmeSmart(ISmartNvme smart)
    {
        Console.WriteLine($"  SMART (NVMe): LastUpdate=[{smart.LastUpdate}]");
        Console.WriteLine($"    Temperature:     {smart.Temperature} C");
        Console.WriteLine($"    AvailableSpare:  {smart.AvailableSpare} %");
        Console.WriteLine($"    PercentageUsed:  {smart.PercentageUsed} %");
        Console.WriteLine($"    DataUnitRead:    {smart.DataUnitRead}");
        Console.WriteLine($"    DataUnitWritten: {smart.DataUnitWritten}");
        Console.WriteLine($"    PowerCycles:     {smart.PowerCycles}");
        Console.WriteLine($"    PowerOnHours:    {smart.PowerOnHours}");
        Console.WriteLine($"    UnsafeShutdowns: {smart.UnsafeShutdowns}");
        Console.WriteLine($"    MediaErrors:     {smart.MediaErrors}");
        Console.WriteLine($"    CriticalWarning: {smart.CriticalWarning}");
    }

    private static void PrintGenericSmart(ISmartGeneric smart)
    {
        Console.WriteLine($"  SMART (Generic): LastUpdate=[{smart.LastUpdate}]");
        Console.WriteLine("    ID   FLAG   CUR  WOR  RAW");
        Console.WriteLine("    ---  ----   ---  ---  --------");
        foreach (var id in smart.GetSupportedIds())
        {
            var attr = smart.GetAttribute(id);
            if (attr.HasValue)
            {
                Console.WriteLine($"    {(byte)id,3}  0x{attr.Value.Flags:X4}  {attr.Value.CurrentValue,3}  {attr.Value.WorstValue,3}  {attr.Value.RawValue}");
            }
        }
    }
}

