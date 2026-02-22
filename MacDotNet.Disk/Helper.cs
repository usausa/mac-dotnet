namespace MacDotNet.Disk;

internal static class Helper
{
    public static short KelvinToCelsius(ushort value) => (short)(value > 0 ? value - 273 : short.MinValue);

    // COM-like インターフェースの Release を呼び出す共通ヘルパー
    public static unsafe void ReleasePlugInInterface(nint ppInterface)
    {
        if (ppInterface == nint.Zero)
        {
            return;
        }

        var vtable = *(nint*)ppInterface;
        var releaseFn = (delegate* unmanaged<nint, uint>)(*((nint*)vtable + 3));
        releaseFn(ppInterface);
    }
}
