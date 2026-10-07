namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

// Connection to AppleSMC held by SmcMonitor
internal sealed class SmcConnection : IDisposable
{
    private SafeIOConnectHandle? connect;

    public bool IsOpen => connect is not null;

    // io_connect_t (0 when not open)
    public uint Handle => connect?.Value ?? 0;

    public void Dispose()
    {
        connect?.Dispose();
        connect = null;
    }

    // Closes the current connection (if any) and opens a new one
    public bool Open()
    {
        Close();

        // The service object is only needed to open the connection
        using var service = new IOObj(IOServiceGetMatchingService(0, IOServiceMatching("AppleSMC")));
        if (!service.IsValid || (IOServiceOpen(service, MachTask.Self, 0, out var connection) != KERN_SUCCESS) || (connection == 0))
        {
            return false;
        }

        connect = new SafeIOConnectHandle(connection);
        return true;
    }

    public void Close()
    {
        connect?.Dispose();
        connect = null;
    }
}
