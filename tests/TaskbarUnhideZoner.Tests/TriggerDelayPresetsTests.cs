using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Tests;

public sealed class TriggerDelayPresetsTests
{
    [Fact]
    public void DetectExact_ReturnsPreset_WhenDelayMatches()
    {
        var config = new AppConfig
        {
            TriggerDelayMs = 180,
            DelayPresets = new DelayPresets { QuickMs = 180, DefaultMs = 350, LongMs = 700 }
        };

        var detected = TriggerDelayPresets.DetectExact(config);

        Assert.Equal(DelayPreset.Quick, detected);
    }

    [Fact]
    public void DetectExact_ReturnsNull_ForCustomDelay()
    {
        var config = new AppConfig
        {
            TriggerDelayMs = 255,
            DelayPresets = new DelayPresets { QuickMs = 180, DefaultMs = 350, LongMs = 700 }
        };

        var detected = TriggerDelayPresets.DetectExact(config);

        Assert.Null(detected);
    }
}
