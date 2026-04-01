using System.Threading;
using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Runtime;
using TaskbarUnhideZoner.Tray;

namespace TaskbarUnhideZoner;

internal static class Program
{
    private const string SingleInstanceMutexName = "Local\\TaskbarUnhideZoner";
    private static Mutex? _singleInstanceMutex;
    private static bool _singleInstanceHasHandle;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory(Paths.AppDirectory);

        RollingFileLogger.Initialize(Paths.LogFilePath);
        RollingFileLogger.Info("Taskbar Unhide Zoner starting");

        if (args.Contains("--harness", StringComparer.OrdinalIgnoreCase))
        {
            return HarnessRunner.Run(args);
        }

        if (!TryAcquireSingleInstance())
        {
            RollingFileLogger.Info("Another instance is already running; exiting.");
            return 1;
        }

        try
        {
            var config = ConfigStore.LoadOrCreate(Paths.ConfigFilePath);
            using var runtime = new RuntimeController(config);
            Application.Run(new TrayApp(runtime));
            return 0;
        }
        catch (Exception ex)
        {
            RollingFileLogger.Error($"Fatal startup error: {ex}");
            return 2;
        }
        finally
        {
            ReleaseSingleInstance();
            RollingFileLogger.Info("Taskbar Unhide Zoner shutting down");
            RollingFileLogger.Dispose();
        }
    }

    private static bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: false, name: SingleInstanceMutexName);
            try
            {
                _singleInstanceHasHandle = _singleInstanceMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                _singleInstanceHasHandle = true;
            }

            return _singleInstanceHasHandle;
        }
        catch
        {
            return true;
        }
    }

    private static void ReleaseSingleInstance()
    {
        try
        {
            if (_singleInstanceHasHandle)
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
        }
        catch
        {
        }

        try
        {
            _singleInstanceMutex?.Dispose();
        }
        catch
        {
        }
    }
}
