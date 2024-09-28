using System;
using System.Runtime.InteropServices;

public class ListerPluginWrapper : IDisposable
{
    private IntPtr _pluginHandle;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ListLoadDelegate(IntPtr parentWin, string fileToLoad, int showFlags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ListCloseWindowDelegate(IntPtr listWin);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ListSendCommandDelegate(IntPtr listWin, int command, int parameter);

    private ListLoadDelegate ListLoad;
    private ListCloseWindowDelegate ListCloseWindow;
    private ListSendCommandDelegate ListSendCommand;

    public ListerPluginWrapper(string pluginPath)
    {
        _pluginHandle = NativeLibrary.Load(pluginPath);

        ListLoad = Marshal.GetDelegateForFunctionPointer<ListLoadDelegate>(NativeLibrary.GetExport(_pluginHandle, nameof(ListLoad)));
        ListCloseWindow = Marshal.GetDelegateForFunctionPointer<ListCloseWindowDelegate>(NativeLibrary.GetExport(_pluginHandle, nameof(ListCloseWindow)));
        ListSendCommand = Marshal.GetDelegateForFunctionPointer<ListSendCommandDelegate>(NativeLibrary.GetExport(_pluginHandle, nameof(ListSendCommand)));
    }

    public IntPtr LoadFile(IntPtr parentWindowHandle, string filePath, int showFlags)
    {
        return ListLoad!(parentWindowHandle, filePath, showFlags);
    }

    public void CloseWindow(IntPtr listerWindowHandle)
    {
        ListCloseWindow!(listerWindowHandle);
    }

    public void SendCommand(IntPtr listerWindowHandle, int command, int parameter)
    {
        ListSendCommand!(listerWindowHandle, command, parameter);
    }

    public void Dispose()
    {
        if (_pluginHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_pluginHandle);
            _pluginHandle = IntPtr.Zero;
        }
    }
}

