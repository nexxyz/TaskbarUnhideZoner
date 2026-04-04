using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Tests;

public sealed class TriggerAssistPresetsTests
{
    [Fact]
    public void DetectExact_ReturnsNull_ForCustomValues()
    {
        var assist = new TriggerAssistConfig
        {
            Enabled = true,
            MinDelayPercent = 74,
            CurveExponent = 1.42
        };

        var detected = TriggerAssistPresets.DetectExact(assist);

        Assert.Null(detected);
    }

    [Fact]
    public void Apply_Strong_SetsExpectedValues()
    {
        var assist = new TriggerAssistConfig();

        TriggerAssistPresets.Apply(assist, TriggerAssistPreset.Strong);

        Assert.True(assist.Enabled);
        Assert.Equal(10, assist.MinDelayPercent);
        Assert.Equal(0.55, assist.CurveExponent, 3);
    }
}
