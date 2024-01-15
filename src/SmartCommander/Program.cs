using Avalonia;
using Avalonia.ReactiveUI;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SmartCommander
{
    internal class Program
    {
        static FileStream? _lockFile;
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartCommander");
            Directory.CreateDirectory(dir);
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _lockFile = File.Open(Path.Combine(dir, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    _lockFile.Lock(0, 0);
                }
                BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
                // TODO: start listening to messages
            }
            catch
            {
                // TODO: send message to activate another instance of application
                return;
            }
           
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()                
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}
