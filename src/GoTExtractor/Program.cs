using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using Serilog.Core;

namespace GoTExtractor;

sealed class Program
{
    public static Logger appLog;

    const string _logFile = "GoTExtractor_log.txt";
    
    // The entry point. 
    [STAThread]
    public static void Main(string[] args)
    {
        if (File.Exists(_logFile)) File.Delete(_logFile);
        appLog = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console().WriteTo.File(_logFile)
            .CreateLogger();

        try
        {
            appLog.Information("Starting application");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            appLog.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}