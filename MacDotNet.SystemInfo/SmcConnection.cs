namespace MacDotNet.SystemInfo;

using static MacDotNet.SystemInfo.NativeMethods;

internal sealed class SmcConnection : IDisposable
{
    private SafeIOConnectHandle? connect;

    public bool IsOpen => connect is not null;

    public uint Handle => connect?.Value ?? 0;

    public void Dispose()
    {
        connect?.Dispose();
        connect = null;
    }

    public bool Open()
    {
        Close();

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
