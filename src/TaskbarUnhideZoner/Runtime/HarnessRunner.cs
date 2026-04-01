using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Runtime;

internal static class HarnessRunner
{
    public static int Run(string[] args)
    {
        RollingFileLogger.Info("HARNESS_START");

        try
        {
            var config = ConfigStore.LoadOrCreate(Paths.ConfigFilePath);
            using var runtime = new RuntimeController(config);

            LogStep("Enable monitoring", () => runtime.SetEnabled(true));
            LogStep("Set quick preset", () => runtime.SetDelayPreset(DelayPreset.Quick));
            LogStep("Set default preset", () => runtime.SetDelayPreset(DelayPreset.Default));
            LogStep("Set long preset", () => runtime.SetDelayPreset(DelayPreset.Long));
            LogStep("Set edge top", () => runtime.SetEdgePosition(EdgePosition.Top));
            LogStep("Set edge bottom", () => runtime.SetEdgePosition(EdgePosition.Bottom));
            LogStep("Switch to hot zone", () => runtime.SetZoneMode(ZoneMode.HotZone));
            LogStep("Assign hot zone rectangle", () => runtime.SetHotZone(new Rectangle(0, 0, 300, 120)));
            LogStep("Disable monitoring", () => runtime.SetEnabled(false));
            LogStep("Enable monitoring again", () => runtime.SetEnabled(true));

            RollingFileLogger.Info("HARNESS_RESULT:PASS");
            return 0;
        }
        catch (Exception ex)
        {
            RollingFileLogger.Error($"HARNESS_RESULT:FAIL {ex}");
            return 10;
        }
    }

    private static void LogStep(string name, Action action)
    {
        RollingFileLogger.Info($"HARNESS_STEP:START {name}");
        action();
        RollingFileLogger.Info($"HARNESS_STEP:PASS {name}");
    }
}
