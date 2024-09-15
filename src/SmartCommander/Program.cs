using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace SmartCommander
{
    internal class Program
    {
        private static FileStream? _lockFile;
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            bool exception = false;
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartCommander");
            _ = Directory.CreateDirectory(dir);
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _lockFile = File.Open(Path.Combine(dir, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    _lockFile.Lock(0, 0);
                }
                _ = BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            }
            catch
            {
                exception = true;
            }

            if (exception)
            {
                try
                {
                    NamedPipeClientStream client = new("SmartCommanderActivation");
                    client.Connect(1000);
                    using StreamWriter writer = new(client);
                    writer.WriteLine("ActivateSmartCommander");
                    writer.Flush();

                }
                catch { }
            }

        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                        .UsePlatformDetect()
                        .LogToTrace()
                        .UseReactiveUI();
        }
    }


}
