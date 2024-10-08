using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;
using SmartCommander.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace SmartCommander
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var haveSecondInstance = false;
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartCommander");
            Directory.CreateDirectory(dir);

            string currentProcessName = Process.GetCurrentProcess().ProcessName;
            var runningProcesses = Process.GetProcessesByName(currentProcessName);
            haveSecondInstance = (runningProcesses.Length > 1);

            if (haveSecondInstance && OptionsModel.Instance.AllowOnlyOneInstance)
            {
                    var client = new NamedPipeClientStream("SmartCommanderActivation");
                    client.Connect(1000);
                    using (StreamWriter writer = new StreamWriter(client))
                    {
                        writer.WriteLine("ActivateSmartCommander");
                        writer.Flush();
                    }
            }
            else {
                Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
#if DEBUG
                   .WriteTo.Console()
#endif
                   .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                   .CreateLogger();                

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
