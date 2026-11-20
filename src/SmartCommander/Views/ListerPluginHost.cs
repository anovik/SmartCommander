using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using Avalonia.Threading;
using SmartCommander.Plugins;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SmartCommander.Views;

// Embeds a Total Commander lister plugin's window in the viewer: the first candidate whose ListLoad
// accepts the file wins. Windows-only - non-Windows lister embedding would need non-Windows plugins,
// which aren't planned.
//
// Nothing native happens until Avalonia calls CreateNativeControlCore with the real parent window
// (the SDK has the plugin create its window as a child of ParentWin). Avalonia's normal teardown
// (viewer closed) calls ListerWindowHandle.Destroy, which closes the plugin window exactly once.
[SupportedOSPlatform("windows")]
internal sealed class ListerPluginHost : NativeControlHost
{
    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    private readonly IReadOnlyList<ListerPlugin> _candidates;
    private readonly string _filePath;
    private readonly int _showFlags;
    private ListerWindowHandle? _handle;

    // Raised (posted, never from inside the native-control callback) once a plugin shows the file.
    public event EventHandler? Accepted;

    // Raised (posted) when every candidate declined the file; the owner removes this host.
    public event EventHandler? Declined;

    public ListerPluginHost(IReadOnlyList<ListerPlugin> candidates, string filePath, int showFlags)
    {
        _candidates = candidates;
        _filePath = filePath;
        _showFlags = showFlags;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        foreach (ListerPlugin plugin in _candidates)
        {
            IntPtr listWin = plugin.Load(parent.Handle, _filePath, _showFlags);
            if (listWin != IntPtr.Zero)
            {
                // SDK: "When Lister is activated, it sets focus to your window". Background
                // priority so the window is attached and laid out first.
                Dispatcher.UIThread.Post(() =>
                {
                    SetFocus(listWin);
                    Accepted?.Invoke(this, EventArgs.Empty);
                }, DispatcherPriority.Background);
                _handle = new ListerWindowHandle(listWin, plugin);
                return _handle;
            }
        }
        Dispatcher.UIThread.Post(() => Declined?.Invoke(this, EventArgs.Empty));
        return base.CreateNativeControlCore(parent);
    }

    // Closes the plugin window now. Avalonia's own teardown is deferred to a background dispatcher
    // pass, which would run after ShowDialog returned - too late for the FTP view's temp-folder
    // delete, and the plugin should get ListCloseWindow while the viewer still exists anyway.
    // The later Avalonia Destroy call is then a no-op.
    public void CloseListerWindow()
    {
        _handle?.Destroy();
    }

    private sealed class ListerWindowHandle : PlatformHandle, INativeControlHostDestroyableControlHandle
    {
        private readonly ListerPlugin _plugin;
        private bool _destroyed;

        public ListerWindowHandle(IntPtr listWin, ListerPlugin plugin) : base(listWin, "HWND")
        {
            _plugin = plugin;
        }

        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }
            _destroyed = true;
            _plugin.Close(Handle);
        }
    }
}
