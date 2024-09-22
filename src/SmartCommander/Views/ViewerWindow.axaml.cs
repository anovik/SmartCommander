using Avalonia.Controls;
using Avalonia.Interactivity;
using SmartCommander.TcPlugins;
using SmartCommander.ViewModels;
using System;

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
        listerPluginWrapper = PluginManager.CreateListerWrapper();
        var viewModel = this.DataContext as ViewerViewModel;
        listerWindowHandle = listerPluginWrapper.CreateListerWindow(GetWindowHandle(), viewModel!.Filename);
        //  од, который выполн€етс€, когда окно открыто
        Console.WriteLine("Window has been shown.");
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