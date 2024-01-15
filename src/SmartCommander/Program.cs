using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.IO;

namespace SmartCommander
{
    internal class Program
    {
        static FileStream _lockFile;
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
                _lockFile = File.Open(Path.Combine(dir, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                _lockFile.Lock(0, 0);
                BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            }
            catch
            {
               // TODO: need to activate another process
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
