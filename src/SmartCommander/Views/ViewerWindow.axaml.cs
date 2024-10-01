using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using SmartCommander.Models;
using SmartCommander.Plugins;
using SmartCommander.ViewModels;
using System;

namespace SmartCommander;

public partial class ViewerWindow : Window
{
    IntPtr listerWindowHandle { get; set; }
    public ViewerWindow()
    {
        InitializeComponent();
        this.Opened+= OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        var grid = this.FindControl<Grid>("GridPanel");
        if (grid != null)
        {
            var viewModel = this.DataContext as ViewerViewModel;

            if (OperatingSystem.IsWindows() && OptionsModel.Instance.ListerPlugins.Count > 0)
            {
                var embed = new EmbedSample(viewModel!.Filename);
                if (embed.CanShowByPlugin)
                {
                    grid.Children.Add(embed);
                }
            }
        }
    }
}

internal class Win32WindowControlHandle : PlatformHandle, INativeControlHostDestroyableControlHandle
{
    public Win32WindowControlHandle(IntPtr handle, string descriptor) : base(handle, descriptor)
    {
    }

    public void Destroy()
    {
    }
}

public class EmbedSample : NativeControlHost
{
    ListerPluginWrapper? listerPluginWrapper { get; set; }
    IntPtr listerWindowHandle { get; set; }

    public bool CanShowByPlugin
    {
        get
        {
            return (listerWindowHandle != IntPtr.Zero);
        }
    }

    public EmbedSample(string Filename)
    {
        HorizontalAlignment = 0;
        VerticalAlignment = 0;

        foreach (var ListerFileName in OptionsModel.Instance.ListerPlugins)
        {
            listerPluginWrapper?.Dispose();

            try
            {
                listerPluginWrapper = PluginManager.CreateListerWrapper(ListerFileName);
                listerWindowHandle = listerPluginWrapper.CreateListerWindow(IntPtr.Zero, Filename);

                if (listerWindowHandle != IntPtr.Zero)
                    return;
            }
            catch (BadImageFormatException ex)
            {
                Console.WriteLine($"we cannot load 32bit libraries: {ex.Message}");
            }

        }
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        //TODO better use  INativeDemoControl? Implementation; to make it cross platform
        return new Win32WindowControlHandle(listerWindowHandle, "Lister");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        listerPluginWrapper?.CloseWindow(listerWindowHandle);
        base.DestroyNativeControlCore(control);
    }
}
