using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal static class TriggerDelayPresets
{
    public static DelayPreset? DetectExact(AppConfig config)
    {
        var delayMs = config.TriggerDelayMs;
        if (delayMs == config.DelayPresets.QuickMs)
        {
            return DelayPreset.Quick;
        }

        if (delayMs == config.DelayPresets.DefaultMs)
        {
            return DelayPreset.Default;
        }

        if (delayMs == config.DelayPresets.LongMs)
        {
            return DelayPreset.Long;
        }

        return null;
    }
}
