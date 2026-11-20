using Serilog;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SmartCommander.Plugins;

// One loaded Total Commander lister (WLX) plugin DLL, per the WLX SDK (listplug.h).
// Every call must be made on the UI thread: plugin windows have thread affinity.
// The DLL is never unloaded (see ListerPluginRegistry).
[SupportedOSPlatform("windows")]
internal sealed class ListerPlugin
{
    // ListLoad ShowFlags: lcp_wraptext. The only flag passed so far.
    public const int ShowFlagWrapText = 1;

    // SDK: "PluginInterfaceVersionLow ... the value after the comma, multiplied by 100".
    // 2.12 is the revision Total Commander 10 reports (adds the dark-mode show flags).
    private const uint InterfaceVersionHi = 2;
    private const uint InterfaceVersionLow = 12;
    private const int MaxPath = 260;
    private const int DetectStringBufferSize = 2048;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr ListLoadDelegate(IntPtr parentWin, byte[] fileToLoad, int showFlags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr ListLoadWDelegate(IntPtr parentWin, [MarshalAs(UnmanagedType.LPWStr)] string fileToLoad, int showFlags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ListCloseWindowDelegate(IntPtr listWin);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ListGetDetectStringDelegate(byte[] detectString, int maxLen);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ListSetDefaultParamsDelegate(ref ListDefaultParamStruct dps);

    [StructLayout(LayoutKind.Sequential)]
    private struct ListDefaultParamStruct
    {
        public int Size;
        public uint PluginInterfaceVersionLow;
        public uint PluginInterfaceVersionHi;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPath)]
        public byte[] DefaultIniName;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    private readonly ListLoadDelegate? _listLoad;
    private readonly ListLoadWDelegate? _listLoadW;
    private readonly ListCloseWindowDelegate? _listCloseWindow;

    public string DllPath { get; }
    public string Name => Path.GetFileName(DllPath);

    // Null when the plugin has no detect string or it failed to parse: then the plugin is offered
    // every file and ListLoad decides, as Total Commander does.
    private readonly DetectString? _detect;

    private ListerPlugin(string dllPath, IntPtr module)
    {
        DllPath = dllPath;
        _listLoad = Bind<ListLoadDelegate>(module, "ListLoad");
        _listLoadW = Bind<ListLoadWDelegate>(module, "ListLoadW");
        _listCloseWindow = Bind<ListCloseWindowDelegate>(module, "ListCloseWindow");

        var setDefaultParams = Bind<ListSetDefaultParamsDelegate>(module, "ListSetDefaultParams");
        if (setDefaultParams != null)
        {
            var dps = new ListDefaultParamStruct
            {
                Size = Marshal.SizeOf<ListDefaultParamStruct>(),
                PluginInterfaceVersionLow = InterfaceVersionLow,
                PluginInterfaceVersionHi = InterfaceVersionHi,
                DefaultIniName = GetDefaultIniName(),
            };
            setDefaultParams(ref dps);
        }

        var getDetectString = Bind<ListGetDetectStringDelegate>(module, "ListGetDetectString");
        if (getDetectString != null)
        {
            _detect = ReadDetectString(getDetectString);
        }
    }

    // Throws when the DLL can't be loaded (missing, 32-bit -> BadImageFormatException) or exports
    // neither ListLoad nor ListLoadW; the caller logs and remembers the failure.
    public static ListerPlugin Load(string dllPath)
    {
        IntPtr module = NativeLibrary.Load(dllPath);
        if (!NativeLibrary.TryGetExport(module, "ListLoad", out _) && !NativeLibrary.TryGetExport(module, "ListLoadW", out _))
        {
            NativeLibrary.Free(module);
            throw new EntryPointNotFoundException("Exports neither ListLoad nor ListLoadW.");
        }
        return new ListerPlugin(dllPath, module);
    }

    public bool Accepts(DetectContext context)
    {
        return _detect == null || _detect.Matches(context);
    }

    // The plugin's window as a child of parentWin, or IntPtr.Zero when it declines the file.
    public IntPtr Load(IntPtr parentWin, string filePath, int showFlags)
    {
        if (_listLoadW != null)
        {
            return _listLoadW(parentWin, filePath, showFlags);
        }
        if (!AnsiPath.TryEncodePath(filePath, out byte[] ansiPath))
        {
            Log.Information("Lister plugin {Plugin} skipped for {File}: ANSI-only plugin and the path has no ANSI or 8.3 form", Name, filePath);
            return IntPtr.Zero;
        }
        return _listLoad!(parentWin, ansiPath, showFlags);
    }

    // SDK: "If your plugin doesn't export ListCloseWindow, Lister will call DestroyWindow".
    public void Close(IntPtr listWin)
    {
        if (_listCloseWindow != null)
        {
            _listCloseWindow(listWin);
        }
        else
        {
            DestroyWindow(listWin);
        }
    }

    private static T? Bind<T>(IntPtr module, string export) where T : Delegate
    {
        return NativeLibrary.TryGetExport(module, export, out IntPtr address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : null;
    }

    private DetectString? ReadDetectString(ListGetDetectStringDelegate getDetectString)
    {
        var buffer = new byte[DetectStringBufferSize];
        getDetectString(buffer, buffer.Length - 1);
        int length = Array.IndexOf(buffer, (byte)0);
        // Latin-1 maps every byte to one char and back, so FIND("...") needles round-trip to the
        // plugin's exact bytes.
        string text = Encoding.Latin1.GetString(buffer, 0, length < 0 ? buffer.Length : length);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return DetectString.Parse(text);
        }
        catch (FormatException ex)
        {
            Log.Warning("Lister plugin {Plugin}: unreadable detect string {Detect} ({Error}); offering it every file", Name, text, ex.Message);
            return null;
        }
    }

    // %AppData%\SmartCommander\lsplugin.ini (Total Commander's default name). The struct field is
    // ANSI: a folder outside the code page (e.g. a Cyrillic user name) goes as its 8.3 short form,
    // which needs the folder to exist. Empty when neither fits - the plugin then uses its own default.
    private byte[] GetDefaultIniName()
    {
        var field = new byte[MaxPath];
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartCommander");
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not create {Folder} for lsplugin.ini", folder);
        }

        string iniPath = Path.Combine(folder, "lsplugin.ini");
        if (!AnsiPath.TryEncode(iniPath, out byte[] bytes))
        {
            string? shortFolder = AnsiPath.GetShortPath(folder);
            if (shortFolder == null || !AnsiPath.TryEncode(Path.Combine(shortFolder, "lsplugin.ini"), out bytes))
            {
                Log.Warning("Lister plugin {Plugin}: {Ini} has no ANSI form; not passing a settings path", Name, iniPath);
                return field;
            }
        }
        if (bytes.Length > MaxPath)
        {
            Log.Warning("Lister plugin {Plugin}: {Ini} is longer than MAX_PATH; not passing a settings path", Name, iniPath);
            return field;
        }
        bytes.CopyTo(field, 0);
        return field;
    }
}
