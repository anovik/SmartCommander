using Avalonia.Controls;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive;
using System.Reflection;

namespace SmartCommander.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        public string RepositoryUrl { get; } = "https://github.com/anovik/SmartCommander";

        public string AppVersion { get; } = GetAppVersion();

        private static string GetAppVersion()
        {
            // InformationalVersion includes a "+<git-sha>" build metadata suffix (SDK default
            // when a git repo is detected); strip it since only the release version is user-facing.
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informational))
            {
                var plusIndex = informational.IndexOf('+');
                return plusIndex >= 0 ? informational[..plusIndex] : informational;
            }
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        }

        public ReactiveCommand<Unit, Unit> OpenRepositoryCommand { get; }
        public ReactiveCommand<Window, Unit> CloseCommand { get; }

        public AboutViewModel()
        {
            OpenRepositoryCommand = ReactiveCommand.Create(OpenRepository);
            CloseCommand = ReactiveCommand.Create<Window>(Close);
        }

        private void OpenRepository()
        {
            new Process
            {
                StartInfo = new ProcessStartInfo(RepositoryUrl)
                {
                    UseShellExecute = true
                }
            }.Start();
        }

        private void Close(Window window)
        {
            window?.Close();
        }
    }
}
