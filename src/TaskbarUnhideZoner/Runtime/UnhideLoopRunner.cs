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

        RollingFileLogger.Info($"UNHIDE_LOOP_START intervalMs={intervalMs} durationSec={durationSec} attempts={attempts} mode=abm-setstate revealHoldMs={revealHoldMs}");

        for (var i = 1; i <= attempts; i++)
        {
            var ok = TryUnhideOnce(taskbarState, revealHoldMs, out var details);
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

    private static bool TryUnhideOnce(TaskbarStateService taskbarState, int revealHoldMs, out string details)
    {
        var before = taskbarState.GetStateFlags();
        var hadAutoHide = (before & Interop.NativeMethods.AbsAutoHide) != 0;

        if (!hadAutoHide)
        {
            details = "autohide already off; skipped";
            return true;
        }

        var showOk = taskbarState.SetAutoHideEnabled(false);
        Thread.Sleep(revealHoldMs);
        var restoreOk = taskbarState.SetAutoHideEnabled(true);
        var after = taskbarState.GetStateFlags();
        var finalAutoHide = (after & Interop.NativeMethods.AbsAutoHide) != 0;

        details = $"showOk={showOk}, restoreOk={restoreOk}, finalAutoHide={finalAutoHide}";
        return showOk && restoreOk && finalAutoHide;
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
