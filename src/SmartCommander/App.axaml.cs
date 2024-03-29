using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using SmartCommander.Models;
using SmartCommander.ViewModels;
using SmartCommander.Views;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SmartCommander
{
    public partial class App : Application
    {
        private MainWindow? mainWindow;
        private TrayIcon? trayIcon;
        private MainWindowViewModel? vm;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);            
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                vm = new MainWindowViewModel();
                mainWindow = new MainWindow
                {
                    DataContext = vm,
                };

                mainWindow.PropertyChanged += MainWindow_PropertyChanged;

                desktop.MainWindow = mainWindow;

                RegisterTrayIcon();

                ((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).ShutdownRequested += App_ShutdownRequested;                
            }

            StartServer();

            base.OnFrameworkInitializationCompleted();
        }

        private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is MainWindow && e.NewValue is WindowState windowState)
            {
                if (windowState == WindowState.Minimized)
                {
                    mainWindow?.Hide();
                    if (trayIcon != null)
                    {
                        trayIcon.IsVisible = true;
                    }
                }
                else
                {                   
                    mainWindow?.Show();
                    if (trayIcon != null)
                    {
                        trayIcon.IsVisible = false;
                    }
                }
            }         
        }

        private void RegisterTrayIcon()
        {
            trayIcon = new TrayIcon
            {             
                ToolTipText = "Smart Commander",
                Command = ReactiveCommand.Create(ShowApplication),
                Icon = mainWindow?.Icon,
                Menu = new NativeMenu() 
            };

            trayIcon.Menu.Add(new NativeMenuItem("Exit") { Command = vm?.ExitCommand });

            var trayIcons = new TrayIcons
            {
                trayIcon
            };

            SetValue(TrayIcon.IconsProperty, trayIcons);

            trayIcon.IsVisible = false;
        }

        private void ShowApplication()
        {
            if (mainWindow != null)
            {
                mainWindow.WindowState = WindowState.Maximized;              
                mainWindow.Topmost = true;
                mainWindow.Show();
                mainWindow.Topmost = false;
            }
        }

        private void App_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            var desktop = sender as ClassicDesktopStyleApplicationLifetime;
            if (desktop != null && desktop.MainWindow != null)
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
                if (desktop != null && desktop.MainWindow != null)
                {
                    OptionsModel.Instance.Left = desktop.MainWindow.Bounds.Left;
                    OptionsModel.Instance.Width = desktop.MainWindow.Bounds.Width;
                    OptionsModel.Instance.Top = desktop.MainWindow.Bounds.Top;
                    OptionsModel.Instance.Height = desktop.MainWindow.Bounds.Height;
                    OptionsModel.Instance.IsMaximized = desktop.MainWindow.WindowState == WindowState.Maximized;
                }
            }

            if (OptionsModel.Instance.SaveSettingsOnExit)
            {
                OptionsModel.Instance.Save();
            }
        }

        void StartServer()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    var server = new NamedPipeServerStream("SmartCommanderActivation");
                    server.WaitForConnection();
                    using (StreamReader reader = new StreamReader(server))
                    {
                        var line = reader.ReadLine();
                        if (line == "ActivateSmartCommander")
                        {                          
                            Dispatcher.UIThread.Post(() => ShowApplication());
                        }                   
                    }                  
                }
            });
        }
    }
}
