using Avalonia.Controls;
using Serilog;
using SmartCommander.Plugins;
using SmartCommander.ViewModels;
using SmartCommander.Views;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace SmartCommander;

public partial class ViewerWindow : Window
{
    private bool _closed;
    private ListerPluginHost? _pluginHost;

    public ViewerWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel && OperatingSystem.IsWindows())
        {
            // Release the plugin (and its hold on the file) before ShowDialog returns.
            _pluginHost?.CloseListerWindow();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        base.OnClosed(e);
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsWindows() && ListerPluginRegistry.HasConfiguredPlugins && DataContext is ViewerViewModel viewModel)
        {
            await TryShowByPluginAsync(viewModel.Filename);
        }
    }

    // Offers the file to the lister plugins whose detect string matches it. With no candidates
    // nothing native happens at all and the text view stays.
    [SupportedOSPlatform("windows")]
    private async Task TryShowByPluginAsync(string filePath)
    {
        long size;
        byte[] header;
        try
        {
            (size, header) = await ReadHeaderAsync(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not read {File} for lister plugin detection", filePath);
            return;
        }
        if (_closed)
        {
            return;
        }

        var candidates = ListerPluginRegistry.GetCandidates(filePath, size, header);
        if (candidates.Count == 0)
        {
            return;
        }

        var host = new ListerPluginHost(candidates, filePath, ListerPlugin.ShowFlagWrapText);
        // The text editor stays under the plugin window until a plugin accepts the file; then it's
        // hidden so Tab can't land on it.
        host.Accepted += (_, _) => TextView.IsVisible = false;
        host.Declined += (_, _) =>
        {
            GridPanel.Children.Remove(host);
            _pluginHost = null;
        };
        _pluginHost = host;
        GridPanel.Children.Add(host);
    }

    // Size plus the first DetectContext.HeaderSize bytes, for detect strings ([n], FIND).
    // Direct I/O in the view layer, like ArchiveService: plugins need a local path anyway.
    private static async Task<(long Size, byte[] Header)> ReadHeaderAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        long size = stream.Length;
        var header = new byte[(int)Math.Min(DetectContext.HeaderSize, size)];
        int read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        return (size, read == header.Length ? header : header[..read]);
    }
}
