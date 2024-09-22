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

    private ListLoadDelegate _listLoad;
    private ListCloseWindowDelegate _listCloseWindow;
    private ListSendCommandDelegate _listSendCommand;

    public ListerPluginWrapper(string pluginPath)
    {
        _pluginHandle = NativeLibrary.Load(pluginPath);

        _listLoad = Marshal.GetDelegateForFunctionPointer<ListLoadDelegate>(
            NativeLibrary.GetExport(_pluginHandle, "ListLoad"));
        _listCloseWindow = Marshal.GetDelegateForFunctionPointer<ListCloseWindowDelegate>(
            NativeLibrary.GetExport(_pluginHandle, "ListCloseWindow"));
        _listSendCommand = Marshal.GetDelegateForFunctionPointer<ListSendCommandDelegate>(
            NativeLibrary.GetExport(_pluginHandle, "ListSendCommand"));
    }

    public IntPtr LoadFile(IntPtr parentWindowHandle, string filePath, int showFlags)
    {
        if (_listLoad == null) throw new InvalidOperationException("ListLoad function not available.");
        return _listLoad(parentWindowHandle, filePath, showFlags);
    }

    public void CloseWindow(IntPtr listWindowHandle)
    {
        if (_listCloseWindow == null) throw new InvalidOperationException("ListCloseWindow function not available.");
        _listCloseWindow(listWindowHandle);
    }

    public void SendCommand(IntPtr listWindowHandle, int command, int parameter)
    {
        if (_listSendCommand == null) throw new InvalidOperationException("ListSendCommand function not available.");
        _listSendCommand(listWindowHandle, command, parameter);
    }

    public void Dispose()
    {
        if (_pluginHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_pluginHandle);
            _pluginHandle = IntPtr.Zero;
        }
    }


    public static void CreateWrapper(IntPtr parentWindowHandle)
    {
        string pluginPath = "d:\\totalcmd\\plugins\\wlx\\CodeViewer\\CodeViewer.wlx64";
        var listerWrapper = new ListerPluginWrapper(pluginPath);

        int showFlags = 1;
        string fileToLoad = "C:\\Projects\\console_Lister\\ConsoleLister\\Program.cs";
        IntPtr listerWindowHandle = listerWrapper.LoadFile(parentWindowHandle, fileToLoad, showFlags);


    }
}

