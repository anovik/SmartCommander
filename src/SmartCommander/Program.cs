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
        static FileStream? _lockFile;
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var exception = false;
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
                .StartWithClassicDesktopLifetime(args);//TODO must avoiding try excpet operators for MAIN thread, debuging of application becoming impossible
            }
            catch
            {
                exception = true;
            }

            if (exception)
            {
                try
                {
                    var client = new NamedPipeClientStream("SmartCommanderActivation");
                    client.Connect(1000);
                    using (StreamWriter writer = new StreamWriter(client))
                    {
                        writer.WriteLine("ActivateSmartCommander");
                        writer.Flush();                    
                    }

                }
                catch { }
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
