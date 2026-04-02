using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Runtime;

internal static class RevealMethodSpikeRunner
{
    public static int Run()
    {
        var state = new TaskbarStateService();
        var explorer = new ExplorerMessageRevealService();

        RollingFileLogger.Info("SPIKE_START explorer-message probe");
        var works = explorer.ProbeWorks(state);
        var result = works ? "PASS" : "FAIL";

        RollingFileLogger.Info($"SPIKE_RESULT {result}");
        Console.WriteLine($"Explorer message probe: {result}");
        return works ? 0 : 3;
    }
}
