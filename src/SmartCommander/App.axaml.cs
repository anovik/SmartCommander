using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SmartCommander.Models;
using SmartCommander.ViewModels;
using SmartCommander.Views;
using System.Reflection;
using System;

namespace SmartCommander
{
    public partial class App : Application
    {
        private MainWindow? mainWindow;


        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };

                mainWindow.PropertyChanged += MainWindow_PropertyChanged;

                desktop.MainWindow = mainWindow;

                RegisterTrayIcon();

                ((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).ShutdownRequested += App_ShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is MainWindow && e.NewValue is WindowState windowState && windowState == WindowState.Minimized)
            {
                mainWindow?.Hide();
            }
        }

        private void RegisterTrayIcon()
        {
            IBitmap bitmap = new Bitmap(AvaloniaLocator.Current?.GetService<IAssetLoader>()?.Open(
                new Uri($"avares://{Assembly.GetExecutingAssembly().GetName().Name}/Assets/main.ico")));

            var trayIcon = new TrayIcon
            {
                IsVisible = true,
                ToolTipText = "Smart Commander",
                Command = ReactiveCommand.Create(ShowApplication),
                Icon = new WindowIcon(bitmap)
            };

            var trayIcons = new TrayIcons
            {
                trayIcon
            };

            SetValue(TrayIcon.IconsProperty, trayIcons);
        }

        private void ShowApplication()
        {
            if (mainWindow != null)
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Show();
            }
        }


        private void App_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            var desktop = sender as ClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                MainWindowViewModel? viewModel = desktop.MainWindow.DataContext as MainWindowViewModel;
                if (viewModel != null)
                {
                    OptionsModel.Instance.LeftPanePath = viewModel.LeftFileViewModel.CurrentDirectory;
                    OptionsModel.Instance.RightPanePath = viewModel.RightFileViewModel.CurrentDirectory;
                }
            }
            if (OptionsModel.Instance.SaveWindowPositionSize)
            {                
                if (desktop != null)
                {
                    OptionsModel.Instance.Left = desktop.MainWindow.Bounds.Left;
                    OptionsModel.Instance.Width = desktop.MainWindow.Bounds.Width;
                    OptionsModel.Instance.Top = desktop.MainWindow.Bounds.Top;
                    OptionsModel.Instance.Height = desktop.MainWindow.Bounds.Height;
                    OptionsModel.Instance.IsMaximized = desktop.MainWindow.WindowState == Avalonia.Controls.WindowState.Maximized;
                }
            }

            if (OptionsModel.Instance.SaveSettingsOnExit)
            {
                OptionsModel.Instance.Save();
            }
        }
    }
}
