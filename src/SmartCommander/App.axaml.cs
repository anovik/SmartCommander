using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SmartCommander.Models;
using SmartCommander.ViewModels;
using SmartCommander.Views;

namespace SmartCommander
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };

                ((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).ShutdownRequested += App_ShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void App_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {            
            if (OptionsModel.Instance.SaveWindowPositionSize)
            {
                var desktop = sender as ClassicDesktopStyleApplicationLifetime;
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
