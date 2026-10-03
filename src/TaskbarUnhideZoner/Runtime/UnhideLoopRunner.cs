using TaskbarUnhideZoner.Interop;
using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Runtime;

internal static class UnhideLoopRunner
{
    public static int Run(string[] args)
    {
        var intervalMs = ParseInt(args, "--interval-ms", 5000, 250, 60000);
        var durationSec = ParseInt(args, "--duration-sec", 60, 5, 600);
        var revealHoldMs = ParseInt(args, "--reveal-hold-ms", 1500, 250, 10000);
        var attempts = Math.Max(1, (durationSec * 1000) / intervalMs);
        var taskbarState = new TaskbarStateService();
        var reveal = new TaskbarMessageRevealService();

        RollingFileLogger.Info($"UNHIDE_LOOP_START intervalMs={intervalMs} durationSec={durationSec} attempts={attempts} revealHoldMs={revealHoldMs}");

        for (var i = 1; i <= attempts; i++)
        {
            var ok = TryRevealOnce(taskbarState, reveal, revealHoldMs, out var details);
            var status = ok ? "PASS" : "FAIL";
            var line = $"[{DateTime.Now:HH:mm:ss}] Unhide attempt {i}/{attempts}: {status} ({details})";
            RollingFileLogger.Info(line);
            Console.WriteLine(line);

            if (i < attempts)
            {
                Thread.Sleep(intervalMs);
            }
        }

        RollingFileLogger.Info("UNHIDE_LOOP_END");
        return 0;
    }

    private static bool TryRevealOnce(TaskbarStateService taskbarState, TaskbarMessageRevealService reveal, int revealHoldMs, out string details)
    {
        if (!taskbarState.IsAutoHideEnabled())
        {
            details = "autohide off; skipped";
            return true;
        }

        var workAreasBefore = GetWorkAreas();
        var shownSamples = 0;
        var samples = 0;
        for (var elapsed = 0; elapsed < revealHoldMs; elapsed += 200)
        {
            reveal.Reveal();
            Thread.Sleep(200);

            // The first sample may still be inside Explorer's slide-in animation.
            if (elapsed == 0)
            {
                continue;
            }

            samples++;
            if (AllTaskbarsShown())
            {
                shownSamples++;
            }
        }

        var workAreasUnchanged = workAreasBefore.SequenceEqual(GetWorkAreas());
        var autoHideKept = taskbarState.IsAutoHideEnabled();
        details = $"allTaskbarsShown={shownSamples}/{samples}, workAreasUnchanged={workAreasUnchanged}, autoHideKept={autoHideKept}";
        return shownSamples == samples && workAreasUnchanged && autoHideKept;
    }

    private static bool AllTaskbarsShown()
    {
        var taskbars = new List<IntPtr> { NativeMethods.FindWindow("Shell_TrayWnd", null) };
        var secondary = IntPtr.Zero;
        while ((secondary = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            taskbars.Add(secondary);
        }

        return taskbars.All(taskbar => taskbar != IntPtr.Zero && TaskbarMessageRevealService.IsTaskbarShown(taskbar));
    }

    private static List<Rectangle> GetWorkAreas()
    {
        return Screen.AllScreens.Select(screen => screen.WorkingArea).ToList();
    }

    private static int ParseInt(string[] args, string key, int fallback, int min, int max)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(args[i + 1], out var value))
            {
                break;
            }

            return Math.Clamp(value, min, max);
        }

        return fallback;
    }
}
