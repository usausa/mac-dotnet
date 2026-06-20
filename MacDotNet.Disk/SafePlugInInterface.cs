namespace MacDotNet.Disk;

using System.Runtime.InteropServices;

internal sealed unsafe class SafePlugInInterface : SafeHandle
{
    public SafePlugInInterface(IntPtr pointer)
        : base(IntPtr.Zero, true)
    {
        SetHandle(pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public IntPtr Pointer => handle;

    protected override bool ReleaseHandle()
    {
        var vtable = *(IntPtr*)handle;
        var releaseFn = (delegate* unmanaged<IntPtr, uint>)(*((IntPtr*)vtable + 3));
        releaseFn(handle);
        return true;
    }
}
