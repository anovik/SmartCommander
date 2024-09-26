using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using SmartCommander.TcPlugins;
using SmartCommander.ViewModels;
using System;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Reflection.Metadata;

namespace SmartCommander;

public partial class ViewerWindow : Window
{
    ListerPluginWrapper listerPluginWrapper { get; set; }
    IntPtr listerWindowHandle { get; set; }
    public ViewerWindow()
    {
        InitializeComponent();
        this.Opened += OnWindowOpened;


    }
    private void OnWindowOpened(object? sender, EventArgs e)
    {



        var grid = this.FindControl<Grid>("GridPanel");
        if (grid != null)
        {
            var viewModel = this.DataContext as ViewerViewModel;


            var embed = new EmbedSample(viewModel!.Filename);
            grid.Children.Add(embed);
        }

    }

    private IntPtr GetWindowHandle()
    {
        var platformHandle=this.TryGetPlatformHandle();
        if (platformHandle!=null)
        {
            return platformHandle.Handle;
        }
        return IntPtr.Zero;
    }
}

internal class Win32WindowControlHandle : PlatformHandle, INativeControlHostDestroyableControlHandle
{
    public Win32WindowControlHandle(IntPtr handle, string descriptor) : base(handle, descriptor)
    {
    }

    public void Destroy()
    {
        ///_ = WinApi.DestroyWindow(Handle);
    }
}

public class EmbedSample : NativeControlHost
{
    public static INativeDemoControl? Implementation { get; set; }
    ListerPluginWrapper listerPluginWrapper { get; set; }
    IntPtr listerWindowHandle { get; set; }
    string filename;

    public EmbedSample(string Filename)
    {
        HorizontalAlignment = 0;
        VerticalAlignment = 0;
        filename = Filename;
    }
    //static EmbedSample()
    //{

    //}

    public bool IsSecond { get; set; }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        listerPluginWrapper = PluginManager.CreateListerWrapper();
        listerWindowHandle = listerPluginWrapper.CreateListerWindow(parent.Handle, filename);
        return new Win32WindowControlHandle(listerWindowHandle, "Lister");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        listerPluginWrapper.CloseWindow(listerWindowHandle);
        base.DestroyNativeControlCore(control);
    }
}

public interface INativeDemoControl
{
    /// <param name="isSecond">Used to specify which control should be displayed as a demo</param>
    /// <param name="parent"></param>
    /// <param name="createDefault"></param>
    IPlatformHandle CreateControl(bool isSecond, IPlatformHandle parent, Func<IPlatformHandle> createDefault);
}

